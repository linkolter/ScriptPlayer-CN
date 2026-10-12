using System;
using System.Threading.Tasks;

namespace ScriptPlayer.Playback
{
    // Commands and immutable snapshots currently run on the adapter's owning thread.
    // Independent script scheduling is a later extraction, not a claim of this class.
    public sealed class PlaybackCoordinator : IDisposable
    {
        IMediaEngine engine;
        long session;
        string media, error;
        PlaybackStatus status;
        bool seeking, resumeAfterSeek, disposed;
        double volume=50;
        public event EventHandler StateChanged;
        public PlaybackState State { get; private set; } = new PlaybackState(0,null,PlaybackStatus.Empty,TimeSpan.Zero,TimeSpan.Zero,false,false,1,50,null);
        public string PausedExitMedia { get; private set; }

        public void AttachEngine(IMediaEngine next)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PlaybackCoordinator));
            if(engine!=null){engine.Changed-=EngineChanged;engine.Dispose();}
            session++;engine=next;media=null;status=PlaybackStatus.Empty;error=null;ResetSeek();PausedExitMedia=null;
            if(engine!=null){engine.Volume=volume;engine.Changed+=EngineChanged;}
            Publish();
        }

        public void SetMediaIdentity(string file)
        {
            if(string.Equals(media,file,StringComparison.OrdinalIgnoreCase))return;
            session++;media=file;PausedExitMedia=null;ResetSeek();
            status=string.IsNullOrEmpty(file)?PlaybackStatus.Empty:PlaybackStatus.Ready;error=null;Publish();
        }

        public async Task OpenAsync(string file, TimeSpan start)
        {
            if(disposed || engine==null)throw new InvalidOperationException("播放引擎尚未就绪。");
            var target=engine;long request=++session;
            media=file;status=PlaybackStatus.Loading;error=null;PausedExitMedia=null;ResetSeek();Publish();
            try
            {
                await target.OpenAsync(file,start);
                if(disposed || request!=session || target!=engine)throw new OperationCanceledException();
                status=PlaybackStatus.Ready;Publish();
            }
            catch(Exception ex)
            {
                if(disposed || request!=session || target!=engine)throw new OperationCanceledException("已被新的播放请求替代。",ex);
                // A failed replacement must not leave the old media playing behind a failed snapshot.
                if(target.CanPlayPause)target.Pause();
                status=PlaybackStatus.Failed;error=ex.Message;Publish();throw;
            }
        }

        public void Toggle()
        {
            if(engine?.IsPlaying==true)Pause(true);else Play();
        }
        public void Play()
        {
            if(engine?.CanPlayPause!=true || disposed)return;
            PausedExitMedia=null;engine.Play();Publish();
        }
        public void Pause(bool userInitiated)
        {
            if(engine?.CanPlayPause!=true || disposed)return;
            PausedExitMedia=userInitiated?media:null;engine.Pause();Publish();
        }
        public void Seek(TimeSpan position,int phase)
        {
            if(engine?.CanSeek!=true || disposed)return;
            if(phase==0){resumeAfterSeek=engine.IsPlaying;seeking=true;engine.Pause();}
            if(phase<0 || phase>2)return;
            engine.Seek(NonNegative(position),TimeSpan.Zero);
            if(phase==2)
            {
                bool resume=seeking && resumeAfterSeek;ResetSeek();
                if(resume){PausedExitMedia=null;engine.Play();}
            }
            Publish();
        }
        public void SeekTo(TimeSpan position,TimeSpan transition)
        {
            if(engine?.CanSeek!=true || disposed)return;
            engine.Seek(NonNegative(position),transition);Publish();
        }
        public void SetRate(double rate)
        {
            if(engine==null || disposed || double.IsNaN(rate) || double.IsInfinity(rate) || rate<=0)return;
            engine.Rate=rate;Publish();
        }
        public void SetVolume(double value)
        {
            if(disposed || double.IsNaN(value) || double.IsInfinity(value))return;
            volume=Math.Max(0,Math.Min(100,value));if(engine!=null)engine.Volume=volume;Publish();
        }
        void EngineChanged(object sender,EventArgs e)
        {
            if(disposed || sender!=engine)return;
            if(engine.IsPlaying)PausedExitMedia=null;
            Publish();
        }
        void ResetSeek(){seeking=false;resumeAfterSeek=false;}
        static TimeSpan NonNegative(TimeSpan value)=>value<TimeSpan.Zero?TimeSpan.Zero:value;
        void Publish()
        {
            bool ready=engine!=null && status!=PlaybackStatus.Loading && status!=PlaybackStatus.Failed;
            State=new PlaybackState(session,media,status,ready?engine.Position:TimeSpan.Zero,ready?engine.Duration:TimeSpan.Zero,
                ready && engine.IsPlaying,seeking,engine?.Rate??1,volume,error);
            StateChanged?.Invoke(this,EventArgs.Empty);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;session++;ResetSeek();PausedExitMedia=null;
            if(engine!=null){engine.Changed-=EngineChanged;engine.Dispose();engine=null;}
            StateChanged=null;
        }
    }
}
