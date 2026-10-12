using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using ScriptPlayer;
using ScriptPlayer.ViewModels;

// Run one B01 assertion per fresh process, or permutations in one window.
// The final argument is center/transport: only the physical pointer location changes.
internal static class PlaybackChromeIsolationTests
{
    private static MainWindow window;
    private static string log;
    private static int failures;
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint p);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [STAThread] private static void Main(string[] args)
    {
        log = args[1]; File.WriteAllText(log, "B01 isolated " + args[3] + " pointer=" + args[4] + "\n");
        CursorPoint saved; bool haveSaved = GetCursorPos(out saved);
        try
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            ScriptPlayer.Localization.ChineseLocalization.Initialize();
            string id = Guid.NewGuid().ToString("N");
            InstanceHandler.Startup("Chrome-Test-" + id, "Chrome-Pipe-" + id);
            XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var dictionary = XDocument.Load(args[2]).Root.Element(p + "Application.Resources").Element(p + "ResourceDictionary");
            app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString(), new ParserContext { BaseUri = new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml") });
            window = new MainWindow(); window.InitializeComponent(); app.MainWindow = window;
            window.Loaded += (s, e) => app.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    window.Width = 1280; window.Height = 820;
                    await Task.Delay(500);
                    window.ViewModel.LoadFile(args[0]);
                    await Wait(() => window.ViewModel.TimeSource != null && window.ViewModel.TimeSource.Duration.TotalSeconds > 5, "media ready");
                    if (!window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlaybackCommand.Execute(null);
                    await Wait(() => window.ViewModel.PlaybackState.IsPlaying && window.ViewModel.TimeSource.IsPlaying, "playing projection");
                    ((System.Windows.Threading.DispatcherTimer)Field("chromeTimer")).Stop();
                    foreach (string test in args[3].Split(',')) await RunCase(test, args[4]);
                }
                catch (Exception ex) { failures++; File.AppendAllText(log, "ERROR: " + ex + "\n"); }
                finally { File.AppendAllText(log, "Failures: " + failures + "\n"); window.Close(); app.Shutdown(); }
            }));
            app.Run(window);
        }
        catch (Exception ex) { failures++; File.AppendAllText(log, "ERROR: " + ex + "\n"); }
        finally { if (haveSaved && !SetCursorPos(saved.X, saved.Y)) { failures++; File.AppendAllText(log, "ERROR: cursor restore failed\n"); } }
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static async Task RunCase(string test, string pointerMode)
    {
        Call("NavigateTo", "player"); window.UpdateLayout(); window.Activate();
        await Wait(() => window.IsActive || Window.GetWindow(Element("PlayerControls")).IsActive, "active test window");
        var root = Element("ShellRoot"); var transport = Element("PlayerControls");
        var popup = (ScriptPlayer.Controls.InlineFlyout)window.FindName("QuickSettingsPopup");
        if (test == "heat")
        {
            string script = Path.Combine(Path.GetDirectoryName(log), "fixture.funscript");
            File.WriteAllText(script, "{\"actions\":[{\"at\":0,\"pos\":0},{\"at\":1000,\"pos\":100},{\"at\":10000,\"pos\":0}]}");
            window.ViewModel.LoadFile(script); window.ViewModel.Settings.ShowHeatMap = true;
            await Wait(() => !string.IsNullOrEmpty(window.ViewModel.LoadedScript) && ((ScriptPlayer.Controls.PlaybackTimelineView)window.FindName("PlaybackTimeline")).PersistentHeight > 0, "script projection");
            if (!window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlaybackCommand.Execute(null);
            await Wait(() => window.ViewModel.PlaybackState.IsPlaying && !window.ViewModel.PlaybackState.IsSeeking, "script playing");
        }
        Call("RevealPlaybackChrome"); window.UpdateLayout();
        Point target = pointerMode == "transport" ? transport.PointToScreen(new Point(transport.ActualWidth / 2, transport.ActualHeight / 2))
            : root.PointToScreen(new Point(root.ActualWidth / 2, root.ActualHeight / 2));
        if (!SetCursorPos((int)Math.Round(target.X), (int)Math.Round(target.Y))) throw new InvalidOperationException("SetCursorPos failed");
        await Wait(() => pointerMode == "transport" ? transport.IsMouseOver : !transport.IsMouseOver && !Element("PlaybackTimeline").IsMouseOver, "physical pointer precondition");
        Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight / 2), true, DateTime.UtcNow);
        var now = DateTime.UtcNow;
        if (test == "edge") Call("ProcessPlaybackPointer", new Point(root.ActualWidth - 2, root.ActualHeight / 2), true, now);
        typeof(MainWindow).GetField("lastPlaybackActivity", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, now.AddSeconds(-5));
        Trace(test + "-before"); Call("EvaluatePlaybackChrome", now); Trace(test + "-after-hide");
        if (test == "right")
        {
            Call("ProcessPlaybackPointer", new Point(root.ActualWidth - 2, root.ActualHeight / 2), true, now);
            Check(popup.IsOpen && !Element("NavigationRail").IsVisible && !transport.IsVisible, "Right edge reveals only quick settings");
        }
        else if (test == "edge") Check(popup.IsOpen && !transport.IsVisible, "Edge settings do not pin an unrelated transport open");
        else if (test == "heat")
        {
            window.UpdateLayout();
            var heat = ((ScriptPlayer.Controls.PlaybackTimelineView)window.FindName("PlaybackTimeline")).HeatSeek;
            Call("ProcessPlaybackPointer", heat.TranslatePoint(new Point(heat.ActualWidth / 2, heat.ActualHeight / 2), root), true, now);
            Check(!transport.IsVisible, "Hovering persistent heatmap does not move it away from the pointer");
            if (pointerMode == "center")
            {
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight - 2), true, now);
                Check(transport.IsVisible, "Actual bottom edge below persistent heatmap reveals transport");
            }
        }
        else throw new ArgumentException("unknown case " + test);
        popup.IsOpen = false;
    }
    private static async Task Wait(Func<bool> condition, string name)
    {
        var deadline = DateTime.UtcNow.AddSeconds(6);
        do { window.UpdateLayout(); if (condition()) return; await Task.Delay(40); } while (DateTime.UtcNow < deadline);
        throw new TimeoutException(name);
    }
    private static object Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    private static FrameworkElement Element(string name) => (FrameworkElement)window.FindName(name);
    private static void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    private static void Check(bool success, string name)
    {
        File.AppendAllText(log, (success ? "PASS: " : "FAIL: ") + name + "\n");
        if (!success) { failures++; File.AppendAllText(log, new System.Diagnostics.StackTrace(true).ToString()); }
    }
    private static void Trace(string tag)
    {
        CursorPoint p; GetCursorPos(out p);
        File.AppendAllText(log, tag + " cursor=" + p.X + "," + p.Y + " hidden=" + Field("playbackChromeHidden")
            + " transportHover=" + Element("PlayerControls").IsMouseOver + " timelineHover=" + Element("PlaybackTimeline").IsMouseOver
            + " playing=" + window.ViewModel.PlaybackState.IsPlaying + " native=" + window.ViewModel.TimeSource.IsPlaying
            + " seeking=" + window.ViewModel.PlaybackState.IsSeeking + " captured=" + System.Windows.Input.Mouse.Captured
            + " inline=" + InlineDialogService.IsOpen + " keyboard=" + Field("keyboardChromeFocus") + " edge=" + Field("edgeQuickSettings")
            + " active=" + window.IsActive + " overlayActive=" + Window.GetWindow(Element("PlayerControls")).IsActive + "\n");
    }
}
