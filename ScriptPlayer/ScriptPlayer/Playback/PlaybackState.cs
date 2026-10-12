using System;

namespace ScriptPlayer.Playback
{
    public enum PlaybackStatus { Empty, Loading, Ready, Failed }

    public sealed class PlaybackState
    {
        public long SessionId { get; }
        public string MediaPath { get; }
        public PlaybackStatus Status { get; }
        public TimeSpan Position { get; }
        public TimeSpan Duration { get; }
        public bool IsPlaying { get; }
        public bool IsSeeking { get; }
        public double Rate { get; }
        public double Volume { get; }
        public string Error { get; }

        public PlaybackState(long session, string media, PlaybackStatus status, TimeSpan position,
            TimeSpan duration, bool playing, bool seeking, double rate, double volume, string error)
        {
            SessionId=session;MediaPath=media;Status=status;Position=position;Duration=duration;
            IsPlaying=playing;IsSeeking=seeking;Rate=rate;Volume=volume;Error=error;
        }
    }
}
