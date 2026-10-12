using System;
using System.Linq;
using System.Threading;
using ScriptPlayer.Shared;

namespace ScriptPlayer.Playback
{
    public sealed class RealtimePlaybackService : IDisposable
    {
        sealed class Configuration
        {
            public readonly long Version, MediaGeneration;
            public readonly string File;
            public readonly ScriptPlaybackEngine Engine;
            public readonly TimeSpan? LoopStart, LoopEnd;
            public readonly bool OutputEnabled;
            public Configuration(long version, long generation, string file, ScriptPlaybackEngine engine, TimeSpan? loopStart, TimeSpan? loopEnd, bool outputEnabled)
            { Version = version; MediaGeneration = generation; File = file; Engine = engine; LoopStart = loopStart; LoopEnd = loopEnd; OutputEnabled = outputEnabled; }
        }
        readonly MpvTimeSource source;
        readonly PlaybackOutputPipeline[] outputs;
        Configuration configuration;
        long version, sampled, calculated;
        long manualRevision = -1;
        int disposed;
        bool wasPlaying;
        public long SampleCount => Interlocked.Read(ref sampled);
        public long CalculationCount => Interlocked.Read(ref calculated);
        public event EventHandler<ScriptOutputFrame> FrameCalculated;
        public event EventHandler<Exception> OutputFailed;
        public RealtimePlaybackService(MpvTimeSource source, params IPlaybackOutput[] outputs)
        {
            this.source = source;
            this.outputs = outputs.Select(output => new PlaybackOutputPipeline(output, IsCurrent)).ToArray();
            foreach (var output in this.outputs) output.Failed += OutputOnFailed;
            source.RealtimeSampled += Sample;
        }
        public void Configure(string file, long mediaGeneration, ScriptPlaybackEngine engine, TimeSpan? loopStart = null, TimeSpan? loopEnd = null, bool outputEnabled = true)
        {
            long next = Interlocked.Increment(ref version);
            Volatile.Write(ref configuration, file == null || engine == null ? null : new Configuration(next, mediaGeneration, file, engine, loopStart, loopEnd, outputEnabled));
            foreach (var output in outputs) output.Invalidate();
        }
        bool IsCurrent(ScriptOutputFrame frame)
        {
            if (Volatile.Read(ref disposed) != 0) return false;
            if (!frame.RequirePlaying)
            {
                var held = source.ReadPlaybackSample();
                return !held.IsPlaying && held.Revision == frame.Sample.Revision && held.MediaGeneration == frame.Sample.MediaGeneration;
            }
            var current = Volatile.Read(ref configuration);
            if (Volatile.Read(ref disposed) != 0 || current == null || !current.OutputEnabled || current.Version != frame.Configuration) return false;
            var sample = source.ReadPlaybackSample();
            return sample.IsPlaying && sample.Revision == frame.Sample.Revision && Matches(current, sample);
        }
        public void SendPausedCommand(DeviceCommandInformation command)
        {
            var sample = source.ReadPlaybackSample();
            if (sample.IsPlaying || Volatile.Read(ref disposed) != 0) return;
            Interlocked.Exchange(ref manualRevision, sample.Revision);
            var frame = new ScriptOutputFrame(sample, Interlocked.Read(ref version), 0, command, 0, TimeSpan.Zero, false);
            foreach (var output in outputs) output.Publish(frame, true);
        }
        static bool Matches(Configuration current, MpvPlaybackSample sample) => sample.MediaGeneration == current.MediaGeneration &&
            string.Equals(sample.File, current.File, StringComparison.OrdinalIgnoreCase);
        void Sample(object sender, MpvPlaybackSample sample)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            Interlocked.Increment(ref sampled);
            var current = Volatile.Read(ref configuration);
            bool playing = current != null && sample.IsPlaying && Matches(current, sample);
            if (!playing)
            {
                if (wasPlaying && Interlocked.Read(ref manualRevision) != sample.Revision)
                    foreach (var output in outputs) output.Invalidate();
                wasPlaying = false; return;
            }
            wasPlaying = true;
            if (current.LoopStart.HasValue && current.LoopEnd.HasValue && current.LoopEnd > current.LoopStart && sample.Position >= current.LoopEnd)
            {
                source.TryLoop(sample, current.LoopStart.Value);
                foreach (var output in outputs) output.Invalidate();
                return;
            }
            if (!current.OutputEnabled) return;
            var frame = current.Engine.Evaluate(sample, current.Version);
            Interlocked.Increment(ref calculated);
            foreach (var output in outputs) output.Publish(frame);
            FrameCalculated?.Invoke(this, frame);
        }
        void OutputOnFailed(object sender, Exception error) => OutputFailed?.Invoke(this, error);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Volatile.Write(ref configuration, null); source.RealtimeSampled -= Sample;
            foreach (var output in outputs) { output.Dispose(); output.Failed -= OutputOnFailed; }
            FrameCalculated = null; OutputFailed = null;
        }
    }
}
