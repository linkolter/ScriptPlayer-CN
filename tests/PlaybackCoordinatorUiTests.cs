using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using ScriptPlayer;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Controls;
using ScriptPlayer.ViewModels;

internal static class PlaybackCoordinatorUiTests
{
    static string log;static int failures;static MainWindow window;
    static void Check(bool yes,string text){File.AppendAllText(log,(yes?"PASS: ":"FAIL: ")+text+"\n");if(!yes)failures++;}
    static object Call(string name,params object[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,args);
    static async Task Wait(Func<bool> condition,string text)
    {
        for(int i=0;i<250 && !condition();i++)await Task.Delay(20);
        Check(condition(),text);if(!condition())throw new TimeoutException(text);
    }
    [STAThread]
    static void Main(string[] args)
    {
        log=args[1];File.WriteAllText(log,"Coordinator UI integration\n");
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        ScriptPlayer.Localization.ChineseLocalization.Initialize();var id=Guid.NewGuid().ToString("N");InstanceHandler.Startup("CoordinatorUI-"+id,"CoordinatorPipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary=XDocument.Load(args[2]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(dictionary.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        window=new MainWindow();window.InitializeComponent();app.MainWindow=window;
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                await Task.Delay(400);var vm=window.ViewModel;vm.Settings.WaitForDevicesToLoad=false;vm.LoadFile(args[0]);
                await Wait(()=>vm.PlaybackState.Status==PlaybackStatus.Ready && vm.PlaybackState.Duration.TotalSeconds>5,"Opening reaches unified ready state with duration");
                var native=(MpvController)typeof(MpvTimeSource).GetField("_controller",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(vm.TimeSource);
                Check(vm.PlaybackState.MediaPath==vm.LoadedMedia,"Unified state identifies loaded media");
                Call("QuickSettings_Click",window.FindName("QuickSettingsButton"),new RoutedEventArgs());await Task.Delay(100);
                var rate=(Slider)window.FindName("QuickPlaybackRate");rate.Value=1.25;await Task.Delay(100);
                Check(vm.PlaybackState.Rate==1.25 && Math.Abs(native.Speed-1.25)<0.01,"Quick rate slider updates coordinator and native MPV");
                var volume=Descendants((DependencyObject)window.FindName("PlayerControls")).OfType<Slider>().Single();volume.Value=37;await Task.Delay(100);
                Check(vm.PlaybackState.Volume==37 && Math.Abs(native.Volume-37)<0.1,"Volume slider updates coordinator and native MPV");
                Call("Mute_Click",new Button(),new RoutedEventArgs());await Task.Delay(50);
                Check(vm.PlaybackState.Volume==0 && native.Volume==0,"Mute uses the same coordinator volume state");
                Call("Mute_Click",new Button(),new RoutedEventArgs());Check(vm.PlaybackState.Volume==37,"Unmute restores prior volume");
                Call("ResetQuickSettings_Click",null,new RoutedEventArgs());Check(vm.PlaybackState.Rate==1 && native.Speed==1,"Quick reset restores engine and state rate together");
                Call("CloseQuickSettings_Click",null,new RoutedEventArgs());
                if(vm.TimeSource.IsPlaying)vm.TogglePlaybackCommand.Execute(null);
                await Wait(()=>!native.IsPaused==vm.PlaybackState.IsPlaying && !vm.PlaybackState.IsPlaying,"Pause command and native engine agree with unified state");
                var before=vm.PlaybackState;
                var seek=(ScriptPlayer.Shared.SeekBar)window.FindName("SeekBar");
                var handler=typeof(ScriptPlayer.Shared.SeekBar).GetMethod("OnSeek",BindingFlags.Instance|BindingFlags.NonPublic);
                handler.Invoke(seek,new object[]{0.4,TimeSpan.FromSeconds(4),0});
                Check(vm.PlaybackState.IsSeeking,"Timeline drag publishes seeking state");
                handler.Invoke(seek,new object[]{0.4,TimeSpan.FromSeconds(4),2});
                await Wait(()=>Math.Abs(vm.PlaybackState.Position.TotalSeconds-4)<0.1 && Math.Abs(native.Position-4)<0.2,"Timeline seeks through coordinator to actual native position");
                Check(!vm.PlaybackState.IsSeeking && !vm.PlaybackState.IsPlaying,"Paused timeline drag returns to paused state");
                await Task.Delay(100);
                Check(((TextBlock)window.FindName("CurrentPlaybackTime")).Text=="00:00:04","Timeline time label renders unified position");
                Check(!ReferenceEquals(before,vm.PlaybackState),"Playback updates replace state snapshots");
                long session=vm.PlaybackState.SessionId;
                vm.TogglePlaybackCommand.Execute(null);Call("NavigateTo","home");await Task.Delay(100);Call("NavigateTo","player");
                Check(vm.PlaybackState.SessionId==session && vm.PlaybackState.IsPlaying,"Mini player and player page share one running session");
                try{await vm.Playback.OpenAsync(Path.Combine(Path.GetDirectoryName(args[0]),"missing-coordinator-fixture.mp4"),TimeSpan.Zero);}catch(FileNotFoundException){}
                Check(vm.PlaybackState.Status==PlaybackStatus.Failed && !string.IsNullOrEmpty(vm.PlaybackState.Error),"Native open failure appears in unified state");
                Check(native.IsPaused && !vm.PlaybackState.IsPlaying,"Native failed replacement stops prior playback");
                vm.LoadFile(args[0]);await Wait(()=>vm.PlaybackState.Status==PlaybackStatus.Ready,"Normal load recovers after native failure");
                Check(vm.PlaybackState.Error==null && vm.PlaybackState.SessionId>session,"Recovery starts a new session and clears error");
            }
            catch(Exception ex){failures++;File.AppendAllText(log,"ERROR: "+ex+"\n");}
            finally{window.Close();File.AppendAllText(log,"Failures: "+failures+"\n");app.Shutdown(failures==0?0:1);}
        }));
        app.Run(window);Environment.ExitCode=failures==0?0:1;
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
        {var c=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return c;foreach(var n in Descendants(c))yield return n;}
    }
}
