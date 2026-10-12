using System;

namespace ScriptPlayer.Playback
{
    public enum TimelineMarkerKind { Bookmark, LoopA, LoopB, PausedExit }

    public sealed class TimelineMarker
    {
        public TimelineMarker(TimeSpan position, TimelineMarkerKind kind, string label)
        {
            Position = position;
            Kind = kind;
            Label = label;
        }
        public TimeSpan Position { get; }
        public TimelineMarkerKind Kind { get; }
        public string Label { get; }
    }
}
