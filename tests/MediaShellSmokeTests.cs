using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using ScriptPlayer;
using ScriptPlayer.Dialogs;
using ScriptPlayer.ViewModels;

internal static class MediaShellSmokeTests
{
    private static MainWindow window;
    private static string log;
    private static int failures;
    [StructLayout(LayoutKind.Sequential)]
    private struct CursorPoint { public int X; public int Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);
    [STAThread]
    private static void Main(string[] args)
    {
        CursorPoint originalCursor;
        bool haveCursor = GetCursorPos(out originalCursor);
        try { Run(args); }
        catch (Exception ex)
        {
            while (ex != null) { File.AppendAllText(args[1], ex.GetType().FullName + ": " + ex.Message + "\n" + ex.StackTrace + "\n"); ex = ex.InnerException; }
            Environment.ExitCode = 1;
        }
        finally
        {
            if (haveCursor && !SetCursorPos(originalCursor.X, originalCursor.Y))
            {
                File.AppendAllText(args[1], "ERROR: Original pointer could not be restored\n");
                Environment.ExitCode = 1;
            }
        }
    }
    private static void Run(string[] args)
    {
        log = args[1];
        File.WriteAllText(log, "Media shell integration tests\n");
        // Independent test application: it never sends commands to the user's running player.
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        ScriptPlayer.Localization.ChineseLocalization.Initialize();
        string instanceId = Guid.NewGuid().ToString("N");
        InstanceHandler.Startup("ScriptPlayer-Test-" + instanceId, "ScriptPlayer-TestPipe-" + instanceId);
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = XDocument.Load(args[2]).Root.Element(p + "Application.Resources").Element(p + "ResourceDictionary");
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString(), new ParserContext { BaseUri = new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml") });
        window = new MainWindow();
        window.InitializeComponent();
        app.MainWindow = window;
        window.Loaded += (s,e) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Task.Delay(500);
                window = (MainWindow)app.MainWindow;
                Check(((TextBlock)window.FindName("PageTitle")).Text == "主页" && ((FrameworkElement)window.FindName("BrowsePage")).IsVisible, "Startup opens home, not the idle native video surface");
                var versionButton = (Button)window.FindName("VersionButton");
                Check((string)versionButton.Content == "v1.0.12.6" && versionButton.IsVisible, "Caption version entry uses the unified version");
                Window aboutDialog = null;
                Action<Window> captureAbout = dialog => aboutDialog = dialog;
                InlineDialogService.Opened += captureAbout;
                try
                {
                    versionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(100);
                    Check(aboutDialog != null && !aboutDialog.IsVisible && InlineDialogService.IsOpen, "About opens inside player without a new window");
                    var aboutTexts = FindVisualChildren<TextBlock>((DependencyObject)window.FindName("ShellRoot")).Select(t => t.Text).ToArray();
                    Check(aboutTexts.Any(t => t == "Version v1.0.12.6") && !aboutTexts.Any(t => t.Contains("0.9.0")), "About has no obsolete beta version");
                    Check(aboutTexts.Any(t => t.Contains("让播放回归简单")) && aboutTexts.Any(t => t.Contains("非官方社区衍生版本")), "About includes author text and project identity");
                    Check(FindVisualChildren<TextBlock>((DependencyObject)window.FindName("ShellRoot")).Any(t => t.Inlines.OfType<System.Windows.Documents.Hyperlink>().Any()), "About includes actionable project source link");
                    Check(FindVisualChildren<TextBlock>((DependencyObject)window.FindName("ShellRoot")).Any(t => t.Text == "ScriptPlayer" && t.FontSize == 28), "About preserves heading typography");
                    Check(FindVisualChildren<ScrollViewer>((DependencyObject)window.FindName("ShellRoot")).Any(t => t.Content is StackPanel && ((StackPanel)t.Content).Children.OfType<TextBlock>().Any(label => label.Text == "Version v1.0.12.6") && t.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && t.VerticalScrollBarVisibility == ScrollBarVisibility.Auto), "About prose uses vertical scrolling and wrapping");
                    SavePreview(window, "version-about.png");
                    InlineDialogService.Complete(aboutDialog, false);
                    await Task.Delay(100);
                    Check(!InlineDialogService.IsOpen, "Closing about restores inline session state");
                }
                finally { InlineDialogService.Opened -= captureAbout; if (aboutDialog != null && InlineDialogService.IsOpen) InlineDialogService.Complete(aboutDialog, false); }
                Check(window.ViewModel.PlaybackMode == PlaybackMode.Mpv, "Bundled MPV is the default player");
                Check(window.ViewModel.Settings.FfmpegPath == Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ffmpeg.exe"), "FFmpeg resolves relative to the executable");
                Check(window.ViewModel.Settings.FfmpegDisplayPath.Contains("ffmpeg.exe")
                    && !window.ViewModel.Settings.FfmpegDisplayPath.Contains(AppDomain.CurrentDomain.BaseDirectory),
                    "Settings show bundled FFmpeg without exposing its installation directory");
                foreach (string token in new[] { "MediaBackgroundBrush", "MediaSurfaceBrush", "MediaBorderBrush", "MediaTextBrush", "MediaMutedBrush", "MediaAccentBrush" })
                    Check(window.TryFindResource(token) is System.Windows.Media.SolidColorBrush, "Shared visual token is available: " + token);
                Check((window.FindName("FavoriteShelf") as FrameworkElement)?.Visibility == Visibility.Collapsed, "Home hides an empty favorite shelf");
                Call("NavigateTo", "home");
                Check(((TextBlock)window.FindName("PageTitle")).Text == "主页", "Home navigation");
                Call("NavigateTo", "settings");
                Check(((FrameworkElement)window.FindName("GridSettings")).IsVisible, "Settings page visible");
                Check(window.TryFindResource("MediaFontFamily") is System.Windows.Media.FontFamily, "Shared interface font token is available");
                bool heat = window.ViewModel.Settings.ShowHeatMap;
                window.ViewModel.Settings.ShowHeatMap = !heat;
                await Task.Delay(1000);
                string settingsFile = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"),"ScriptPlayer","Settings.xml");
                Check(File.Exists(settingsFile), "Common settings automatically saved");
                Call("OpenAdvancedSettings", "Remember");
                var editor = (SettingsDialog)typeof(MainWindow).GetField("unifiedSettingsEditor", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                var sections = (Dictionary<string, FrameworkElement>)typeof(MainWindow).GetField("settingsSections", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                Check(sections.Count == 36 && sections.ContainsKey("Appearance") && sections.ContainsKey("Animations") && window.FindName("AdvancedSettingsHost") == null, "All 27 editors plus overview, connection, tools and appearance pages are accessible");
                Check(FindVisualChildren<PasswordBox>(sections["The Handy"]).Any(box => box.Name == "txtHandyConnectionKey"),
                    "Unified Handy settings use a masked connection-key input");
                Check(FindVisualChildren<TextBox>(sections["FFmpeg"]).Any(box => box.IsReadOnly
                    && box.Text.Contains("ffmpeg.exe") && !box.Text.Contains(AppDomain.CurrentDomain.BaseDirectory)),
                    "Unified FFmpeg editor displays bundled status without a private address");
                window.Width = 920; window.Height = 620;
                var navigation=(Dictionary<string,RadioButton>)typeof(MainWindow).GetField("settingsNavigation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                var search = (TextBox)window.FindName("SettingsSearch");
                search.Text = "延迟"; window.UpdateLayout();
                Check(navigation["Timings"].Visibility == Visibility.Visible && navigation.Values.Count(x => x.Visibility == Visibility.Visible) < sections.Count, "Settings search finds delays and filters unrelated categories");
                search.Text = "fFmPeG";
                Check(navigation["FFmpeg"].Visibility == Visibility.Visible, "Settings search matches English names regardless of case");
                search.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, System.Windows.Input.Key.Enter) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent });
                Check(ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content, sections["FFmpeg"]), "Enter opens first matching settings page");
                search.Text = "不存在的设置-123456";
                Check(navigation.Values.All(x => x.Visibility == Visibility.Collapsed) && ((TextBlock)window.FindName("SettingsSearchStatus")).Text.Contains("没有匹配"), "Empty settings search has a clear explanation and no unrelated results");
                Call("ClearSettingsSearch_Click", null, new RoutedEventArgs());
                Check(navigation.Values.All(x => x.Visibility == Visibility.Visible), "Clearing search restores every settings entry");
                window.UpdateLayout(); Call("UpdateResponsiveLayout"); window.UpdateLayout();
                var compactSettings = (ComboBox)window.FindName("CompactSettingsNavigator");
                Check(compactSettings.IsVisible && !((FrameworkElement)window.FindName("SettingsNavigationScroll")).IsVisible, "Narrow settings use a full-width editor and category selector");
                compactSettings.SelectedValue = "Range";
                Check(ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content, sections["Range"]), "Compact category selector opens the live editor");
                window.UpdateLayout();
                var categoryLabels = FindVisualChildren<TextBlock>(compactSettings).Select(x => x.Text).ToArray();
                Check(categoryLabels.Any(x => x.Contains("行程范围")) && !categoryLabels.Any(x => x.StartsWith("[Range,")), "Compact selector displays the readable category title without internal keys");
                foreach (var item in sections)
                {
                    navigation[item.Key].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content,item.Value), "Settings navigation button opens correct page: " + item.Key);
                    window.UpdateLayout();
                    var scroll = (ScrollViewer)window.FindName("SettingsContentScroll");
                    Check(item.Value.IsVisible && scroll.ViewportHeight > 80 && !double.IsInfinity(scroll.ExtentHeight), "Settings page fits bounded viewport: " + item.Key);
                    Check(scroll.ExtentWidth <= scroll.ViewportWidth + 2, "Settings page fits width without clipping: " + item.Key);
                    var switches = FindVisualChildren<CheckBox>(item.Value).ToArray();
                    Check(switches.All(check => { check.ApplyTemplate(); return check.Template.FindName("SwitchTrack", check) is FrameworkElement
                        && check.Template.FindName("SwitchKnob", check) is FrameworkElement; }), "Every realized setting boolean uses a switch: " + item.Key);
                    var font = window.TryFindResource("MediaFontFamily") as System.Windows.Media.FontFamily;
                    Check(font != null && switches.All(check => check.FontFamily.Source == font.Source), "Settings switches share the interface font: " + item.Key);
                    scroll.ScrollToBottom(); window.UpdateLayout();
                    Check(scroll.ScrollableHeight == 0 || Math.Abs(scroll.VerticalOffset - scroll.ScrollableHeight) < 1, "Settings page bottom is reachable: " + item.Key);
                }
                foreach (string page in new[] { "Overview", "Paths", "Players", "Input Mappings" })
                {
                    Call("SelectSettingsPage", page); window.UpdateLayout(); await Task.Delay(120); window.UpdateLayout();
                    SavePreview(window, "settings-" + page.Replace(" ", "-") + ".png");
                }
                window.Width = 1280; window.Height = 820;
                await CheckSwitchBehavior();
                await CheckPathTableSwitch(editor, sections["Paths"]);
                Call("SelectSettingsPage", "Overview"); window.UpdateLayout();
                ((ScrollViewer)window.FindName("SettingsContentScroll")).ScrollToTop(); window.UpdateLayout();
                SavePreview(window, "v11-settings-overview.png");
                window.ViewModel.Settings.ShowHeatMap = heat;
                Check(ReferenceEquals(editor.Settings,window.ViewModel.Settings), "Unified and quick settings share one live model");
                editor.Settings.RememberVolume = !window.ViewModel.Settings.RememberVolume;
                await Task.Delay(900);
                Check(File.ReadAllText(settingsFile).Contains("<RememberVolume>"+editor.Settings.RememberVolume.ToString().ToLowerInvariant()+"</RememberVolume>"), "Unified settings automatically persist without Apply");
                await CheckInlineEditors();
                Call("NavigateTo", "home");
                window.ViewModel.LoadFile(args[0]);
                await Task.Delay(3000);
                Check(window.ViewModel.TimeSource.Duration.TotalSeconds > 5, "Bundled MPV loads a real video");
                Check(string.IsNullOrEmpty(window.ViewModel.LoadedScript), "Regression fixture is a video without a paired script");
                if (!window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlaybackCommand.Execute(null);
                var before = window.ViewModel.TimeSource.Progress;
                await Task.Delay(900);
                Check(window.ViewModel.TimeSource.Progress > before, "Playback advances");
                window.ViewModel.TogglePlaybackCommand.Execute(null);
                await Task.Delay(200);
                before = window.ViewModel.TimeSource.Progress;
                await Task.Delay(500);
                Check(Math.Abs((window.ViewModel.TimeSource.Progress-before).TotalSeconds)<0.3, "Pause holds the play position");
                Check(!window.ViewModel.TimeSource.IsPlaying, "Pause command works in MPV without a script");
                window.ViewModel.TogglePlaybackCommand.Execute(null);
                await Task.Delay(400);
                Check(window.ViewModel.TimeSource.IsPlaying, "Play command resumes MPV without reopening the file");
                Call("RevealPlaybackChrome");window.UpdateLayout();
                var gear=(Button)window.FindName("QuickSettingsButton");
                var overlayWindow=Window.GetWindow(gear);
                if(overlayWindow!=window) overlayWindow.Activate();
                gear.Focus();await Task.Delay(200);
                Check(gear.IsVisible, "Transport stays visible when its native overlay receives focus");
                gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(150);
                Check(((ScriptPlayer.Controls.InlineFlyout)window.FindName("QuickSettingsPopup")).IsOpen && ((FrameworkElement)window.FindName("QuickSettingsScroller")).IsVisible, "Settings gear opens an interactive in-player panel after focus transfer");
                for(int cycle=0;cycle<12;cycle++){Call("PlaybackChromeTick",null,EventArgs.Empty);await Task.Delay(100);}
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible && gear.IsVisible, "Transport and gear remain stable during repeated native focus checks");
                Call("CloseQuickSettings_Click",null,new RoutedEventArgs());window.Activate();

                ((System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("chromeTimer", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window)).Stop();
                Call("NavigateTo", "player");
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible, "Entering player immediately hides navigation");
                window.UpdateLayout();
                var root = (Grid)window.FindName("ShellRoot");
                var now = DateTime.UtcNow;
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight / 2), true, now);
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Movement in video center reveals no controls");
                Call("ProcessPlaybackPointer", new Point(2, root.ActualHeight / 2), true, now);
                Check(((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Left edge reveals only navigation");
                Call("EvaluatePlaybackChrome", now.AddSeconds(2));
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible, "Navigation hides after leaving its region");
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight - 2), true, now);
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible && ((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Bottom edge reveals only transport");
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight - 10), false, now.AddSeconds(1));
                Call("EvaluatePlaybackChrome", now.AddSeconds(1.1));
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Transport remains visible while pointer rests on controls");
                await PlacePointer((FrameworkElement)window.FindName("PlayerControls"));
                Call("EvaluatePlaybackChrome", now.AddSeconds(3));
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Actual transport hover holds controls after the hide delay");
                await PrepareIdleChrome();
                Call("EvaluatePlaybackChrome", now.AddSeconds(3));
                Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Right-edge scenario starts with idle transport hidden");
                TraceChrome("B01-right-before");
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth - 2, root.ActualHeight / 2), true, now);
                var edgePopup = (ScriptPlayer.Controls.InlineFlyout)window.FindName("QuickSettingsPopup");
                TraceChrome("B01-right-after");
                Check(edgePopup.IsOpen && !((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Right edge reveals only quick settings");
                Call("RevealPlaybackChrome");
                typeof(MainWindow).GetField("lastPlaybackActivity", BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window, now.AddSeconds(-5));
                await PrepareIdleChrome();
                Call("EvaluatePlaybackChrome", now);
                TraceChrome("B01-edge-pin-after");
                Check(edgePopup.IsOpen && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Edge settings do not pin an unrelated transport open");
                await Task.Delay(100);
                var quickSlider = (Slider)window.FindName("QuickScriptDelay");
                File.AppendAllText(log, "DRAG popup="+edgePopup.IsOpen+" visible="+quickSlider.IsVisible+" enabled="+quickSlider.IsEnabled+" active="+window.IsActive+" source="+(PresentationSource.FromVisual(quickSlider)!=null)+" captured="+System.Windows.Input.Mouse.Captured+"\n");
                Check(quickSlider.CaptureMouse(), "Quick slider can capture pointer for dragging");
                Call("EvaluatePlaybackChrome", now.AddSeconds(4));
                Check(edgePopup.IsOpen, "Hover quick settings stay open throughout slider dragging");
                quickSlider.ReleaseMouseCapture();
                CursorPoint savedCursor;
                bool savedCursorAvailable = GetCursorPos(out savedCursor);
                Check(savedCursorAvailable, "Hover close test saves the original pointer position");
                try
                {
                    if (!savedCursorAvailable) throw new InvalidOperationException("Cannot save pointer position for hover-close test.");
                    var videoCenter = root.PointToScreen(new Point(root.ActualWidth / 2, root.ActualHeight / 2));
                    Check(SetCursorPos((int)Math.Round(videoCenter.X), (int)Math.Round(videoCenter.Y)), "Hover close test moves the actual pointer into the video center");
                    await Task.Delay(80);
                    Check(!edgePopup.IsMouseOver, "Hover close test has an actual pointer outside quick settings");
                    Call("EvaluatePlaybackChrome", now.AddSeconds(4));
                    Check(!edgePopup.IsOpen, "Hover quick settings close after pointer leaves");
                }
                finally
                {
                    if (savedCursorAvailable)
                        Check(SetCursorPos(savedCursor.X, savedCursor.Y), "Hover close test restores the original pointer position");
                }
                var autoHideSwitch = (CheckBox)window.FindName("AutoHideControlsSwitch");
                autoHideSwitch.IsChecked = false;
                Call("AutoHideControls_Click", autoHideSwitch, new RoutedEventArgs());
                Call("EvaluatePlaybackChrome", now.AddSeconds(5));
                Check(((FrameworkElement)window.FindName("NavigationRail")).IsVisible && ((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Disabling auto hide keeps both controls visible");
                autoHideSwitch.IsChecked = true;
                Call("AutoHideControls_Click", autoHideSwitch, new RoutedEventArgs());
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Enabling auto hide during playback immediately hides controls");
                Call("SetFullscreen", true, true);
                window.UpdateLayout();
                Call("ProcessPlaybackPointer", new Point(2, root.ActualHeight / 2), true, DateTime.UtcNow);
                Check(((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Fullscreen left edge reveals navigation independently");
                Call("SetFullscreen", false, true);
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Playing idle video hides both navigation and transport");
                Check(((Grid)window.FindName("ShellRoot")).ColumnDefinitions[0].ActualWidth == 0 || ((Grid)window.FindName("ShellRoot")).ColumnDefinitions[0].Width.Value == 0, "Hidden navigation releases its space for video");
                Call("RevealPlaybackChrome");
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Pointer activity reveals controls");
                var popup = (ScriptPlayer.Controls.InlineFlyout)window.FindName("QuickSettingsPopup");
                Call("QuickSettings_Click", window.FindName("QuickSettingsButton"), new RoutedEventArgs());
                await Task.Delay(100);
                ((Slider)window.FindName("QuickScriptDelay")).Value = 250;
                Check(window.ViewModel.Settings.ScriptDelay.TotalMilliseconds == 250, "Quick script delay edits the live setting");
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Quick settings keep the playback controls visible");
                popup.IsOpen = false;
                window.ViewModel.TogglePlaybackCommand.Execute(null);
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Paused playback does not auto-hide controls");
                window.ViewModel.Seek(TimeSpan.FromSeconds(4),2);
                await Task.Delay(600);
                Check(Math.Abs(window.ViewModel.TimeSource.Progress.TotalSeconds-4)<1, "Seeking works");
                string fixture = Path.Combine(Path.GetDirectoryName(log), "heatmap-preview-fixture.funscript");
                File.WriteAllText(fixture, "{\"version\":\"1.0\",\"actions\":[{\"at\":0,\"pos\":0},{\"at\":1000,\"pos\":100},{\"at\":2000,\"pos\":0},{\"at\":2500,\"pos\":100},{\"at\":2800,\"pos\":0},{\"at\":3100,\"pos\":100},{\"at\":5000,\"pos\":0},{\"at\":10000,\"pos\":100}]}");
                window.ViewModel.LoadFile(fixture);
                window.ViewModel.Settings.ShowHeatMap = true;
                await Task.Delay(500);
                Call("NavigateTo", "player");
                if (!window.ViewModel.TimeSource.IsPlaying) window.ViewModel.TogglePlaybackCommand.Execute(null);
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                window.UpdateLayout();
                var strip = (FrameworkElement)window.FindName("PersistentHeatMap");
                var dock = (StackPanel)window.FindName("PlaybackDock");
                var transport = (Grid)window.FindName("PlayerControls");
                Check(strip.IsVisible && !transport.IsVisible, "Heatmap remains visible when playback controls hide");
                Check(dock.Parent == window.FindName("VideoOverlayLayer") && ((Grid)window.FindName("ShellRoot")).RowDefinitions[2].Height.Value == 0, "Transport overlays native video without reserving a bottom row");
                double hiddenHeight = dock.ActualHeight;
                Call("RevealPlaybackChrome"); window.UpdateLayout();
                Check(dock.ActualHeight > hiddenHeight && strip.TranslatePoint(new Point(), dock).Y < transport.TranslatePoint(new Point(), dock).Y, "Heatmap sits above visible transport and settles at bottom when hidden");
                var gradient = transport.Background as System.Windows.Media.LinearGradientBrush;
                Check(gradient != null && gradient.GradientStops.All(g => g.Color.A < 255), "Transport background is translucent");
                await Task.Delay(120);
                SavePreview(dock, "transparent-playback-controls.png");
                Check(((TextBlock)window.FindName("TotalPlaybackTime")).Text != "00:00:00", "Transport displays loaded media duration");
                var heatSeek = (ScriptPlayer.Shared.SeekBar)window.FindName("HeatMapPreview");
                typeof(ScriptPlayer.Shared.SeekBar).GetMethod("OnSeek", BindingFlags.Instance|BindingFlags.NonPublic).Invoke(heatSeek, new object[] { 0.4, TimeSpan.FromSeconds(4), 2 });
                await Task.Delay(150);
                Check(Math.Abs(window.ViewModel.TimeSource.Progress.TotalSeconds - 4) < 1, "Persistent heatmap can seek real MPV playback");
                window.ViewModel.Settings.ShowHeatMap = false; window.UpdateLayout();
                Check(!strip.IsVisible, "Heatmap visibility setting is respected");
                window.ViewModel.Settings.ShowHeatMap = true;
                window.UpdateLayout();
                await PrepareIdleChrome();
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(5));
                Check(!transport.IsVisible, "Persistent-strip scenario starts with idle transport hidden");
                Call("ProcessPlaybackPointer", heatSeek.TranslatePoint(new Point(heatSeek.ActualWidth / 2, heatSeek.ActualHeight / 2), root), true, DateTime.UtcNow);
                TraceChrome("B01-heat-after");
                Check(!transport.IsVisible, "Hovering persistent heatmap does not move it away from the pointer");
                Call("ProcessPlaybackPointer", new Point(root.ActualWidth / 2, root.ActualHeight - 2), true, DateTime.UtcNow);
                Check(transport.IsVisible, "Actual bottom edge below persistent heatmap reveals transport");
                Call("NavigateTo", "settings");
                Check(!((FrameworkElement)window.FindName("GridVideo")).IsVisible, "Video surface hidden while settings are open");
                Call("NavigateTo", "library");
                Check(((FrameworkElement)window.FindName("MediaLibrary")).IsVisible, "Media list is a separate full page");
                Check(window.ViewModel.Playlist.EntryCount > 0, "Opened media is added to the queue");
                Call("NavigateTo", "home");
                Check(((ItemsControl)window.FindName("RecentMediaItems")).Items.Count > 0, "Recent media appears on the home page");
                Call("SetFullscreen", true, true);
                Check(!((FrameworkElement)window.FindName("NavigationRail")).IsVisible, "Fullscreen hides navigation");
                Call("SetFullscreen", false, true);
                Check(window.ViewModel.TimeSource.IsPlaying && ((Grid)window.FindName("ShellRoot")).RowDefinitions[2].Height.Value == 0, "Leaving fullscreen preserves playback and overlay layout");
                await CheckHomeShelves(args[0]);
                await CheckAppearancePreferences();
            }
            catch(Exception ex) { failures++; File.AppendAllText(log,"ERROR: "+ex+"\n"); }
            finally
            {
                File.AppendAllText(log, "Failures: "+failures+"\n");
                window.Close();
                app.Shutdown(failures == 0 ? 0 : 1);
            }
        }));
        app.Run(window);
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    private static async Task CheckAppearancePreferences()
    {
        var settings=window.ViewModel.Settings;
        await Task.Delay(850);
        var realtime=window.ViewModel.RealtimePlayback;
        Check(realtime != null, "Appearance isolation checks use a real realtime playback instance");
        var versionField=typeof(ScriptPlayer.Playback.RealtimePlaybackService).GetField("version",BindingFlags.NonPublic|BindingFlags.Instance);
        long before=(long)versionField.GetValue(realtime);
        Call("NavigateTo","settings"); Call("SelectSettingsPage","Appearance"); window.UpdateLayout();
        var language=(ComboBox)window.FindName("InterfaceLanguageSelector");
        var theme=(ComboBox)window.FindName("InterfaceThemeSelector");
        var accent=(ComboBox)window.FindName("InterfaceAccentSelector");
        var animations=(CheckBox)window.FindName("InterfaceAnimationsSwitch");
        Check(language.IsVisible && theme.IsVisible && accent.IsVisible && animations.IsVisible, "All appearance preferences have accessible live settings controls");
        settings.EnableInterfaceAnimations=false;
        settings.InterfaceLanguage="en";
        Check(ScriptPlayer.Localization.ChineseLocalization.CurrentLanguage=="en" && (string)language.SelectedValue=="en", "Language selection applies live without changing stored option identity");
        Check(((TextBlock)window.FindName("PageTitle")).Text=="Settings" && ((TextBlock)window.FindName("SettingsPageHeading")).Text.EndsWith(" / Interface and appearance"), "English refreshes both shell and settings page headings");
        window.UpdateLayout();
        Check(FindVisualChildren<TextBlock>((DependencyObject)window.FindName("UnifiedSettingsSections")).Any(t=>t.Text=="Controls and appearance" && t.TextWrapping==TextWrapping.Wrap && t.ActualHeight>20), "Long English settings group header renders through its wrapping template");
        Check(((ComboBoxItem)theme.Items[0]).Content.ToString()=="Dark", "English updates already-created appearance option labels");
        Check(((ComboBox)window.FindName("CompactSettingsNavigator")).Items.Cast<KeyValuePair<string,string>>().Any(p=>p.Key=="Appearance"&&p.Value.EndsWith(" / Interface and appearance")), "English refreshes compact settings labels without changing keys");
        var bound=new TextBlock(); bound.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding { Source="主页" });
        ScriptPlayer.Localization.ChineseLocalization.TranslateTree(bound);
        Check(bound.Text=="主页" && System.Windows.Data.BindingOperations.IsDataBound(bound,TextBlock.TextProperty), "Language never translates bound media-like text or removes its binding");
        var literal=new TextBlock { Text="主页" }; ScriptPlayer.Localization.ChineseLocalization.TranslateTree(literal);
        Check(literal.Text=="Home", "Explicit application UI labels translate to English");
        var categories=(Dictionary<string,FrameworkElement>)typeof(MainWindow).GetField("settingsSections",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
        foreach(var category in categories.Keys)
        {
            Call("SelectSettingsPage",category); window.UpdateLayout();
            string heading=((TextBlock)window.FindName("SettingsPageHeading")).Text;
            Check(!heading.Any(c=>c>=0x4e00&&c<=0x9fff), "English settings heading uses translated group and page: "+category);
        }
        Call("SelectSettingsPage","Appearance");
        settings.InterfaceTheme="Light"; settings.InterfaceAccent="Blue"; window.UpdateLayout();
        var root=(Grid)window.FindName("ShellRoot");
        var background=(System.Windows.Media.SolidColorBrush)root.Background;
        Check(background.Color.R>200 && background.Color.G>200 && background.Color.B>200, "Light theme updates actual shell background");
        var library=(FrameworkElement)window.FindName("MediaLibrary");
        Check(((System.Windows.Media.SolidColorBrush)library.TryFindResource("MediaTextBrush")).Color.R<60, "Light theme reaches media library local resource dictionary");
        var sections=(Dictionary<string,FrameworkElement>)typeof(MainWindow).GetField("settingsSections",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
        Check(((System.Windows.Media.SolidColorBrush)sections["Paths"].TryFindResource("MediaTextBrush")).Color.R<60, "Light theme reaches detached settings editor resources");
        var mediaInfo=(Button)window.FindName("MediaInfoButton");
        Check(((System.Windows.Media.SolidColorBrush)mediaInfo.Foreground).Color.R>200, "Light theme preserves readable text on the dark playback bar");
        Window about=null; Action<Window> capture=dialog=>about=dialog; InlineDialogService.Opened+=capture;
        try
        {
            ((Button)window.FindName("VersionButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(80);
            Check(about!=null && !about.IsVisible && FindVisualChildren<TextBlock>((DependencyObject)window.FindName("ShellRoot")).Any(t=>t.Text=="About ScriptPlayer"), "About opens inline and translates in the light English interface");
            var inlineLayer=FindVisualChildren<Grid>((DependencyObject)window.FindName("ShellRoot")).Single(g=>Equals(g.Tag,"InlineDialogLayer"));
            Check(FindVisualChildren<Button>(inlineLayer).Any(b=>Equals(b.Content,"Close")), "English inline dialog translates its close button");
            SavePreview(window,"round2-about-light-english.png"); InlineDialogService.Complete(about,false); await Task.Delay(80);
        }
        finally { InlineDialogService.Opened-=capture; if(about!=null && InlineDialogService.IsOpen) InlineDialogService.Complete(about,false); }
        foreach(var color in new[]{"Orange","Blue","Teal","Purple","Rose"})
        {
            settings.InterfaceAccent=color; window.UpdateLayout();
            Check((string)accent.SelectedValue==color && ScriptPlayer.Themes.AppearanceTheme.AccentName==color, "Accent preference applies and keeps stable key: "+color);
        }
        settings.InterfaceAccent="Blue";
        Check(((System.Windows.Media.SolidColorBrush)window.TryFindResource("MediaAccentBrush")).Color.B>150, "Blue accent updates the shared palette");
        Call("NavigateTo","home"); Call("NavigateTo","library");
        Check(!((FrameworkElement)window.FindName("BrowsePage")).HasAnimatedProperties && ((FrameworkElement)window.FindName("BrowsePage")).Opacity==1, "Disabled animation shows pages immediately at full opacity");
        settings.InterfaceMotionMode="Standard"; settings.SimplifyInterfaceAnimations=false;
        Call("NavigateTo","home");
        Check(!SystemParameters.ClientAreaAnimation || ((FrameworkElement)window.FindName("BrowsePage")).HasAnimatedProperties, "Enabled page transition respects system animation preference");
        settings.EnableInterfaceAnimations=false;
        Check(!((FrameworkElement)window.FindName("BrowsePage")).HasAnimatedProperties, "Turning animation off cancels an in-flight transition");
        for(int i=0;i<12;i++) Call("NavigateTo",i%2==0?"library":"home");
        Check(((FrameworkElement)window.FindName("BrowsePage")).Opacity==1 && ((TextBlock)window.FindName("PageTitle")).Text=="Home", "Rapid navigation leaves the latest page visible and translated");
        await Task.Delay(850);
        Check((long)versionField.GetValue(realtime)==before && ReferenceEquals(realtime,window.ViewModel.RealtimePlayback), "Immediate and delayed appearance changes never reconfigure realtime output");
        string filename=Path.Combine(Environment.GetEnvironmentVariable("APPDATA"),"ScriptPlayer","Settings.xml");
        var restored=SettingsViewModel.FromFile(filename);
        Check(restored!=null && restored.InterfaceLanguage=="en" && restored.InterfaceTheme=="Light" && restored.InterfaceAccent=="Blue" && !restored.EnableInterfaceAnimations, "Language theme accent and animation preferences survive XML roundtrip");
        SavePreview(window,"round2-light-english.png");
        Call("NavigateTo","settings"); Call("SelectSettingsPage","Appearance"); SavePreview(window,"round2-appearance-light-english.png");
        settings.InterfaceLanguage="zh-CN";
        Check(literal.Text=="主页" && bound.Text=="主页" && ((TextBlock)window.FindName("PageTitle")).Text=="设置", "Switching back restores exact UI source text and leaves bound values unchanged");
        window.Width=760; window.Height=620; window.UpdateLayout(); Call("UpdateResponsiveLayout"); window.UpdateLayout();
        Check(language.ActualWidth>=140 && animations.IsVisible && ((FrameworkElement)window.FindName("SettingsPageHost")).ActualWidth>=300, "Narrow appearance editor retains selectors and animation switch");
        SavePreview(window,"round2-appearance-760.png");
        settings.InterfaceTheme="Dark"; settings.InterfaceAccent="Orange";
        settings.EnableInterfaceAnimations=true;
        await Task.Delay(850); window.UpdateLayout(); SavePreview(window,"round2-appearance-dark-chinese.png");
        Check(((System.Windows.Media.SolidColorBrush)root.Background).Color.R<60, "Dark theme restores existing shell surface");
        Check((long)versionField.GetValue(realtime)==before, "Restoring original appearance still preserves realtime configuration");
        double initialFilter=settings.FilterRange;
        settings.FilterRange=initialFilter+.01; settings.InterfaceAccent="Teal";
        await Task.Delay(850);
        Check((long)versionField.GetValue(realtime)>before, "A pending real playback preference is not lost when appearance also changes");
        settings.FilterRange=initialFilter; settings.InterfaceAccent="Orange"; await Task.Delay(850);
        Check(SettingsViewModel.FromFile(filename).FilterRange==initialFilter, "Mixed playback and appearance preferences persist their final values");
    }
    private static async Task CheckSwitchBehavior()
    {
        Call("SelectSettingsPage", "Overview"); window.UpdateLayout();
        var host = (StackPanel)window.FindName("SettingsOverview");
        var style = window.TryFindResource("SettingsToggleSwitch") as Style;
        Check(style != null, "Settings card switch style is available");
        if (style == null) return;
        bool original = window.ViewModel.Settings.ShowHeatMap;
        var control = new CheckBox { Content = "统一开关回归验证：长文字应能换行且不会覆盖右侧开关", Style = style };
        control.SetBinding(ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("Settings.ShowHeatMap") { Source = window.ViewModel, Mode = System.Windows.Data.BindingMode.TwoWay });
        host.Children.Add(control);
        try
        {
            window.UpdateLayout(); control.ApplyTemplate();
            var binding = System.Windows.Data.BindingOperations.GetBindingExpression(control, ToggleButton.IsCheckedProperty);
            var track = control.Template.FindName("SwitchTrack", control) as FrameworkElement;
            var label = control.Template.FindName("SwitchLabel", control) as FrameworkElement;
            Check(track != null && label != null && control.ActualHeight >= 40, "Card switch exposes its label, track and usable hit area");
            if (track != null && label != null)
            {
                var trackRect = new Rect(track.TranslatePoint(new Point(), control), track.RenderSize);
                var labelRect = new Rect(label.TranslatePoint(new Point(), control), label.RenderSize);
                Check(labelRect.Right <= trackRect.Left && trackRect.Right <= control.ActualWidth + 1, "Switch label and track occupy separate bounded columns");
            }
            typeof(ToggleButton).GetMethod("OnToggle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, null);
            await Task.Delay(30);
            Check(window.ViewModel.Settings.ShowHeatMap == !original && ReferenceEquals(binding, System.Windows.Data.BindingOperations.GetBindingExpression(control, ToggleButton.IsCheckedProperty)), "Switch toggle updates the original setting without replacing its binding");
            control.IsEnabled = false; window.UpdateLayout();
            Check(!control.IsEnabled && control.Opacity < 1 && control.IsChecked == !original, "Disabled switch preserves state and has visible disabled feedback");
            var peer = new System.Windows.Automation.Peers.CheckBoxAutomationPeer(control);
            bool disabledBlocked = false;
            try { ((System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)).Toggle(); }
            catch (System.Windows.Automation.ElementNotEnabledException) { disabledBlocked = true; }
            Check(disabledBlocked && control.IsChecked == !original, "Disabled switch rejects an automation toggle without changing its setting");
            control.IsEnabled = true; control.ClearValue(ToggleButton.IsCheckedProperty); control.IsThreeState = true; control.IsChecked = null;
            window.UpdateLayout();
            Check(control.IsChecked == null && control.Template.FindName("SwitchTrack", control) != null, "Switch supports an explicit indeterminate state without converting it to false");
        }
        finally { window.ViewModel.Settings.ShowHeatMap = original; host.Children.Remove(control); }
        bool fillGaps = window.ViewModel.Settings.FillGaps;
        try
        {
            Call("SelectSettingsPage", "Gaps");
            window.ViewModel.Settings.FillGaps = false; window.UpdateLayout();
            var gapControls = FindVisualChildren<CheckBox>((FrameworkElement)((ContentControl)window.FindName("SettingsPageHost")).Content)
                .Where(check => System.Windows.Data.BindingOperations.GetBinding(check, UIElement.IsEnabledProperty)?.Path?.Path == "Settings.FillGaps").ToArray();
            Check(gapControls.Length >= 3 && gapControls.All(check => !check.IsEnabled), "Parent gap setting still disables all dependent switches");
            window.ViewModel.Settings.FillGaps = true; window.UpdateLayout();
            Check(gapControls.All(check => check.IsEnabled), "Dependent switches re-enable when their original parent setting changes");
        }
        finally { window.ViewModel.Settings.FillGaps = fillGaps; Call("SelectSettingsPage", "Overview"); }
    }

    private static async Task CheckPathTableSwitch(SettingsDialog editor, FrameworkElement section)
    {
        var row = new AdditionalPathView(Path.GetDirectoryName(log));
        editor.AdditionalPaths.Add(row);
        DataGrid grid = null;
        try
        {
            Call("SelectSettingsPage", "Paths"); window.UpdateLayout(); await Task.Delay(60);
            grid = FindVisualChildren<DataGrid>(section).Single();
            grid.ScrollIntoView(row); window.UpdateLayout();
            var displayed = FindVisualChildren<CheckBox>(grid).FirstOrDefault(check => ReferenceEquals(check.DataContext, row));
            Check(displayed != null && !displayed.IsHitTestVisible && !displayed.Focusable
                && displayed.Template.FindName("SwitchTrack", displayed) != null, "Path table displays a compact switch without accidental row toggles");
            grid.SelectedItem = row;
            grid.CurrentCell = new DataGridCellInfo(row, grid.Columns.OfType<DataGridCheckBoxColumn>().Single());
            grid.Focus(); grid.BeginEdit(); window.UpdateLayout();
            var editing = FindVisualChildren<CheckBox>(grid).FirstOrDefault(check => ReferenceEquals(check.DataContext, row) && check.IsHitTestVisible);
            Check(editing != null && editing.Focusable && editing.Template.FindName("SwitchTrack", editing) != null, "Path table edit mode exposes the same interactive switch");
            if (editing != null)
                typeof(ToggleButton).GetMethod("OnToggle", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editing, null);
            grid.CommitEdit(DataGridEditingUnit.Cell, true); grid.CommitEdit(DataGridEditingUnit.Row, true);
            ((TextBox)window.FindName("SettingsSearch")).Focus();
            await Task.Delay(30);
            Check(row.IncludeSubDirectories && editor.Settings.AdditionalPaths.Contains(row.ToPath()), "Leaving the path table saves the switch state to the original recursive-search setting");
            SavePreview(window, "v11-settings-path-switch.png");
        }
        finally
        {
            if (grid != null) grid.CancelEdit();
            editor.AdditionalPaths.Remove(row);
            Call("SelectSettingsPage", "Overview");
        }
    }

    private static async Task CheckHomeShelves(string video)
    {
        Call("NavigateTo", "home");
        window.Width = 920; window.Height = 620; window.UpdateLayout();
        var recent = (ItemsControl)window.FindName("RecentMediaItems");
        var favorites = window.FindName("FavoriteMediaItems") as ItemsControl;
        Check(favorites != null, "Home provides a favorite shelf backed by live library entries");
        if (favorites == null) return;
        var covers = FindVisualChildren<FrameworkElement>(recent).Where(x => x.Name == "HomeCover").ToArray();
        Check(covers.Any(x => Math.Abs(x.ActualWidth / Math.Max(1, x.ActualHeight) - 16.0 / 9) < 0.02), "Recent home covers use a widescreen aspect ratio");
        var titles = FindVisualChildren<TextBlock>(recent).Where(x => System.Windows.Data.BindingOperations.GetBinding(x, TextBlock.TextProperty)?.Path?.Path == "DisplayTitle").ToArray();
        Check(titles.Any(x => x.TextWrapping == TextWrapping.NoWrap && x.TextTrimming == TextTrimming.CharacterEllipsis), "Home titles remain one line with ellipsis");
        var folder = Path.Combine(Path.GetDirectoryName(log), "home-media-fixtures"); Directory.CreateDirectory(folder);
        var entries = new List<PlaylistEntry>();
        window.ViewModel.Settings.AutogenerateThumbnails = false;
        for (int i = 0; i < 8; i++)
        {
            string file = Path.Combine(folder, "favorite-" + i + ".mp4"); File.Copy(video, file, true);
            var entry = new PlaylistEntry(file) { DisplayTitle = "收藏媒体 " + i + " — " + new string('长', 45) };
            entries.Add(entry); window.ViewModel.Playlist.Entries.Add(entry); entry.IsFavorite = true;
        }
        await Task.Delay(100); window.UpdateLayout();
        Check(favorites.Items.Count == 6 && favorites.Items.Cast<PlaylistEntry>().All(entries.Contains), "Favorite shelf is bounded and reuses original library entry objects");
        window.ViewModel.Playlist.Filter = "无匹配的媒体搜索";
        await Task.Delay(80);
        Check(favorites.Items.Count == 6, "Library search cannot hide favorite home entries");
        window.ViewModel.Playlist.Filter = "";
        entries[0].IsFavorite = false; await Task.Delay(50);
        Check(!favorites.Items.Cast<PlaylistEntry>().Contains(entries[0]), "Removing a favorite immediately updates home");
        window.ViewModel.Playlist.Entries.Remove(entries[1]);
        // Removal is projected at DispatcherPriority.Background, like collection replacement.
        // Observe the scheduled operation rather than treating a timer's 50 ms slot as completion.
        var removalRefresh = window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        await Task.WhenAny(removalRefresh.Task, Task.Delay(1500));
        File.AppendAllText(log, "INFO: Home removal background barrier=" + removalRefresh.Status
            + ", sourceContains=" + window.ViewModel.Playlist.Entries.Contains(entries[1])
            + ", shelfContains=" + favorites.Items.Cast<PlaylistEntry>().Contains(entries[1]) + "\n");
        Check(!favorites.Items.Cast<PlaylistEntry>().Contains(entries[1]), "Removing a library entry removes its home tile");
        var oldCollection = window.ViewModel.Playlist.Entries;
        window.ViewModel.Playlist.Entries = new System.Collections.ObjectModel.ObservableCollection<PlaylistEntry>(oldCollection.Reverse());
        // The production projection deliberately runs after foreground layout work.
        // Wait for its result with a bound instead of assuming a 50 ms scheduler slot.
        var refreshDeadline = DateTime.UtcNow.AddMilliseconds(1500);
        while (DateTime.UtcNow < refreshDeadline && (favorites.Items.Count == 0 || !ReferenceEquals(favorites.Items[0], entries[7])))
            await Task.Delay(25);
        Check(favorites.Items.Count > 0 && ReferenceEquals(favorites.Items[0], entries[7]), "Replacing the library collection refreshes home order");
        foreach (int width in new[] { 920, 1280, 1920 })
        {
            window.Width = width; window.Height = 620; window.UpdateLayout(); await Task.Delay(40); window.UpdateLayout();
            var shelf = FindVisualChildren<ScrollViewer>(favorites).FirstOrDefault(x => x.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
                ?? FindVisualChildren<ScrollViewer>((FrameworkElement)window.FindName("FavoriteShelf")).FirstOrDefault();
            Check(shelf != null && !double.IsInfinity(shelf.ExtentWidth), "Home shelf has a bounded scroll viewport at width " + width);
            window.Height = 760; window.UpdateLayout(); await Task.Delay(40); window.UpdateLayout();
            SavePreview(window, "v10-home-" + width + ".png");
        }
        window.ViewModel.Playlist.Entries.Clear(); await Task.Delay(50); window.UpdateLayout();
        Check(favorites.Items.Count == 0 && ((FrameworkElement)window.FindName("FavoriteShelf")).Visibility == Visibility.Collapsed, "Clearing the library hides the empty favorite shelf");
        entries[0].IsFavorite = true; oldCollection.Add(new PlaylistEntry(video)); await Task.Delay(50);
        Check(favorites.Items.Count == 0, "Old entries and replaced collections cannot repopulate the home shelf");
        await CheckBatchedHomeProjection(folder);
    }

    private static async Task CheckBatchedHomeProjection(string privateFixtureFolder)
    {
        // The harness owns this playlist and profile. These paths are only model fixtures;
        // no media, metadata, or sidecar is generated or modified for the batch test.
        var library = window.ViewModel.Playlist;
        library.Entries.Clear();
        var batch = new List<PlaylistEntry>();
        using (var projection = new HomeMediaViewModel(library))
        {
            int notifications = 0;
            projection.Favorites.CollectionChanged += (s, e) => notifications++;
            for (int index = 0; index < 256; index++)
            {
                var entry = new PlaylistEntry(Path.Combine(privateFixtureFolder, "batch-model-" + index + ".mp4")) { IsFavorite = true };
                batch.Add(entry);
                library.Entries.Add(entry);
            }
            Check(projection.Favorites.Count == 0 && notifications == 0, "Batch adds defer home projection instead of publishing intermediate collections");
            await Task.Delay(80);
            Check(projection.Favorites.SequenceEqual(batch.Take(6)), "Batched home refresh publishes the first six original entries in source order");
            Check(notifications <= 7, "Batch adds publish one bounded home update without repeated collection churn");

            var retained = projection.Favorites.ToArray();
            int notificationsBeforeDispose = notifications;
            library.Entries.Clear();
            library.Entries.Add(new PlaylistEntry(Path.Combine(privateFixtureFolder, "disposed-model.mp4")) { IsFavorite = true });
            projection.Dispose();
            batch[0].IsFavorite = false;
            await Task.Delay(80);
            Check(projection.Favorites.SequenceEqual(retained) && notifications == notificationsBeforeDispose,
                "Disposing a home projection cancels its queued refresh and ignores later source changes");
        }
        library.Entries.Clear();
        await Task.Delay(50);
    }

    private static async Task CheckInlineEditors()
    {
        int visibleWindows=Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible);
        var folder=new EditFavouriteFolderDialog();
        var task=InlineDialogService.ShowAsync(folder);await Task.Delay(100);
        Check(InlineDialogService.IsOpen && !folder.IsVisible, "Favourite folder editor opens inside the settings page");
        Check(Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible)==visibleWindows, "Inline editor creates no additional visible window");
        folder.Path=Path.GetDirectoryName(log);folder.FolderName="Test folder";
        typeof(EditFavouriteFolderDialog).GetMethod("btnOk_Click",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(folder,new object[]{new Button(),new RoutedEventArgs()});
        Check(await task==true && folder.FolderName=="Test folder", "Favourite folder editor validates and returns saved values");
        var shortcut=new ShortcutInputDialog();var shortcutTask=InlineDialogService.ShowAsync(shortcut);await Task.Delay(80);
        Check(!shortcut.IsVisible && InlineDialogService.IsOpen, "Shortcut editor stays inside the player");
        InlineDialogService.Complete(shortcut,false);Check(await shortcutTask==false && !InlineDialogService.IsOpen, "Cancelling inline shortcut restores the parent page");
        var wifi=new TCodeWifiDialog(endpoint=>Task.CompletedTask,()=>{});var wifiTask=InlineDialogService.ShowAsync(wifi);await Task.Delay(80);
        Check(!wifi.IsVisible && InlineDialogService.IsDevicePageOpen && !InlineDialogService.IsOpen, "Wi-Fi settings open in the player without a separate window");
        SavePreview(window,"v3-inline-wifi.png");
        InlineDialogService.Complete(wifi,false);await wifiTask;
        await CheckEditor(new DeviceManagerDialog(window.ViewModel.Devices));
        await CheckEditor(new GeneratorProgressDialog(window.ViewModel));
        await CheckEditor(new TimeShiftDialog(TimeSpan.Zero));
        await CheckEditor(new SectionDialog());
        await CheckEditor(new HandyDeviceIdSettingsDialog("test-key", "127.0.0.1", "80", true));
        await CheckEditor(new KodiConnectionSettingsDialog("127.0.0.1", 8080, 9090, "", ""));
        await CheckEditor(new VlcConnectionSettingsDialog("127.0.0.1:8080", ""));
        await CheckEditor(new SerialPortSelector(new string[] { "TEST_PORT" }));
        var generator=new GeneratorSettingsDialog(window.ViewModel,new GeneratorSettingsViewModel(),GeneratedElements.All,GeneratedElements.Thumbnails);
        var generatorTask=InlineDialogService.ShowAsync(generator);await Task.Delay(100);
        ((TabItem)generator.FindName("tabGeneral")).IsSelected = true;
        window.UpdateLayout();
        var generationSwitches = FindVisualChildren<CheckBox>((FrameworkElement)((TabItem)generator.FindName("tabGeneral")).Content)
            .Where(check => check.IsVisible).ToArray();
        Check(generationSwitches.Length == 4 && generationSwitches.All(check => { check.ApplyTemplate(); return check.Template.FindName("SwitchTrack", check) != null; }), "Generator booleans use the same switch inside its embedded tab");
        var generationHost = (FrameworkElement)((TabItem)generator.FindName("tabGeneral")).Content;
        var generationBounds = generationSwitches.Select(check => new Rect(check.TranslatePoint(new Point(), generationHost), check.RenderSize)).ToArray();
        Check(generationBounds.SelectMany((rect, index) => generationBounds.Skip(index + 1).Select(other => !rect.IntersectsWith(other))).All(clear => clear), "Generator switch rows measure without overlapping after typography changes");
        SavePreview(window,"v3-inline-generator.png");
        var preview=new ThumbnailBannerGeneratorSettingsPreviewDialog(new ScriptPlayer.Generators.ThumbnailBannerGeneratorSettings());
        var previewTask=InlineDialogService.ShowAsync(preview);await Task.Delay(100);
        Check(InlineDialogService.IsOpen && !generator.IsVisible && !preview.IsVisible, "Nested generation preview stays inside existing player window");
        InlineDialogService.Complete(preview,false);await previewTask;
        Check(InlineDialogService.IsOpen, "Closing nested preview restores parent generator settings");
        InlineDialogService.Complete(generator,false);await generatorTask;
        await CheckEditor(new ThumbnailGeneratorSettingsDialog(window.ViewModel,null));
        await CheckEditor(new ThumbnailBannerGeneratorSettingsDialog(null));
        await CheckEditor(new PreviewGeneratorSettingsDialog(null));
        await CheckEditor(new EditMetaDataDialog());
        await CheckEditor(new VersionDialog(window.ViewModel.Version));
        foreach(var type in new[] {typeof(AudioDeviceSelector),typeof(FunstimDeviceSelector)})
        {
            var ctor=type.GetConstructors().Single();
            var empty=Activator.CreateInstance(ctor.GetParameters()[0].ParameterType);
            await CheckEditor((Window)ctor.Invoke(new[]{empty}));
        }
        Call("SelectSettingsPage","Overview");
        var currentMode=window.ViewModel.PlaybackMode;
        string oldEndpoint=window.ViewModel.Settings.VlcEndpoint;window.ViewModel.Settings.VlcEndpoint="";
        var engine=(ComboBox)window.FindName("EngineSelector");
        engine.SelectedItem=engine.Items.OfType<ComboBoxItem>().First(x=>(PlaybackMode)x.Tag==PlaybackMode.Vlc);
        Check(window.ViewModel.PlaybackMode==currentMode && ((TextBlock)window.FindName("SettingsPageHeading")).Text.Contains("外部播放器"), "Missing external engine configuration routes to in-page settings without opening a window");
        window.ViewModel.Settings.VlcEndpoint=oldEndpoint;
    }
    private static async Task CheckEditor(Window editor)
    {
        int before=Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible);
        var content = (FrameworkElement)editor.Content;
        if (editor is HandyDeviceIdSettingsDialog)
        {
            Check(FindVisualChildren<PasswordBox>(content).Any(box => box.Name == "deviceId"),
                "Handy inline editor masks its connection key");
            Check(!FindVisualChildren<TextBox>(content).Any(box => box.Text == "test-key"),
                "Handy inline editor contains no plaintext key textbox");
        }
        var task=InlineDialogService.ShowAsync(editor);await Task.Delay(80);
        Check(!editor.IsVisible && InlineDialogService.IsOpen && Application.Current.Windows.OfType<Window>().Count(w=>w.IsVisible)==before,"In-player submenu opens: "+editor.GetType().Name);
        if (editor is EditMetaDataDialog || editor is SectionDialog || editor is TimeShiftDialog
            || editor is AudioDeviceSelector || editor is HandyDeviceIdSettingsDialog
            || editor is KodiConnectionSettingsDialog || editor is VlcConnectionSettingsDialog)
        {
            window.UpdateLayout();
            var fields = FindVisualChildren<Control>(content).Where(control => control.IsVisible
                && (control is TextBox || control is PasswordBox || control is ComboBox || control is CheckBox || control is Button))
                .Where(control => !(control is TextBox) || !FindVisualChildren<ComboBox>(content)
                    .Any(combo => combo.IsAncestorOf(control))).ToArray();
            var bounds = fields.Select(field => new Rect(field.TranslatePoint(new Point(), content), field.RenderSize)).ToArray();
            Check(fields.Length >= 3 && bounds.All(rect => rect.Width > 0 && rect.Height > 0)
                && bounds.SelectMany((rect, index) => bounds.Skip(index + 1).Select(other => !rect.IntersectsWith(other))).All(clear => clear),
                "Inline form fields and actions do not overlap: " + editor.GetType().Name);
            SavePreview(window, "v11-inline-" + editor.GetType().Name + ".png");
        }
        InlineDialogService.Complete(editor,false);await task;
        Check(!InlineDialogService.IsOpen,"In-player submenu closes cleanly: "+editor.GetType().Name);
    }
    private static void SavePreview(FrameworkElement element, string filename)
    {
        element.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var drawing = new System.Windows.Media.DrawingVisual();
        using (var dc = drawing.RenderOpen()) dc.DrawRectangle(new System.Windows.Media.VisualBrush(element) { Stretch = System.Windows.Media.Stretch.Fill }, null, new Rect(0,0,element.ActualWidth,element.ActualHeight));
        bitmap.Render(drawing);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(Path.GetDirectoryName(log), filename))) encoder.Save(stream);
    }
    private static void Call(string name, params object[] args)
    {
        typeof(MainWindow).GetMethod(name, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,args);
    }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T item) yield return item;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }
    private static void Check(bool success,string name)
    {
        File.AppendAllText(log,(success?"PASS: ":"FAIL: ")+name+"\n");
        if(!success) { failures++; File.AppendAllText(log, new System.Diagnostics.StackTrace(true).ToString()); }
    }
    private static void TraceChrome(string tag)
    {
        var root = (FrameworkElement)window.FindName("ShellRoot");
        CursorPoint cursor; GetCursorPos(out cursor);
        var point = root.PointFromScreen(new Point(cursor.X, cursor.Y));
        Func<string, object> field = name => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
        Func<string, FrameworkElement> element = name => (FrameworkElement)window.FindName(name);
        File.AppendAllText(log, tag + " cursor=" + cursor.X + "," + cursor.Y + " root=" + point
            + " playing=" + window.ViewModel.PlaybackState.IsPlaying + " nativePlaying=" + window.ViewModel.TimeSource.IsPlaying
            + " seeking=" + window.ViewModel.PlaybackState.IsSeeking + " captured=" + System.Windows.Input.Mouse.Captured
            + " inline=" + InlineDialogService.IsOpen + " hidden=" + field("playbackChromeHidden")
            + " edge=" + field("edgeQuickSettings") + " keyboard=" + field("keyboardChromeFocus")
            + " transportHover=" + element("PlayerControls").IsMouseOver + " timelineHover=" + element("PlaybackTimeline").IsMouseOver
            + " transportFocus=" + element("PlayerControls").IsKeyboardFocusWithin + " timelineFocus=" + element("PlaybackTimeline").IsKeyboardFocusWithin
            + " lastActivity=" + ((DateTime)field("lastPlaybackActivity")).ToString("O")
            + " position=" + window.ViewModel.TimeSource.Progress + " duration=" + window.ViewModel.TimeSource.Duration + "\n");
    }
    private static async Task PlacePointer(FrameworkElement element)
    {
        window.UpdateLayout();
        var target = element.PointToScreen(new Point(element.ActualWidth / 2, element.ActualHeight / 2));
        if (!SetCursorPos((int)Math.Round(target.X), (int)Math.Round(target.Y))) throw new InvalidOperationException("SetCursorPos failed");
        await WaitChromeCondition(() => element.IsMouseOver, "Physical hover did not reach the transport");
    }
    private static async Task PrepareIdleChrome()
    {
        window.Activate(); window.UpdateLayout();
        var root = (FrameworkElement)window.FindName("ShellRoot");
        var target = root.PointToScreen(new Point(root.ActualWidth / 2, root.ActualHeight / 2));
        if (!SetCursorPos((int)Math.Round(target.X), (int)Math.Round(target.Y))) throw new InvalidOperationException("SetCursorPos failed");
        await WaitChromeCondition(() => !((FrameworkElement)window.FindName("PlayerControls")).IsMouseOver
            && !((FrameworkElement)window.FindName("PlaybackTimeline")).IsMouseOver
            && window.ViewModel.PlaybackState.IsPlaying && !window.ViewModel.PlaybackState.IsSeeking
            && System.Windows.Input.Mouse.Captured == null && !InlineDialogService.IsOpen,
            "Idle chrome precondition did not settle");
    }
    private static async Task WaitChromeCondition(Func<bool> predicate, string failure)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        do { window.UpdateLayout(); if (predicate()) return; await Task.Delay(20); } while (DateTime.UtcNow < deadline);
        throw new TimeoutException(failure);
    }
}
