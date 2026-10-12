using System;

namespace ScriptPlayer.Shared
{
    // No WPF objects: this snapshot can safely cross the UI / playback boundary.
    public sealed class MpvPlaybackSample
    {
        public long Revision { get; }
        public long MediaGeneration { get; }
        public string File { get; }
        public TimeSpan Position { get; }
        public TimeSpan Duration { get; }
        public double Rate { get; }
        public bool IsPlaying { get; }
        public MpvPlaybackSample(long revision, long generation, string file, double position, double duration, double rate, bool playing)
        {
            Revision = revision; MediaGeneration = generation; File = file;
            Position = TimeSpan.FromSeconds(Math.Max(0, position));
            Duration = TimeSpan.FromSeconds(Math.Max(0, duration));
            Rate = rate; IsPlaying = playing && file != null && duration > 0;
        }
    }
}
