using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using ScriptPlayer;
using ScriptPlayer.ViewModels;

// Real layered-window hit testing: a transparent first frame must not pass hover to mpv.
internal static class MotionNativeHoverTests
{
    [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out CursorPoint point);
    [StructLayout(LayoutKind.Sequential)] struct CursorPoint { public int X,Y; }
    static MainWindow window;
    static int result;
    static void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);
    [STAThread] static int Main(string[] args)
    {
        var app=new Application {ShutdownMode=ShutdownMode.OnMainWindowClose}; ScriptPlayer.Localization.ChineseLocalization.Initialize();
        string id=Guid.NewGuid().ToString("N"); InstanceHandler.Startup("NativeMotion-"+id,"NativeMotionPipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources=XDocument.Load(args[0]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        CursorPoint original; bool haveCursor=GetCursorPos(out original);
        if (!haveCursor) {File.AppendAllText(args[2],"ERROR: Cannot preserve cursor before native hover test\nFailures: 1\n");return 1;}
        window=new MainWindow(); window.InitializeComponent(); app.MainWindow=window;
        var deadline=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(25)};
        deadline.Tick+=(s,e)=>{File.AppendAllText(args[2],"ERROR: Native hover test deadline\n");SetCursorPos(original.X,original.Y);Environment.Exit(2);}; deadline.Start();
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                window.ViewModel.Settings.InterfaceMotionMode="Light";
                window.ViewModel.Settings.SimplifyInterfaceAnimations=false;
                window.ViewModel.LoadFile(args[1]); await Task.Delay(2200);
                if (!(window.ViewModel.TimeSource is ScriptPlayer.Shared.MpvTimeSource) || window.ViewModel.PlaybackState.Status != ScriptPlayer.Playback.PlaybackStatus.Ready || !window.ViewModel.TimeSource.IsPlaying || window.ViewModel.TimeSource.Duration.TotalSeconds < 1)
                    throw new Exception("Native hover fixture must be playing through real libmpv");
                ((System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("chromeTimer",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Stop();
                Call("NavigateTo","player"); window.UpdateLayout();
                var root=(FrameworkElement)window.FindName("ShellRoot");
                var transport=(FrameworkElement)window.FindName("PlayerControls");
                var center=root.PointToScreen(new Point(root.ActualWidth/2,root.ActualHeight/2));
                if (!SetCursorPos((int)center.X,(int)center.Y)) throw new Exception("SetCursorPos failed before trigger");
                await Task.Delay(80);
                var now=DateTime.UtcNow;
                Call("ProcessPlaybackPointer",new Point(root.ActualWidth/2,root.ActualHeight/2),true,now);
                Call("EvaluatePlaybackChrome",now.AddSeconds(3));
                Call("ProcessPlaybackPointer",new Point(root.ActualWidth/2,root.ActualHeight-2),true,now.AddSeconds(3));
                window.UpdateLayout();
                await Task.Delay(20);
                if (transport.Opacity < .8) throw new Exception("Animated native hit surface faded below 80%: " + transport.Opacity);
                var target=transport.PointToScreen(new Point(transport.ActualWidth/2,transport.ActualHeight/2));
                if (!SetCursorPos((int)Math.Round(target.X),(int)Math.Round(target.Y))) throw new Exception("SetCursorPos failed during trigger");
                await Task.Delay(400);
                if (!transport.IsMouseOver) throw new Exception("Physical hover did not reach the transport");
                File.AppendAllText(args[2],"PASS: Native transport receives physical hover on its first animated appearance\nFailures: 0\n");
            }
            catch(Exception ex) {File.AppendAllText(args[2],"ERROR: "+ex+"\nFailures: 1\n");result=1;}
            finally {deadline.Stop(); window.Close(); if(!SetCursorPos(original.X,original.Y)) {File.AppendAllText(args[2],"ERROR: pointer restore failed\n");result=1;} app.Shutdown(result);}
        }));
        app.Run(window); return result;
    }
}
