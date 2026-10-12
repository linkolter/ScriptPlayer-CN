using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ScriptPlayer.Playback
{
    public interface IPlaybackOutput
    {
        Task SendAsync(ScriptOutputFrame frame, bool discontinuity, bool segmentChanged);
        Task StopAsync();
    }

    // One bounded mailbox and worker per device: slow I/O cannot stop media sampling or another device.
    public sealed class PlaybackOutputPipeline : IDisposable
    {
        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        readonly Thread worker;
        readonly IPlaybackOutput output;
        readonly Func<ScriptOutputFrame, bool> isCurrent;
        ScriptOutputFrame pending;
        bool stopPending, disposed;
        long version, lastConfiguration = -1, lastRevision = -1;
        int lastSegment = -1;
        public event EventHandler<Exception> Failed;
        public long DroppedFrames => Interlocked.Read(ref dropped);
        public long SentFrames => Interlocked.Read(ref sent);
        long dropped, sent;
        public PlaybackOutputPipeline(IPlaybackOutput output, Func<ScriptOutputFrame, bool> isCurrent)
        {
            this.output = output; this.isCurrent = isCurrent;
            worker = new Thread(Run) { IsBackground = true, Name = "ScriptPlayer Device Output" };
            worker.Start();
        }
        public void Publish(ScriptOutputFrame frame, bool stopFirst = false)
        {
            lock (gate)
            {
                if (disposed) return;
                if (stopFirst) { version++; stopPending = true; }
                if (pending != null) Interlocked.Increment(ref dropped);
                pending = frame; wake.Set();
            }
        }
        public void Invalidate()
        {
            lock (gate)
            {
                if (disposed) return;
                version++; pending = null; stopPending = true; wake.Set();
            }
        }
        void Run()
        {
            while (true)
            {
                wake.WaitOne();
                ScriptOutputFrame frame; bool stop, exit; long generation;
                lock (gate)
                {
                    stop = stopPending; stopPending = false; frame = pending; pending = null;
                    exit = disposed; generation = version;
                }
                if (stop)
                {
                    try { output.StopAsync().GetAwaiter().GetResult(); } catch (Exception error) { OnFailed(error); }
                    lastConfiguration = lastRevision = -1; lastSegment = -1;
                }
                if (exit) return;
                if (frame == null) continue;
                lock (gate) { if (generation != version || disposed) continue; }
                double age = (Stopwatch.GetTimestamp() - frame.CreatedTicks) * 1000.0 / Stopwatch.Frequency;
                if (age > 150 || !isCurrent(frame)) { Interlocked.Increment(ref dropped); continue; }
                bool discontinuity = lastConfiguration != frame.Configuration || lastRevision != frame.Sample.Revision;
                bool segmentChanged = discontinuity || lastSegment != frame.Segment;
                try
                {
                    if (discontinuity && lastConfiguration >= 0) output.StopAsync().GetAwaiter().GetResult();
                    // Recheck after a potentially slow stop; never send a queued frame from the old session.
                    lock (gate) { if (generation != version || disposed) continue; }
                    if (!isCurrent(frame) || (Stopwatch.GetTimestamp() - frame.CreatedTicks) * 1000.0 / Stopwatch.Frequency > 150) continue;
                    output.SendAsync(frame, discontinuity, segmentChanged).GetAwaiter().GetResult();
                    lastConfiguration = frame.Configuration; lastRevision = frame.Sample.Revision; lastSegment = frame.Segment;
                    Interlocked.Increment(ref sent);
                }
                catch (Exception error) { OnFailed(error); }
            }
        }
        void OnFailed(Exception error)
        {
            try { Failed?.Invoke(this, error); } catch (Exception reportingError) { Trace.WriteLine(reportingError); }
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; version++; pending = null; stopPending = true; wake.Set();
            }
            worker.Join(); // Device adapters must finish their current I/O before their native resources are disposed.
            wake.Dispose(); Failed = null;
        }
    }
}
