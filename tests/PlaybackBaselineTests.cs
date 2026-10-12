using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using Newtonsoft.Json;
using ScriptPlayer;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Controls;
using ScriptPlayer.Shared.Scripts;
using ScriptPlayer.ViewModels;

// Characterization of the real v4 MPV + ScriptHandler path. No production instrumentation.
internal static class PlaybackBaselineTests
{
    static MainWindow window;
    static string log, output, scenario="startup";
    static int failures, scriptEvents, intervalErrors, stableActions;
    static ScriptActionEventArgs lastAction;
    static ScriptHandler script;
    static MpvController native;
    static Stopwatch clock=Stopwatch.StartNew();
    static List<object> trace=new List<object>();
    static Dictionary<string,object> metrics=new Dictionary<string,object>();
    static object Call(string method, params object[] args) => typeof(MainWindow).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,args);
    static void Check(bool condition,string message)
    {
        File.AppendAllText(log,(condition?"PASS: ":"FAIL: ")+message+"\n");
        if(!condition) failures++;
    }
    static async Task Until(Func<bool> predicate,string message,int timeout=5000)
    {
        var timer=Stopwatch.StartNew();
        while(!predicate() && timer.ElapsedMilliseconds<timeout)await Task.Delay(20);
        Check(predicate(),message);
        if(!predicate())throw new TimeoutException(message);
    }
    static void Sample(string kind, double? scriptMs=null, double? position=null)
    {
        scriptEvents++;
        trace.Add(new {kind,scenario,wallMs=clock.Elapsed.TotalMilliseconds,mediaMs=window.ViewModel.TimeSource.Progress.TotalMilliseconds,
            scriptMs,position,delayMs=window.ViewModel.Settings.ScriptDelay.TotalMilliseconds,thread=Thread.CurrentThread.ManagedThreadId});
    }
    static async Task Open(string file,double duration)
    {
        window.ViewModel.LoadFile(file);
        await Until(()=>Math.Abs(native.Duration-duration)<0.05 && window.ViewModel.LoadedScript==Path.ChangeExtension(file,"funscript"),"Native duration and matching script belong to requested file");
        await Task.Delay(180);
    }
    static async Task Paused()
    {
        if(window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlayback();
        await Until(()=>!window.ViewModel.TimeSource.IsPlaying && native.IsPaused,"Pause reaches native engine");
    }
    static async Task Playing()
    {
        if(!window.ViewModel.TimeSource.IsPlaying)window.ViewModel.TogglePlayback();
        await Until(()=>window.ViewModel.TimeSource.IsPlaying && !native.IsPaused,"Play reaches native engine");
    }
    static async Task Seek(double seconds)
    {
        window.ViewModel.Seek(TimeSpan.FromSeconds(seconds),0);
        window.ViewModel.Seek(TimeSpan.FromSeconds(seconds),1);
        window.ViewModel.Seek(TimeSpan.FromSeconds(seconds),2);
        await Until(()=>Math.Abs(native.Position-seconds)<0.25,"Native seek reaches target within 250 ms");
        await Task.Delay(100);
    }
    static void CheckAction(int second,int position,string name)
    {
        var action=lastAction?.RawCurrentAction as FunScriptAction;
        Check(action!=null && Math.Abs(action.TimeStamp.TotalSeconds-second)<0.001 && action.Position==position,name);
    }
    [STAThread]
    static void Main(string[] args)
    {
        output=args[2];log=Path.Combine(output,"playback-core.log");File.WriteAllText(log,"Playback v4 characterization\n");
        // Watchdog is test-owned and cannot stop a user player's process.
        Task.Run(async()=>{await Task.Delay(90000);File.AppendAllText(log,"ERROR: 90 second watchdog timeout\n");Environment.Exit(2);});
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        ScriptPlayer.Localization.ChineseLocalization.Initialize();
        var id=Guid.NewGuid().ToString("N");InstanceHandler.Startup("Baseline-"+id,"BaselinePipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources=XDocument.Load(args[3]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        window=new MainWindow();window.InitializeComponent();app.MainWindow=window;
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                await Task.Delay(400);
                var vm=window.ViewModel;
                vm.Settings.WaitForDevicesToLoad=false;vm.Settings.AutoSkip=false;vm.Settings.RandomChapters=false;vm.Settings.RepeatPlaylist=false;vm.Settings.ShufflePlaylist=false;
                vm.Settings.ScriptDelay=TimeSpan.Zero;ScriptHandler.SetDelay(TimeSpan.Zero);
                script=(ScriptHandler)typeof(MainViewModel).GetField("_scriptHandler",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm);
                script.FillGaps=false;script.LoopScriptToDuration=false;script.RangeExtender=0;
                native=(MpvController)typeof(MpvTimeSource).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm.TimeSource);
                script.ScriptActionRaised+=(sender,ev)=>
                {
                    lastAction=ev;var a=ev.RawCurrentAction as FunScriptAction;Sample("action",a?.TimeStamp.TotalMilliseconds,a?.Position);
                    if(a!=null && scenario!="rapid-switch")
                    {
                        stableActions++;
                        double effective=vm.TimeSource.Progress.TotalMilliseconds-vm.Settings.ScriptDelay.TotalMilliseconds;
                        if(a.TimeStamp.TotalMilliseconds>effective+1 || a.TimeStamp.TotalMilliseconds+1000<=effective-1)intervalErrors++;
                    }
                };
                script.IntermediateScriptActionRaised+=(sender,ev)=>Sample("intermediate",ev.TimeStamp.TotalMilliseconds,ev.Progress);
                scenario="open-A";await Open(args[0],12);await Playing();
                int count=scriptEvents;await Task.Delay(750);
                Check(scriptEvents-count>=3,"Script callbacks advance with real MPV playback");
                Check(script.GetScript().First().Position==10,"A uses its own script data");

                scenario="paused-drag";await Paused();await Seek(2.25);
                Check(!vm.TimeSource.IsPlaying,"Dragging while paused preserves pause");CheckAction(2,10,"Paused forward seek selects the correct script interval");
                double held=native.Position;count=scriptEvents;await Task.Delay(300);
                Check(Math.Abs(native.Position-held)<0.08,"Paused position does not drift");
                Check(scriptEvents==count,"Stable pause emits no advancing script callbacks");

                scenario="playing-drag";await Playing();vm.Seek(TimeSpan.FromSeconds(7.25),0);
                Check(!vm.TimeSource.IsPlaying,"Drag start temporarily pauses playing media");
                vm.Seek(TimeSpan.FromSeconds(5.25),1);vm.Seek(TimeSpan.FromSeconds(5.25),2);
                await Until(()=>native.Position>=5.2 && native.Position<5.8,"Drag end uses final target, not initial target");
                Check(vm.TimeSource.IsPlaying,"Drag end restores prior playing state");CheckAction(5,90,"Playing seek selects target script interval");
                await Paused();await Seek(1.25);CheckAction(1,90,"Backward seek resets script interval cache");

                scenario="delay";vm.Settings.ScriptDelay=TimeSpan.FromMilliseconds(250);await Seek(3.10);
                CheckAction(2,10,"Positive 250 ms delay evaluates earlier script time");
                vm.Settings.ScriptDelay=TimeSpan.FromMilliseconds(-250);await Seek(3.90);
                CheckAction(4,10,"Negative 250 ms delay evaluates later script time");
                vm.Settings.ScriptDelay=TimeSpan.Zero;

                scenario="speed-2x";await Seek(2.25);vm.TimeSource.PlaybackRate=2;await Playing();
                var speedClock=Stopwatch.StartNew();double start=native.Position;await Task.Delay(700);
                double measured=(native.Position-start)/speedClock.Elapsed.TotalSeconds;metrics["speed2xMeasured"]=measured;
                Check(measured>1.6 && measured<2.4,"2x speed advances media time once, not twice");
                await Paused();vm.TimeSource.PlaybackRate=1;

                scenario="rapid-switch";
                for(int i=0;i<6;i++){vm.LoadFile(i%2==0?args[0]:args[1]);await Task.Delay(35);}
                await Until(()=>Math.Abs(native.Duration-16)<0.05 && vm.LoadedScript==Path.ChangeExtension(args[1],"funscript"),"Rapid A/B switching settles on B native media and script");
                await Task.Delay(250);scenario="settled-B";await Paused();await Seek(2.25);
                CheckAction(2,80,"Rapid switching does not retain A script values");
                Check(vm.Playlist.CurrentEntry.Fullname==args[1],"Playlist selection matches settled media");

                scenario="shell-transitions";await Playing();var source=vm.TimeSource;double before=native.Position;
                Call("NavigateTo","home");await Task.Delay(100);Call("NavigateTo","library");await Task.Delay(100);Call("NavigateTo","player");
                Call("SetFullscreen",true,true);await Task.Delay(100);Call("SetFullscreen",false,true);
                Check(ReferenceEquals(source,vm.TimeSource),"Navigation and fullscreen preserve time-source instance");
                Check(vm.TimeSource.IsPlaying && native.Position>before,"Navigation and fullscreen preserve advancing playback");

                scenario="ui-stall";await Paused();await Seek(3.25);await Playing();await Task.Delay(150);
                count=scriptEvents;double nativeBefore=native.Position;double sampledBefore=vm.TimeSource.Progress.TotalSeconds;
                var stall=Stopwatch.StartNew();Thread.Sleep(650);double blockedMs=stall.Elapsed.TotalMilliseconds;
                metrics["uiStallMs"]=blockedMs;metrics["nativeAdvanceDuringStallMs"]=(native.Position-nativeBefore)*1000;
                metrics["sampledAdvanceDuringStallMs"]=(vm.TimeSource.Progress.TotalSeconds-sampledBefore)*1000;
                metrics["scriptCallbacksDuringStall"]=scriptEvents-count;
                File.AppendAllText(log,"OBSERVE: UI stall values are measurements, not an accepted synchronization guarantee.\n");
                await Until(()=>scriptEvents>count && Math.Abs(vm.TimeSource.Progress.TotalSeconds-native.Position)<0.20,"After UI recovers, script callbacks and media sampling catch up",1500);
                metrics["recoveryWaitMs"]=stall.Elapsed.TotalMilliseconds-blockedMs;
                CheckAction(4,80,"After UI stall, script selects current interval instead of replaying stale intervals");
                await Paused();
                metrics["validatedStableActionEvents"]=stableActions;
                metrics["scriptIntervalErrors"]=intervalErrors;
                Check(stableActions>=10 && intervalErrors==0,"Stable script action events bracket media time minus configured delay");
            }
            catch(Exception ex){failures++;File.AppendAllText(log,"ERROR: "+ex+"\n");}
            finally
            {
                File.WriteAllText(Path.Combine(output,"script-trace.json"),JsonConvert.SerializeObject(trace,Formatting.Indented));
                File.WriteAllText(Path.Combine(output,"timing-metrics.json"),JsonConvert.SerializeObject(metrics,Formatting.Indented));
                File.AppendAllText(log,"Failures: "+failures+"\n");window.Close();app.Shutdown(failures==0?0:1);
            }
        }));
        app.Run(window);Environment.ExitCode=failures==0?0:1;
    }
}
