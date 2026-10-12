using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ScriptPlayer.Playback;

internal static class PlaybackCoordinatorTests
{
    static string log;
    static int failures;
    static void Check(bool value,string text){File.AppendAllText(log,(value?"PASS: ":"FAIL: ")+text+"\n");if(!value)failures++;}
    static async Task Cancelled(Task task,string text)
    {try{await task;Check(false,text);}catch(OperationCanceledException){Check(true,text);}}
    static void Main(string[] args)
    {
        log=args[0];File.WriteAllText(log,"PlaybackCoordinator independent tests\n");
        try{Run().GetAwaiter().GetResult();}catch(Exception ex){failures++;File.AppendAllText(log,"ERROR: "+ex+"\n");}
        File.AppendAllText(log,"Failures: "+failures+"\n");Environment.ExitCode=failures==0?0:1;
    }
    static async Task Run()
    {
        var c=new PlaybackCoordinator();var engine=new FakeEngine();c.AttachEngine(engine);
        Check(engine.Volume==50,"Initial volume is applied when engine attaches");
        var first=c.OpenAsync("A",TimeSpan.Zero);var snapshot=c.State;
        Check(snapshot.Status==PlaybackStatus.Loading && snapshot.Position==TimeSpan.Zero,"Opening publishes loading state without old position");
        var second=c.OpenAsync("B",TimeSpan.Zero);long session=c.State.SessionId;
        engine.Complete(1);await second;engine.Complete(0);await Cancelled(first,"Older successful open is cancelled at coordinator boundary");
        Check(c.State.MediaPath=="B" && c.State.SessionId==session && c.State.Status==PlaybackStatus.Ready,"Older completion cannot replace newest state");
        Check(snapshot.MediaPath=="A" && snapshot.Status==PlaybackStatus.Loading,"Published snapshot remains immutable");
        first=c.OpenAsync("C",TimeSpan.Zero);second=c.OpenAsync("D",TimeSpan.Zero);
        engine.Complete(3);await second;engine.Fail(2);await Cancelled(first,"Older failed open is treated as superseded");
        Check(c.State.Status==PlaybackStatus.Ready && c.State.Error==null,"Older failure cannot mark current session failed");
        c.Play();var failed=c.OpenAsync("broken",TimeSpan.Zero);engine.Fail(4);
        try{await failed;}catch(InvalidOperationException){}
        Check(c.State.Status==PlaybackStatus.Failed && !string.IsNullOrEmpty(c.State.Error),"Current load failure has explicit error state");
        Check(!engine.IsPlaying && !c.State.IsPlaying,"Failed replacement pauses old media instead of hiding continuing playback");
        var ready=c.OpenAsync("ready",TimeSpan.Zero);engine.Complete(5);await ready;
        Check(c.State.Status==PlaybackStatus.Ready && c.State.Error==null,"Next successful open clears previous error");
        c.Toggle();Check(engine.IsPlaying && c.State.IsPlaying,"Toggle starts playback through coordinator");
        c.Toggle();Check(!engine.IsPlaying && c.PausedExitMedia=="ready","Toggle pause records user intent");

        c.Play();c.Seek(TimeSpan.FromSeconds(2),0);
        Check(!engine.IsPlaying && c.State.IsSeeking,"Drag start pauses a playing engine");
        c.Seek(TimeSpan.FromSeconds(3),1);c.Seek(TimeSpan.FromSeconds(4),2);
        Check(engine.IsPlaying && !c.State.IsSeeking && engine.Position.TotalSeconds==4,"Drag end restores playing state at final target");
        c.Pause(true);c.Seek(TimeSpan.FromSeconds(2),0);c.Seek(TimeSpan.FromSeconds(2),2);
        Check(!engine.IsPlaying && c.PausedExitMedia=="ready","Paused dragging preserves pause and explicit exit marker");
        c.Seek(TimeSpan.FromSeconds(3),2);
        Check(!engine.IsPlaying,"Standalone seek end does not restore stale drag state");
        c.Play();c.Pause(false);Check(c.PausedExitMedia==null,"Internal pause is not a paused-exit request");
        c.Pause(true);engine.Play();Check(c.PausedExitMedia==null,"Engine-originated resume clears explicit exit marker");
        c.Pause(true);first=c.OpenAsync("next",TimeSpan.Zero);Check(c.PausedExitMedia==null,"Open clears old paused-exit marker");engine.Complete(6);await first;
        c.SetRate(1.5);Check(c.State.Rate==1.5 && engine.Rate==1.5,"Rate command and snapshot agree");
        c.SetRate(double.NaN);Check(engine.Rate==1.5,"Invalid rate is ignored");
        c.SetVolume(120);Check(c.State.Volume==100 && engine.Volume==100,"Volume is clamped to valid output range");
        c.SetVolume(-1);Check(c.State.Volume==0,"Negative volume clamps to zero");
        c.SeekTo(TimeSpan.FromSeconds(-2),TimeSpan.Zero);Check(engine.Position==TimeSpan.Zero,"Negative seek clamps to media start");
        c.SeekTo(TimeSpan.FromSeconds(2),TimeSpan.FromMilliseconds(200));Check(engine.LastTransition.TotalMilliseconds==200,"Legacy soft-seek transition is preserved");
        c.Play();c.Seek(TimeSpan.FromSeconds(1),0);first=c.OpenAsync("switched",TimeSpan.Zero);engine.Complete(7);await first;
        c.Seek(TimeSpan.FromSeconds(2),2);Check(!engine.IsPlaying && !c.State.IsSeeking,"Switching media cancels previous drag resume intent");

        first=c.OpenAsync("external-old",TimeSpan.Zero);c.SetMediaIdentity("external-new");engine.Complete(8);
        await Cancelled(first,"Changing external media identity invalidates outstanding load");
        Check(c.State.MediaPath=="external-new","External identity survives stale completion");
        var old=engine;engine=new FakeEngine();c.AttachEngine(engine);
        int notifications=0;c.StateChanged+=(s,e)=>notifications++;
        old.Play();Check(notifications==0 && old.Disposed,"Replacing engine detaches old event subscriptions");
        engine.SetClock(7,20);Check(c.State.Position.TotalSeconds==7 && c.State.Duration.TotalSeconds==20,"Engine clock update publishes position and duration together");
        first=c.OpenAsync("closing",TimeSpan.Zero);c.Dispose();int before=notifications;engine.Complete(0);
        await Cancelled(first,"Dispose invalidates in-flight completion");engine.Play();
        Check(notifications==before && engine.Disposed,"Disposed coordinator emits no engine notifications");
    }
    sealed class FakeEngine:IMediaEngine
    {
        public event EventHandler Changed;
        public TimeSpan Position{get;private set;}
        public TimeSpan Duration{get;private set;}=TimeSpan.FromSeconds(30);
        public bool IsPlaying{get;private set;}
        public bool CanPlayPause=>true;
        public bool CanSeek=>true;
        public double Rate{get;set;}=1;
        public double Volume{get;set;}
        public bool Disposed;
        public TimeSpan LastTransition;
        List<TaskCompletionSource<bool>> pending=new List<TaskCompletionSource<bool>>();
        public Task OpenAsync(string file,TimeSpan start){var item=new TaskCompletionSource<bool>();pending.Add(item);return item.Task;}
        public void Complete(int index)=>pending[index].SetResult(true);
        public void Fail(int index)=>pending[index].SetException(new InvalidOperationException("fixture load failed"));
        public void Play(){IsPlaying=true;Changed?.Invoke(this,EventArgs.Empty);}
        public void Pause(){IsPlaying=false;Changed?.Invoke(this,EventArgs.Empty);}
        public void Seek(TimeSpan position,TimeSpan transition){Position=position;LastTransition=transition;Changed?.Invoke(this,EventArgs.Empty);}
        public void SetClock(double position,double duration){Position=TimeSpan.FromSeconds(position);Duration=TimeSpan.FromSeconds(duration);Changed?.Invoke(this,EventArgs.Empty);}
        public void Dispose(){Disposed=true;}
    }
}
