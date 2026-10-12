using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using Newtonsoft.Json;
using ScriptPlayer;
using ScriptPlayer.ViewModels;

internal static class PlaybackExitTests
{
    static MainWindow window;
    static string log, recordPath;
    static int failures;
    static void Check(bool condition, string text)
    {
        File.AppendAllText(log, (condition ? "PASS: " : "FAIL: ") + text + "\n");
        if (!condition) failures++;
    }
    static object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window,args);
    static Dictionary<string,TimeSpan> Records() => File.Exists(recordPath) ? JsonConvert.DeserializeObject<Dictionary<string,TimeSpan>>(File.ReadAllText(recordPath)) : new Dictionary<string,TimeSpan>();
    static async Task Load(string path)
    {
        window.ViewModel.LoadFile(path);
        await Task.Delay(1100);
        Check(window.ViewModel.TimeSource.Duration.TotalSeconds > 5, "MPV loads fixture");
    }
    static async Task PauseAt(double seconds)
    {
        if (!window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlayback();
        await Task.Delay(120);
        window.ViewModel.TogglePlayback();
        await Task.Delay(120);
        window.ViewModel.Seek(TimeSpan.FromSeconds(seconds),0);
        window.ViewModel.Seek(TimeSpan.FromSeconds(seconds),2);
        await Task.Delay(180);
        Check(!window.ViewModel.TimeSource.IsPlaying, "Explicit pause holds before exit");
    }
    [STAThread]
    static void Main(string[] args)
    {
        int phase=int.Parse(args[2]); log=args[3];
        recordPath=Path.Combine(Environment.GetEnvironmentVariable("APPDATA"),"ScriptPlayer","PausedExitPoints.json");
        File.AppendAllText(log,"Phase "+phase+"\n");
        var before=File.Exists(recordPath)?File.ReadAllText(recordPath):null;
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        ScriptPlayer.Localization.ChineseLocalization.Initialize();
        string id=Guid.NewGuid().ToString("N"); InstanceHandler.Startup("ExitTest-"+id,"ExitPipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary=XDocument.Load(args[4]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(dictionary.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        window=new MainWindow();window.InitializeComponent();app.MainWindow=window;
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                await Task.Delay(500);
                window.ViewModel.Settings.WaitForDevicesToLoad=false;
                window.ViewModel.Playlist.AddEntries(false,new[]{args[0],args[1]});
                await Task.Delay(300);
                await Load(phase==2 || phase==4 ? args[1] : args[0]);
                if(phase==1 || phase==5)
                {
                    await PauseAt(3);
                    Check(!File.Exists(recordPath) || File.ReadAllText(recordPath)==before,"Pausing alone does not write exit record");
                    var next=window.ViewModel.Playlist.Entries.First(x=>x.Fullname==args[1]);
                    Call("ProcessPlaybackPointer",new Point(2,200),true,DateTime.UtcNow);window.UpdateLayout();
                    var list=(ListBox)window.FindName("PlaybackMediaList");list.ScrollIntoView(next);window.UpdateLayout();
                    var container=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(next);
                    var button=Descendants(container).OfType<Button>().First();button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(900);
                    Check(window.ViewModel.LoadedMedia==args[1],"Clicking compact tile switches video");
                    Check(window.ViewModel.PausedExitMedia==null,"Switching clears explicit pause marker");
                    Check(!File.Exists(recordPath) || File.ReadAllText(recordPath)==before,"Switching video does not write exit record");
                    if(phase==5) window.ViewModel.TimeSource.Pause(); // internal engine pause must not qualify
                }
                else if(phase==2) await PauseAt(4);
                else if(phase==3)
                {
                    Check(Records().ContainsKey(args[1]),"Second video's record survives restart");
                    var second=window.ViewModel.Playlist.Entries.First(x=>x.Fullname==args[1]);
                    Check(second.PausedExitPosition.HasValue && Math.Abs(second.PausedExitPosition.Value.TotalSeconds-4)<0.5,"Saved exit time appears on playlist entry");
                    Check(second.PausedExitDisplay=="上次退出 00:00:04","Exit label formats timestamp");
                    Call("ProcessPlaybackPointer",new Point(2,200),true,DateTime.UtcNow);window.UpdateLayout();
                    Check(((FrameworkElement)window.FindName("PlaybackListPanel")).IsVisible && !((FrameworkElement)window.FindName("BrowseNavigationLinks")).IsVisible,"Playback rail shows compact list instead of full navigation");
                    var search=(TextBox)window.FindName("ShellSearch");search.Text=Path.GetFileNameWithoutExtension(args[1]);window.UpdateLayout();
                    Check(((ListBox)window.FindName("PlaybackMediaList")).Items.Count==1 && ((FrameworkElement)window.FindName("GridVideo")).IsVisible,"Sidebar search filters list without leaving playback");search.Text="";
                    Call("ProcessPlaybackPointer",new Point(30,((Grid)window.FindName("ShellRoot")).ActualHeight-60),false,DateTime.UtcNow.AddSeconds(5));
                    Call("EvaluatePlaybackChrome",DateTime.UtcNow.AddSeconds(5));
                    Check(((FrameworkElement)window.FindName("NavigationRail")).IsVisible,"Hovering bottom playlist rows keeps list visible");
                    await PauseAt(2);window.UpdateLayout();
                    var rail=(FrameworkElement)window.FindName("NavigationRail");
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)rail.ActualWidth,(int)rail.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
                    bitmap.Render(rail);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using(var stream=File.Create(Path.Combine(Path.GetDirectoryName(log),"v4-playback-list.png")))png.Save(stream);
                }
                else if(phase==4)
                {
                    if(!window.ViewModel.TimeSource.IsPlaying)window.ViewModel.TogglePlayback();
                    await Task.Delay(200);
                    Check(window.ViewModel.TimeSource.IsPlaying,"Exit while video is still playing");
                }
                else if(phase==6)
                {
                    await PauseAt(3);
                    window.ViewModel.TimeSource.Play();await Task.Delay(200);
                    window.ViewModel.TimeSource.Pause();await Task.Delay(100);
                    Check(window.ViewModel.PausedExitMedia==null,"Engine resume clears old explicit pause marker");
                }
                window.Close();
                var after=Records();
                if(phase==1)Check(!File.Exists(recordPath),"Close after switching creates no exit record");
                if(phase==2)Check(after.Count==1 && after.ContainsKey(args[1]) && Math.Abs(after[args[1]].TotalSeconds-4)<0.5,"Real paused window close writes only current video");
                if(phase==3)Check(after.Count==2 && Math.Abs(after[args[0]].TotalSeconds-2)<0.5 && Math.Abs(after[args[1]].TotalSeconds-4)<0.5,"Different videos retain independent exit points");
                if(phase>=4)Check(File.ReadAllText(recordPath)==before,"Non-user-pause exit leaves all previous records unchanged");
            }
            catch(Exception ex){failures++;File.AppendAllText(log,"ERROR: "+ex+"\n");try{window.Close();}catch{}}
            finally{File.AppendAllText(log,"Phase failures: "+failures+"\n");app.Shutdown(failures==0?0:1);}
        }));
        app.Run(window);
        Environment.ExitCode=failures==0?0:1;
    }
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
}
