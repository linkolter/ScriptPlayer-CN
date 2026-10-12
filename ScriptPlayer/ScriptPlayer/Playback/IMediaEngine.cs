using System;
using System.Threading.Tasks;

namespace ScriptPlayer.Playback
{
    // Single-owner command/event contract; no UI or device types cross this boundary.
    public interface IMediaEngine : IDisposable
    {
        event EventHandler Changed;
        TimeSpan Position { get; }
        TimeSpan Duration { get; }
        bool IsPlaying { get; }
        bool CanPlayPause { get; }
        bool CanSeek { get; }
        double Rate { get; set; }
        double Volume { get; set; }
        Task OpenAsync(string file, TimeSpan start);
        void Play();
        void Pause();
        void Seek(TimeSpan position, TimeSpan transition);
    }
}
