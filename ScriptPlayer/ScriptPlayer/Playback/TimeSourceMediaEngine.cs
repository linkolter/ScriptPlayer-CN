using System;
using System.Threading.Tasks;
using ScriptPlayer.Shared;

namespace ScriptPlayer.Playback
{
    // Compatibility boundary for the existing WPF TimeSource and native video hosts.
    // Disposing this adapter detaches events; the window still owns the native hosts.
    public sealed class TimeSourceMediaEngine : IMediaEngine
    {
        readonly TimeSource source;
        readonly Func<string,TimeSpan,Task> open;
        readonly Action<double> setVolume;
        readonly Action<TimeSpan,TimeSpan> softSeek;
        double volume;
        public event EventHandler Changed;
        public TimeSourceMediaEngine(TimeSource source,Func<string,TimeSpan,Task> open,Action<double> setVolume,Action<TimeSpan,TimeSpan> softSeek)
        {
            this.source=source;this.open=open;this.setVolume=setVolume;this.softSeek=softSeek;
            source.ProgressChanged+=TimeChanged;source.DurationChanged+=TimeChanged;
            source.IsPlayingChanged+=PlayingChanged;source.PlaybackRateChanged+=RateChanged;
        }
        public TimeSpan Position=>source.Progress;
        public TimeSpan Duration=>source.Duration;
        public bool IsPlaying=>source.IsPlaying;
        public bool CanPlayPause=>source.CanPlayPause;
        public bool CanSeek=>source.CanSeek;
        public double Rate{get=>source.PlaybackRate;set=>source.PlaybackRate=value;}
        public double Volume{get=>volume;set{volume=value;setVolume?.Invoke(value);}}
        public Task OpenAsync(string file,TimeSpan start)=>open!=null?open(file,start):Task.FromException(new NotSupportedException("此播放模式不支持打开本地媒体。"));
        public void Play()=>source.Play();
        public void Pause()=>source.Pause();
        public void Seek(TimeSpan position,TimeSpan transition)
        {
            if(transition>TimeSpan.Zero && softSeek!=null)softSeek(position,transition);else source.SetPosition(position);
        }
        void TimeChanged(object sender,TimeSpan value)=>Changed?.Invoke(this,EventArgs.Empty);
        void PlayingChanged(object sender,bool value)=>Changed?.Invoke(this,EventArgs.Empty);
        void RateChanged(object sender,double value)=>Changed?.Invoke(this,EventArgs.Empty);
        public void Dispose()
        {
            source.ProgressChanged-=TimeChanged;source.DurationChanged-=TimeChanged;
            source.IsPlayingChanged-=PlayingChanged;source.PlaybackRateChanged-=RateChanged;Changed=null;
        }
    }
}
