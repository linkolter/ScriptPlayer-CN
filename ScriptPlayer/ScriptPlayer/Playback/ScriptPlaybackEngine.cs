using System;
using System.Collections.Generic;
using System.Linq;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Scripts;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Playback
{
    public sealed class PositionTransform
    {
        readonly byte inputMin, inputMax, min, max;
        readonly bool invert;
        readonly PositionFilterMode mode;
        readonly double filterRange;
        public PositionTransform(byte inputMin, byte inputMax, byte min, byte max, bool invert, PositionFilterMode mode, double filterRange)
        {
            this.inputMin = inputMin; this.inputMax = inputMax; this.min = min; this.max = max;
            this.invert = invert; this.mode = mode; this.filterRange = filterRange;
        }
        public byte Apply(byte position, TimeSpan timestamp)
        {
            double relative = inputMax == inputMin ? position / 99.0 : (double)(position - inputMin) / (inputMax - inputMin);
            relative = Math.Min(1, Math.Max(0, relative));
            if (invert) relative = 1 - relative;
            byte low = min, high = max;
            double cycle = timestamp.TotalSeconds / 10;
            double factor;
            switch (mode)
            {
                case PositionFilterMode.FullRange: return SpeedPredictor.ClampValue((byte)(low + (high - low) * relative));
                case PositionFilterMode.Top: factor = 1; break;
                case PositionFilterMode.Middle: factor = .5; break;
                case PositionFilterMode.Bottom: factor = 0; break;
                case PositionFilterMode.SineWave: factor = (1 + Math.Sin(cycle * Math.PI * 2)) / 2; break;
                case PositionFilterMode.TopBottom: factor = cycle - Math.Floor(cycle) >= .5 ? 1 : 0; break;
                default: throw new ArgumentOutOfRangeException();
            }
            double actualRange = (high - low) * (1 - filterRange);
            low = (byte)(low + actualRange * factor);
            high = (byte)(high - actualRange * (1 - factor));
            return SpeedPredictor.ClampValue((byte)(low + (high - low) * relative));
        }
    }

    // Prepared script and settings are copied on the owning UI thread, never read from a ViewModel by the clock.
    public sealed class ScriptPlaybackEngine
    {
        readonly FunScriptAction[] actions;
        readonly PositionTransform transform;
        readonly TimeSpan delay;
        readonly double multiplier;
        readonly byte minSpeed, maxSpeed;
        public ScriptPlaybackEngine(IEnumerable<FunScriptAction> actions, PositionTransform transform, TimeSpan delay,
            double multiplier, byte minSpeed, byte maxSpeed)
        {
            this.actions = actions.Select(action => action.Duplicate()).OrderBy(action => action.TimeStamp).ToArray();
            this.transform = transform; this.delay = delay; this.multiplier = multiplier;
            this.minSpeed = minSpeed; this.maxSpeed = maxSpeed;
        }
        public ScriptOutputFrame Evaluate(MpvPlaybackSample sample, long configuration)
        {
            TimeSpan progress = sample.Position - delay;
            int low = 0, high = actions.Length;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (actions[middle].TimeStamp <= progress) low = middle + 1; else high = middle;
            }
            int index = low - 1;
            if (index < 0 || index + 1 >= actions.Length) return new ScriptOutputFrame(sample, configuration, -1, null, 0, delay);
            var from = actions[index]; var to = actions[index + 1];
            TimeSpan duration = to.TimeStamp - from.TimeStamp;
            if (duration <= TimeSpan.Zero) return new ScriptOutputFrame(sample, configuration, -1, null, 0, delay);
            byte first = transform.Apply(from.Position, from.TimeStamp), last = transform.Apply(to.Position, to.TimeStamp);
            TimeSpan stretched = duration.Divide(sample.Rate);
            var command = new DeviceCommandInformation
            {
                PositionFromOriginal = from.Position, PositionToOriginal = to.Position,
                PositionFromTransformed = first, PositionToTransformed = last,
                SpeedOriginal = SpeedPredictor.PredictSpeed((byte)Math.Abs(from.Position - to.Position), stretched),
                SpeedTransformed = (byte)Math.Min(maxSpeed, Math.Max(minSpeed,
                    SpeedPredictor.PredictSpeed((byte)Math.Abs(first - last), stretched) * multiplier)),
                Duration = duration, DurationStretched = stretched, PlaybackRate = sample.Rate,
                SpeedMultiplier = multiplier, SpeedMin = minSpeed / 99.0, SpeedMax = maxSpeed / 99.0,
                TimeStamp = from.TimeStamp, MediaDuration = sample.Duration
            };
            return new ScriptOutputFrame(sample, configuration, index, command,
                Math.Min(1, Math.Max(0, (progress - from.TimeStamp).Divide(duration))), delay);
        }
    }

    public sealed class ScriptOutputFrame
    {
        readonly DeviceCommandInformation command;
        public MpvPlaybackSample Sample { get; }
        public long Configuration { get; }
        public int Segment { get; }
        public double Progress { get; }
        public TimeSpan ScriptDelay { get; }
        public bool RequirePlaying { get; }
        public long CreatedTicks { get; } = System.Diagnostics.Stopwatch.GetTimestamp();
        public ScriptOutputFrame(MpvPlaybackSample sample, long configuration, int segment, DeviceCommandInformation command, double progress, TimeSpan delay, bool requirePlaying = true)
        {
            Sample = sample; Configuration = configuration; Segment = segment; this.command = command; Progress = progress; ScriptDelay = delay;
            RequirePlaying = requirePlaying;
        }
        // Legacy device APIs use mutable command objects. Give each device its own copy.
        public DeviceCommandInformation CreateCommand()
        {
            if (command == null) return null;
            return new DeviceCommandInformation
            {
                PositionFromOriginal = command.PositionFromOriginal, PositionToOriginal = command.PositionToOriginal,
                PositionFromTransformed = command.PositionFromTransformed, PositionToTransformed = command.PositionToTransformed,
                SpeedOriginal = command.SpeedOriginal, SpeedTransformed = command.SpeedTransformed,
                Duration = command.Duration, DurationStretched = command.DurationStretched, PlaybackRate = command.PlaybackRate,
                SpeedMultiplier = command.SpeedMultiplier, SpeedMin = command.SpeedMin, SpeedMax = command.SpeedMax,
                TimeStamp = command.TimeStamp, MediaDuration = command.MediaDuration
            };
        }
    }
}
