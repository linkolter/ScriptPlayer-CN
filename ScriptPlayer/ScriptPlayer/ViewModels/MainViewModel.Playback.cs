using System;
using System.Threading.Tasks;
using ScriptPlayer.Playback;

namespace ScriptPlayer.ViewModels
{
    public partial class MainViewModel
    {
        public PlaybackCoordinator Playback { get; } = new PlaybackCoordinator();
        public PlaybackState PlaybackState => Playback.State;
        public double PlaybackRate { get => PlaybackState.Rate; set => Playback.SetRate(value); }
        public string PausedExitMedia => Playback.PausedExitMedia;
        private PlaybackState previousPlaybackState;

        private void PlaybackStateChanged(object sender, EventArgs e)
        {
            var state=PlaybackState;
            if (previousPlaybackState == null || previousPlaybackState.SessionId != state.SessionId ||
                previousPlaybackState.Status != state.Status || previousPlaybackState.IsSeeking != state.IsSeeking)
                RefreshRealtimePlayback();
            OnPropertyChanged(nameof(PlaybackState));
            if(previousPlaybackState==null || previousPlaybackState.Volume!=state.Volume)OnPropertyChanged(nameof(Volume));
            if(previousPlaybackState==null || previousPlaybackState.Rate!=state.Rate)OnPropertyChanged(nameof(PlaybackRate));
            previousPlaybackState=state;
        }

        private void AttachPlaybackEngine()
        {
            if(TimeSource==null){Playback.AttachEngine(null);AttachRealtimePlayback();return;}
            Func<string,TimeSpan,Task> open=null;
            Action<TimeSpan,TimeSpan> softSeek=null;
            if(PlaybackMode==PlaybackMode.Mpv)
                open=(file,start)=>MpvVideoPlayer.Open(file,start);
            else if(PlaybackMode==PlaybackMode.Local)
            {
                open=(file,start)=>VideoPlayer.Open(file,start,Settings.SoftSeekFiles?Settings.SoftSeekFilesDuration:TimeSpan.Zero);
                softSeek=(position,duration)=>VideoPlayer.SoftSeek(position,duration);
            }
            Playback.AttachEngine(new TimeSourceMediaEngine(TimeSource,open,value=>
            {
                if(VideoPlayer!=null)VideoPlayer.Volume=value;
                if(MpvVideoPlayer!=null)MpvVideoPlayer.Volume=value;
            },softSeek));
            AttachRealtimePlayback();
        }

        private async void ObservePlaybackOpen(Task opening,long session)
        {
            try
            {
                await opening;
                if(PlaybackState.SessionId==session)CheckDevices();
            }
            catch(OperationCanceledException) { }
            catch(Exception ex)
            {
                if(PlaybackState.SessionId==session)
                    OsdShowMessage("无法打开媒体："+ex.Message,TimeSpan.FromSeconds(5),"MediaOpenError");
            }
        }
    }
}
