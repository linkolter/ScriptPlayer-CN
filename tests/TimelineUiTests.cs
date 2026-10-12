using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Newtonsoft.Json;
using ScriptPlayer;
using ScriptPlayer.Controls;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Classes;
using ScriptPlayer.ViewModels;

internal static class TimelineUiTests
{
    static MainWindow window;
    static string log;
    static int failures;
    [StructLayout(LayoutKind.Sequential)]
    struct CursorPoint { public int X; public int Y; }
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out CursorPoint point);
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    static void Check(bool value, string message) { File.AppendAllText(log, (value ? "PASS: " : "FAIL: ") + message + "\n"); if (!value) failures++; }
    static object Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    static async Task Wait(Func<bool> condition, string message)
    {
        for (int i = 0; i < 250 && !condition(); i++) await Task.Delay(20);
        Check(condition(), message); if (!condition()) throw new TimeoutException(message);
    }
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var c = VisualTreeHelper.GetChild(root, i); yield return c; foreach (var next in Children(c)) yield return next; }
    }
    static void Hover(SeekBar bar, double relative)
    {
        typeof(SeekBar).GetMethod("UpdatePopup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, new object[] { new Point(bar.ActualWidth * relative, 10) });
    }
    static void Drag(SeekBar bar, double seconds, int phase)
    {
        typeof(SeekBar).GetMethod("OnSeek", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bar, new object[] { seconds / bar.Duration.TotalSeconds, TimeSpan.FromSeconds(seconds), phase });
    }
    static void Cache(string video, byte red, byte blue)
    {
        var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[] { blue, 0, red, 255, blue, 0, red, 255, blue, 0, red, 255, blue, 0, red, 255 }, 8);
        bitmap.Freeze();
        using (var collection = new VideoThumbnailCollection())
        {
            collection.Add(TimeSpan.Zero, bitmap); collection.Add(TimeSpan.FromSeconds(4), bitmap);
            using (var stream = File.Create(Path.ChangeExtension(video, ".thumbs"))) collection.Save(stream);
        }
    }
    static void Screenshot(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)element.ActualWidth), Math.Max(1, (int)element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
            context.DrawRectangle(new VisualBrush(element) { Stretch = Stretch.Fill }, null, new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        bitmap.Render(drawing); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(path)) encoder.Save(stream);
    }
    [STAThread]
    static void Main(string[] args)
    {
        log = Path.Combine(args[2], "timeline-ui.log"); File.WriteAllText(log, "v6 timeline and adaptive layout integration\n");
        Cache(args[0], 255, 0); Cache(args[1], 0, 255);
        var profile = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "ScriptPlayer"); Directory.CreateDirectory(profile);
        var saved = JsonConvert.SerializeObject(new Dictionary<string, TimeSpan> { { args[0], TimeSpan.FromSeconds(3) } });
        File.WriteAllText(Path.Combine(profile, "PausedExitPoints.json"), saved);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ScriptPlayer.Localization.ChineseLocalization.Initialize();
        var id = Guid.NewGuid().ToString("N"); InstanceHandler.Startup("TimelineTest-" + id, "TimelinePipe-" + id);
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var dictionary = XDocument.Load(args[3]).Root.Element(p + "Application.Resources").Element(p + "ResourceDictionary");
        app.Resources = (ResourceDictionary)XamlReader.Parse(dictionary.ToString(), new ParserContext { BaseUri = new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml") });
        window = new MainWindow(); window.InitializeComponent(); app.MainWindow = window;
        window.Loaded += (s, e) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Task.Delay(300);
                var vm = window.ViewModel;
                vm.Settings.WaitForDevicesToLoad = false; vm.Settings.AutoSkip = false; vm.Settings.RandomChapters = false; vm.Settings.AutogenerateThumbnails = false; vm.Settings.ShowHeatMap = true;
                var view = (PlaybackTimelineView)window.FindName("PlaybackTimeline");
                var model = (TimelineViewModel)typeof(MainWindow).GetField("timelineModel", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                vm.LoadFile(args[0]);
                await Wait(() => vm.PlaybackState.Status == PlaybackStatus.Ready && vm.PlaybackState.Duration.TotalSeconds > 5 && model.Thumbnails != null, "Real MPV and frozen cached thumbnails load for A");
                Check(model.Thumbnails.Get(0).Thumbnail.IsFrozen, "Thumbnail decoding produces cross-thread-safe frames");
                vm.Playback.Pause(true);
                Check(model.Markers.Any(x => x.Kind == TimelineMarkerKind.PausedExit && x.Position.TotalSeconds == 3), "Saved paused-exit point appears independently of current pause");
                var bookmark = new BookmarkViewModel { FilePath = args[0], Timestamp = TimeSpan.FromSeconds(6), Label = "片段一" };
                vm.Bookmarks.Folders.Add(new BookmarkFolderViewModel { Bookmarks = new ObservableCollection<BookmarkViewModel> { bookmark,
                    new BookmarkViewModel { FilePath = args[1], Timestamp = TimeSpan.FromSeconds(5), Label = "其他视频" },
                    new BookmarkViewModel { FilePath = args[0], Timestamp = TimeSpan.FromSeconds(999), Label = "越界" } } });
                Check(model.Markers.Count(x => x.Kind == TimelineMarkerKind.Bookmark) == 1, "Nested bookmarks are filtered by media and valid duration");
                bookmark.Label = "更新的片段";
                Check(model.Markers.Any(x => x.Label == "更新的片段"), "Editing bookmark updates timeline without reloading media");
                vm.Playback.SeekTo(TimeSpan.FromSeconds(2), TimeSpan.Zero); await Task.Delay(80); vm.SetLoopACommand.Execute(null);
                vm.Playback.SeekTo(TimeSpan.FromSeconds(4), TimeSpan.Zero); await Task.Delay(80); vm.SetLoopBCommand.Execute(null);
                Check(model.Markers.Any(x => x.Kind == TimelineMarkerKind.LoopA) && model.Markers.Any(x => x.Kind == TimelineMarkerKind.LoopB), "A and B points share the timeline marker layer");
                Call("RevealPlaybackChrome"); window.UpdateLayout();
                double height = view.ActualHeight;
                var heatTop = view.HeatSeek.TranslatePoint(new Point(), view);
                var progressTop = view.ProgressSeek.TranslatePoint(new Point(), view);
                Check(Math.Abs(progressTop.Y - heatTop.Y - view.HeatSeek.ActualHeight) < 0.5,
                    "Heatmap immediately precedes progress without a separate marker row between them");
                Check(Math.Abs(heatTop.X - progressTop.X) < 0.5 && Math.Abs(view.HeatSeek.ActualWidth - view.ProgressSeek.ActualWidth) < 0.5,
                    "Heatmap and progress share the same horizontal seek coordinates");
                for (int i = 0; i < 40; i++) Hover(view.ProgressSeek, i / 40.0);
                Check(view.IsPreviewVisible && Math.Abs(view.PreviewPosition.TotalSeconds - vm.PlaybackState.Duration.TotalSeconds * 39 / 40) < 0.1, "Fast hover displays newest time only");
                window.UpdateLayout();
                Check(Math.Abs(view.ActualHeight - height) < 0.1, "Preview does not change dock height or move settings button");
                Hover(view.MarkerBar, 6 / vm.PlaybackState.Duration.TotalSeconds);
                Check(view.PreviewMarkerText.Contains("更新的片段"), "Hover preview combines frame, timestamp and bookmark name");
                Check(Children(Window.GetWindow(view).Content as DependencyObject).OfType<ThumbnailPreview>().Any(x => x.IsVisible), "Preview is embedded in existing overlay tree");
                Check(!view.ProgressSeek.ShowPreview && !view.HeatSeek.ShowPreview && !view.MarkerBar.ShowPreview, "All timeline surfaces use one in-tree preview instead of old popup windows");
                Screenshot((FrameworkElement)Window.GetWindow(view).Content, Path.Combine(args[2], "timeline-preview.png"));
                view.ClosePreview(); Check(!view.IsPreviewVisible, "Leaving timeline clears preview");
                Drag(view.HeatSeek, 5, 0); Check(vm.PlaybackState.IsSeeking, "Heatmap drag enters unified seeking state");
                Drag(view.HeatSeek, 5, 2); await Task.Delay(100);
                Check(!vm.PlaybackState.IsPlaying && !vm.PlaybackState.IsSeeking && Math.Abs(vm.PlaybackState.Position.TotalSeconds - 5) < 0.15, "Heatmap drag preserves explicit paused state");
                vm.Playback.Play(); Drag(view.ProgressSeek, 7, 0);
                Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(10));
                Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Dragging keeps transport stable during hide checks");
                Drag(view.ProgressSeek, 7, 2); await Task.Delay(100);
                Check(vm.PlaybackState.IsPlaying && !vm.PlaybackState.IsSeeking, "Seek drag resumes previously playing media");
                // This test needs a real visible HWND and a playing engine. A failed capture
                // produces no LostMouseCapture event, and seeking at the old pointer's position
                // can land at EOF instead of exercising the intended resume-after-drag behavior.
                vm.Playback.Play();
                await Wait(() => vm.TimeSource.CanSeek && vm.PlaybackState.IsPlaying && !vm.PlaybackState.IsSeeking,
                    "Lost capture starts with a seekable, playing MPV engine");
                Call("RevealPlaybackChrome"); window.UpdateLayout();
                await Wait(() => view.ProgressSeek.IsVisible && view.ProgressSeek.IsEnabled
                    && view.ProgressSeek.ActualWidth > 0 && view.ProgressSeek.ActualHeight > 0
                    && PresentationSource.FromVisual(view.ProgressSeek) != null,
                    "Lost capture uses the visible, measured progress track");
                CursorPoint originalCursor;
                if (!GetCursorPos(out originalCursor)) throw new InvalidOperationException("Cannot preserve cursor for capture test.");
                try
                {
                    var captureWindow = Window.GetWindow(view);
                    captureWindow.Activate(); captureWindow.UpdateLayout();
                    bool captured = Mouse.Capture(view.ProgressSeek);
                    Check(captured && Mouse.Captured == view.ProgressSeek && view.ProgressSeek.IsMouseCaptured,
                        "Progress track receives real mouse capture before drag");
                    if (!captured || Mouse.Captured != view.ProgressSeek)
                        throw new InvalidOperationException("Progress track did not receive mouse capture.");
                    var target = view.ProgressSeek.PointToScreen(new Point(view.ProgressSeek.ActualWidth * 0.4, view.ProgressSeek.ActualHeight / 2));
                    if (!SetCursorPos((int)Math.Round(target.X), (int)Math.Round(target.Y)))
                        throw new InvalidOperationException("Cannot position cursor inside capture test track.");
                    Action<string> logPointer = phase =>
                    {
                        CursorPoint native;
                        bool found = GetCursorPos(out native);
                        var screenPoint = view.ProgressSeek.PointFromScreen(new Point(native.X, native.Y));
                        var mousePoint = Mouse.GetPosition(view.ProgressSeek);
                        File.AppendAllText(log, "INFO: Lost capture " + phase + ": native=" + found + " (" + native.X + "," + native.Y
                            + "), PointFromScreen=" + screenPoint + ", Mouse.GetPosition=" + mousePoint
                            + ", target=" + target + ", track=" + view.ProgressSeek.ActualWidth + "x" + view.ProgressSeek.ActualHeight
                            + ", ownerActive=" + captureWindow.IsActive + ", captured=" + view.ProgressSeek.IsMouseCaptured + "\n");
                    };
                    logPointer("after positioning");
                    // Capturing before moving directs native mouse messages to this WPF HWND,
                    // even when the underlying MPV surface was the previous mouse input source.
                    await Task.Delay(40); Mouse.Synchronize();
                    try
                    {
                    await Wait(() =>
                    {
                        Mouse.Synchronize();
                        var point = Mouse.GetPosition(view.ProgressSeek);
                        // Captured drags may leave the track vertically. SeekBar uses only X;
                        // require a valid non-EOF time, not a particular physical pointer Y.
                        return point.X >= 0 && point.X < view.ProgressSeek.ActualWidth * 0.9;
                    }, "Lost capture pointer selects an in-range time rather than EOF");
                    }
                    finally { logPointer("after mouse input pump"); }
                    typeof(SeekBar).GetField("_down", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view.ProgressSeek, true);
                    Drag(view.ProgressSeek, 4, 0);
                    await Wait(() => vm.PlaybackState.IsSeeking, "Captured progress drag enters seeking before capture is released");
                    Mouse.Capture(null);
                    await Wait(() => !vm.PlaybackState.IsSeeking && vm.PlaybackState.IsPlaying,
                        "Lost mouse capture completes drag instead of leaving playback paused");
                }
                finally
                {
                    if (Mouse.Captured == view.ProgressSeek) Mouse.Capture(null);
                    SetCursorPos(originalCursor.X, originalCursor.Y);
                }
                Hover(view.ProgressSeek, 0.5);
                vm.LoadFile(args[1]);
                Check(!view.IsPreviewVisible && !model.Markers.Any(x => x.Kind == TimelineMarkerKind.PausedExit), "Switch invalidates preview and previous media exit marker immediately");
                await Wait(() => vm.PlaybackState.Status == PlaybackStatus.Ready && model.Thumbnails != null && vm.ThumbnailsMediaPath == args[1], "B receives its own asynchronous cache");
                Check(!model.Markers.Any(x => x.Kind == TimelineMarkerKind.LoopA || x.Kind == TimelineMarkerKind.LoopB), "A/B selection does not leak into next media");
                for (int i = 0; i < 6; i++) { vm.LoadFile(args[0]); vm.LoadFile(args[1]); }
                await Wait(() => vm.PlaybackState.Status == PlaybackStatus.Ready && model.Thumbnails != null && vm.ThumbnailsMediaPath == args[1], "Rapid repeated replacement retains latest thumbnail identity");
                byte[] pixel = new byte[4]; model.Thumbnails.Get(0).Thumbnail.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
                Check(pixel[0] > pixel[2], "Final thumbnail pixels belong to blue B, not red A");
                Check(File.ReadAllText(Path.Combine(profile, "PausedExitPoints.json")) == saved, "Switching and timeline interaction never write exit records");
                foreach (int width in new[] { 760, 920, 1280, 1920 })
                {
                    window.Width = width; window.Height = width == 920 ? 620 : 820; Call("RevealPlaybackChrome"); await Task.Delay(80); window.UpdateLayout();
                    var gear = (Button)window.FindName("QuickSettingsButton"); var transport = (FrameworkElement)window.FindName("PlayerControls");
                    var narrowHeat = view.HeatSeek.TranslatePoint(new Point(), view);
                    var narrowProgress = view.ProgressSeek.TranslatePoint(new Point(), view);
                    Check(Math.Abs(narrowProgress.Y - narrowHeat.Y - view.HeatSeek.ActualHeight) < 0.5
                        && Math.Abs(narrowHeat.X - narrowProgress.X) < 0.5 && Math.Abs(view.HeatSeek.ActualWidth - view.ProgressSeek.ActualWidth) < 0.5,
                        "Unified heatmap and progress stay adjacent and aligned at width " + width);
                    Point point = gear.TranslatePoint(new Point(), transport);
                    Check(gear.IsVisible && point.X >= 0 && point.X + gear.ActualWidth <= transport.ActualWidth + 1, "Settings hit target remains inside transport at width " + width);
                    if (width <= 920)
                    {
                        Call("ProcessPlaybackPointer", new Point(2, ((Grid)window.FindName("ShellRoot")).ActualHeight / 2), true, DateTime.UtcNow);
                        await Task.Delay(80); window.UpdateLayout();
                        point = gear.TranslatePoint(new Point(), transport);
                        var play = (Button)window.FindName("MainPlayButton");
                        var playPoint = play.TranslatePoint(new Point(), transport);
                        Check(gear.IsVisible && point.X + gear.ActualWidth <= transport.ActualWidth + 1 && playPoint.X + play.ActualWidth <= point.X,
                            "Expanded media list keeps play and settings separate inside narrow transport");
                        Check(!((FrameworkElement)window.FindName("ShuffleTransport")).IsVisible && !((FrameworkElement)window.FindName("RepeatTransport")).IsVisible,
                            "Narrow transport prioritizes play and settings over secondary toggles");
                    }
                    var viewport=(FrameworkElement)window.FindName("VideoOverlayLayer");
                    File.AppendAllText(log,"BOUNDS width="+width+" shell="+((FrameworkElement)window.FindName("ShellRoot")).RenderSize+" video="+((FrameworkElement)window.FindName("GridVideo")).RenderSize+" overlay="+viewport.RenderSize+" dock="+((FrameworkElement)window.FindName("PlaybackDock")).RenderSize+" desired="+((FrameworkElement)window.FindName("PlaybackDock")).DesiredSize+" timeline="+view.RenderSize+"\n");
                    foreach(var part in new FrameworkElement[]{ view,view.ProgressSeek,view.CurrentTime,view.TotalTime,view.HeatContainer,view.MarkerBar })
                    {
                        if(!part.IsVisible) continue;
                        var top=part.PointToScreen(new Point());
                        var end=part.PointToScreen(new Point(part.ActualWidth,part.ActualHeight));
                        var hostTop=viewport.PointToScreen(new Point());
                        var hostEnd=viewport.PointToScreen(new Point(viewport.ActualWidth,viewport.ActualHeight));
                        Check(top.X>=hostTop.X-1 && end.X<=hostEnd.X+1 && top.Y>=hostTop.Y-1 && end.Y<=hostEnd.Y+1,
                            "Entire timeline part stays inside actual native overlay at width "+width+": "+part.GetType().Name);
                    }
                    Check(view.ProgressSeek.ActualWidth>=200 && view.TotalTime.ActualWidth>=40, "Narrow timeline retains seek length and total time at width "+width);
                    gear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                    var flyout = (InlineFlyout)window.FindName("QuickSettingsPopup");
                    Check(flyout.IsOpen && flyout.ActualWidth <= window.ActualWidth, "Quick settings opens with bounded width " + width);
                    var overlay = (FrameworkElement)Window.GetWindow(flyout).Content;
                    var flyoutPoint = flyout.TranslatePoint(new Point(), overlay);
                    Check(flyoutPoint.X >= 0 && flyoutPoint.Y >= 0 && flyoutPoint.X + flyout.ActualWidth <= overlay.ActualWidth + 1
                        && flyoutPoint.Y + flyout.ActualHeight <= overlay.ActualHeight + 1,
                        "Quick settings rectangle stays inside its actual overlay at width " + width);
                    if (width <= 920)
                    {
                        int windows = Application.Current.Windows.Count;
                        var group = Children(flyout).OfType<Expander>().Single(x => (string)x.Header == "片段与队列");
                        group.IsExpanded = true; window.UpdateLayout();
                        Check(Children(group).OfType<Button>().Any(x => (string)x.Content == "设 A 点" && x.IsVisible)
                            && Application.Current.Windows.Count == windows, "Quick submenu expands inside the existing player without another window");
                        Screenshot(flyout, Path.Combine(args[2], "v9-quick-settings.png"));
                        group.IsExpanded = false;
                    }
                    Call("CloseQuickSettings_Click", null, new RoutedEventArgs());
                    Call("NavigateTo", "library"); window.UpdateLayout();
                    var library = (PlaylistControl)window.FindName("MediaLibrary");
                    var listButton = library.FindName("ListViewButton") as Button;
                    var tileButton = library.FindName("TileViewButton") as Button;
                    Check(listButton != null && tileButton != null && listButton.IsVisible && tileButton.IsVisible, "Library exposes both view choices at width " + width);
                    if (listButton != null && tileButton != null)
                    {
                        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tileButton, null);
                        await Task.Delay(30); window.UpdateLayout();
                        Check(vm.Playlist.ViewStyle == PlaylistViewStyle.Tiles && ((SolidColorBrush)tileButton.BorderBrush).Color == ((SolidColorBrush)library.FindResource("MediaAccentBrush")).Color,
                            "Card view button uses the existing command and selected appearance at width " + width);
                        typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(listButton, null);
                        Check(vm.Playlist.ViewStyle == PlaylistViewStyle.List && listButton.Focusable, "Compact view remains clickable and keyboard accessible at width " + width);
                        var filterGrid = (FrameworkElement)library.FindName("LibraryFilterGrid");
                        foreach (string target in new[] { "SearchBorder", "TagFilter", "FavoriteFilter" })
                        {
                            var control = (FrameworkElement)library.FindName(target); var offset = control.TranslatePoint(new Point(), filterGrid);
                            Check(control.IsVisible && offset.X >= 0 && offset.Y >= 0 && offset.X + control.ActualWidth <= filterGrid.ActualWidth + 1
                                && offset.Y + control.ActualHeight <= filterGrid.ActualHeight + 1, "Library filter stays inside measured rows: " + target + " / " + width);
                        }
                        var more = (MenuItem)library.FindName("LibraryMoreMenu");
                        Check(more.Items.OfType<MenuItem>().Count() == 4, "More menu retains queue, generation, sorting and view groups at width " + width);
                    }
                    vm.Playlist.SetViewStyleCommand.Execute(PlaylistViewStyle.List);
                    foreach (var entry in vm.Playlist.Entries) entry.DisplayTitle = "布局检查标题：" + new string('长', 60);
                    await Task.Delay(50); window.UpdateLayout();
                    var titles = Children(library).OfType<TextBlock>().Where(x => x.Text.StartsWith("布局检查标题：")).ToArray();
                    Check(titles.Any(x => x.ActualWidth >= 100), "Library media title retains usable width " + width);
                    var filter = Children(library).OfType<ComboBox>().First();
                    Check(Children(filter).OfType<TextBlock>().Any(x => x.Text.StartsWith("全部标签")), "Tag filter displays a readable label instead of type name at width " + width);
                    var counts = Children(library).OfType<TextBlock>().SelectMany(x => x.Inlines.OfType<System.Windows.Documents.Run>()).Where(x => System.Windows.Data.BindingOperations.GetBinding(x, System.Windows.Documents.Run.TextProperty)?.Path?.Path == "Playlist.Entries.Count").ToArray();
                    Check(counts.Any(x => x.Text == vm.Playlist.Entries.Count.ToString()), "Localized playlist count retains live binding at width " + width);
                    Screenshot(library, Path.Combine(args[2], "library-" + width + ".png"));
                    Call("NavigateTo", "player");
                }
                vm.LoadFile(args[0]); await Wait(() => vm.PlaybackState.Status == PlaybackStatus.Ready && model.Thumbnails != null, "A restores its own markers and preview cache");
                vm.Playback.Play(); Call("RevealPlaybackChrome"); window.UpdateLayout();
                ((System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("chromeTimer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window)).Stop();
                view.ClosePreview(); Mouse.Capture(null); window.Focus();
                CursorPoint beforeHideCursor;
                bool canRestoreHideCursor = GetCursorPos(out beforeHideCursor);
                Check(canRestoreHideCursor, "Idle hide test saves the actual pointer position");
                try
                {
                    if (!canRestoreHideCursor) throw new InvalidOperationException("Cannot save cursor for idle hide test.");
                    var shell = (FrameworkElement)window.FindName("ShellRoot");
                    var center = new Point(shell.ActualWidth / 2, shell.ActualHeight / 2);
                    var screenCenter = shell.PointToScreen(center);
                    Check(SetCursorPos((int)Math.Round(screenCenter.X), (int)Math.Round(screenCenter.Y)), "Idle hide test moves pointer away from transport and timeline");
                    await Task.Delay(100);
                    Call("ProcessPlaybackPointer", center, true, DateTime.UtcNow);
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsMouseOver && !view.IsMouseOver,
                        "Idle hide test has no real pointer holding transport open");
                    Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(10)); window.UpdateLayout();
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible && view.HeatSeek.IsVisible && view.MarkerBar.IsVisible, "Hidden transport retains heatmap and saved/bookmark markers at bottom");
                    Check(!view.ProgressSeek.IsVisible, "Hidden transport removes seek track without removing persistent layers");
                    var heatPoint = view.HeatSeek.TranslatePoint(new Point(view.HeatSeek.ActualWidth / 2, view.HeatSeek.ActualHeight / 2), shell);
                    var heatScreen = shell.PointToScreen(heatPoint);
                    Check(SetCursorPos((int)Math.Round(heatScreen.X), (int)Math.Round(heatScreen.Y)), "Heatmap hover uses the actual pointer inside the persistent strip");
                    await Task.Delay(80);
                    Check(view.HeatSeek.IsMouseOver && !((FrameworkElement)window.FindName("PlayerControls")).IsMouseOver,
                        "Real pointer is on heatmap rather than transport during preview-only hover");
                    Call("ProcessPlaybackPointer", heatPoint, true, DateTime.UtcNow);
                    Hover(view.HeatSeek, 0.5);
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible && view.IsPreviewVisible,
                        "Persistent heatmap hover shows preview without revealing transport");
                    Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(3));
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Resting on heatmap does not reveal hidden transport");
                    var stripTop = view.TranslatePoint(new Point(view.ActualWidth / 2, 0), shell);
                    Call("ProcessPlaybackPointer", new Point(stripTop.X, stripTop.Y - 2), true, DateTime.UtcNow);
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Approaching heatmap from video does not enter a phantom bottom reveal zone");
                    SetCursorPos((int)Math.Round(screenCenter.X), (int)Math.Round(screenCenter.Y));
                    await Task.Delay(80); view.ClosePreview();
                    Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(10)); window.UpdateLayout();
                    Check(!((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Actual bottom reveal starts from hidden transport");
                    Call("ProcessPlaybackPointer", new Point(shell.ActualWidth / 2, shell.ActualHeight - 2), true, DateTime.UtcNow); window.UpdateLayout();
                    Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible && !((FrameworkElement)window.FindName("NavigationRail")).IsVisible,
                        "Only the real bottom edge reveals transport independently of persistent heatmap");
                    model.SetBookmarks(null);
                    model.SetPresentation(vm.LoadedMedia, null, false, null, null, null, null, null, null);
                    Call("EvaluatePlaybackChrome", DateTime.UtcNow.AddSeconds(10)); window.UpdateLayout();
                    Check(!view.HeatSeek.IsVisible && !view.MarkerBar.IsVisible && !((FrameworkElement)window.FindName("PlayerControls")).IsVisible,
                        "No-heatmap and no-marker scenario starts with hidden transport");
                    Check(Math.Abs(view.ActualHeight - 12) < 0.5, "Empty persistent timeline retains only the real bottom reveal strip");
                    view.Compact = true; window.UpdateLayout();
                    Check(view.ActualHeight < 0.5, "Compact empty timeline does not reserve a player bottom reveal strip");
                    view.Compact = false;
                    Call("ProcessPlaybackPointer", new Point(shell.ActualWidth / 2, shell.ActualHeight - 2), true, DateTime.UtcNow); window.UpdateLayout();
                    Check(((FrameworkElement)window.FindName("PlayerControls")).IsVisible, "Real bottom edge also reveals controls without heatmap or markers");
                    Call("RefreshTimelinePresentation"); window.UpdateLayout();
                }
                finally
                {
                    if (canRestoreHideCursor) Check(SetCursorPos(beforeHideCursor.X, beforeHideCursor.Y), "Idle hide test restores the actual pointer position");
                }
                Call("RevealPlaybackChrome"); window.UpdateLayout();
                Screenshot((FrameworkElement)window.FindName("PlaybackDock"), Path.Combine(args[2], "timeline-controls.png"));
                vm.ClearLoopCommand.Execute(null);
                Check(!model.Markers.Any(x => x.Kind == TimelineMarkerKind.LoopA || x.Kind == TimelineMarkerKind.LoopB), "Clearing AB leaves other marker sources intact");
                Check(model.Markers.Any(x => x.Kind == TimelineMarkerKind.PausedExit) && model.Markers.Any(x => x.Kind == TimelineMarkerKind.Bookmark), "Saved exit and nested bookmark remain independent");
                var tags = new CompactTagStrip { Tags = new[] { "标签一", "标签二", "标签三", "很长的标签名称需要完整查看", "标签五", "标签六", "标签七", "标签八" } };
                tags.Measure(new Size(180, 25)); tags.Arrange(new Rect(0, 0, 180, 25)); tags.UpdateLayout();
                var tagTexts = Children(tags).OfType<TextBlock>().Select(x => x.Text).ToArray();
                Check(tagTexts.Any(x => x.StartsWith("+")), "Dense tags expose remaining count instead of clipping a wrap row");
                Check(((string)tags.ToolTip).Contains("标签八") && ((string)tags.ToolTip).Contains("很长的标签名称需要完整查看"), "Collapsed tags preserve complete labels in tooltip");
                Check(Children(tags).OfType<Border>().All(x => x.TranslatePoint(new Point(), tags).X + x.ActualWidth <= tags.ActualWidth + 1), "Tag chips remain inside available width");
                Call("RevealPlaybackChrome"); window.UpdateLayout();
                var cached = Path.ChangeExtension(args[0], ".thumbs"); File.Move(cached, cached + ".saved");
                try
                {
                    // Synthetic hover tests must not race a pending real pointer leave or
                    // a layout invalidation from the preceding interactive/resize checks.
                    var previewShell = (FrameworkElement)window.FindName("ShellRoot");
                    var neutralScreen = previewShell.PointToScreen(new Point(previewShell.ActualWidth / 2, previewShell.ActualHeight / 2));
                    Check(SetCursorPos((int)Math.Round(neutralScreen.X), (int)Math.Round(neutralScreen.Y)), "On-demand preview isolates the real pointer from synthetic hover");
                    await Task.Delay(100); window.UpdateLayout();
                    Check(!view.IsMouseOver, "Synthetic preview starts without a real pointer on a timeline surface");
                    typeof(MainViewModel).GetMethod("TryFindMatchingThumbnails", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, new object[] { args[0], false });
                    window.UpdateLayout();
                    Hover(view.ProgressSeek, 0.25);
                    Check(model.Thumbnails == null && view.IsPreviewVisible, "Missing thumbnail cache still provides hover timestamp and marker preview");
                    await Wait(() => view.GeneratedPreviewFrame != null, "Missing cache receives a real on-demand FFmpeg preview");
                    Check(view.GeneratedPreviewFrame.IsFrozen && view.GeneratedPreviewFrame.PixelWidth == 224 && view.GeneratedPreviewFrame.PixelHeight == 126,
                        "On-demand frame has a bounded size and is safe to cross threads");
                    for (int i = 0; i < 30; i++) Hover(view.ProgressSeek, i / 40.0);
                    // A deferred missing-cache recheck can finish during FFmpeg generation.
                    // Unchanged presentation must not cancel the current hover request.
                    typeof(MainViewModel).GetMethod("TryFindMatchingThumbnails", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, new object[] { args[0], false });
                    Check(view.IsPreviewVisible && model.Thumbnails == null,
                        "Repeated missing-cache metadata preserves the active latest hover preview");
                    try { await Wait(() => view.GeneratedPreviewFrame != null, "Fast uncached hover settles on the latest requested preview"); }
                    finally
                    {
                        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                        var cancellation = (System.Threading.CancellationTokenSource)typeof(PlaybackTimelineView).GetField("previewCancellation", flags).GetValue(view);
                        File.AppendAllText(log, "INFO: Fast preview visible=" + view.IsPreviewVisible + ", generated=" + (view.GeneratedPreviewFrame != null)
                            + ", position=" + view.PreviewPosition + ", state=" + vm.PlaybackState.Status + ", session=" + vm.PlaybackState.SessionId
                            + ", request=" + typeof(PlaybackTimelineView).GetField("previewRequest", flags).GetValue(view)
                            + ", hoverSource=" + (typeof(PlaybackTimelineView).GetField("hoverSource", flags).GetValue(view) != null)
                            + ", cancellation=" + (cancellation == null ? "none" : cancellation.IsCancellationRequested.ToString())
                            + ", ffmpeg=" + view.PreviewExecutableProvider() + ", media=" + vm.PlaybackState.MediaPath + "\n");
                    }
                    Check(Math.Abs(view.PreviewPosition.TotalSeconds - vm.PlaybackState.Duration.TotalSeconds * 29 / 40) < 0.1,
                        "Generated frame does not replace the latest hover timestamp");
                    File.Copy(cached + ".saved", cached);
                    typeof(MainViewModel).GetMethod("TryFindMatchingThumbnails", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, new object[] { args[0], false });
                    await Wait(() => model.Thumbnails != null, "A real cache replacement publishes its new thumbnail collection");
                    Check(!view.IsPreviewVisible && view.GeneratedPreviewFrame == null,
                        "A real cache replacement invalidates the previous generated hover frame");
                    File.Delete(cached);
                    typeof(MainViewModel).GetMethod("TryFindMatchingThumbnails", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(vm, new object[] { args[0], false });
                    Hover(view.ProgressSeek, 0.15); view.ClosePreview(); await Task.Delay(450);
                    Check(!view.IsPreviewVisible && view.GeneratedPreviewFrame == null, "Leaving timeline cancels generation and cannot reopen the preview");
                    Hover(view.ProgressSeek, 0.35); vm.LoadFile(args[1]);
                    await Wait(() => vm.PlaybackState.Status == PlaybackStatus.Ready && vm.ThumbnailsMediaPath == args[1], "Video switch completes while an uncached preview is pending");
                    await Task.Delay(450);
                    Check(!view.IsPreviewVisible && view.GeneratedPreviewFrame == null, "Old on-demand preview cannot appear in a replacement session");
                }
                finally
                {
                    // The private cache-replacement probe may fail after restoring a copy.
                    // Remove only that fixture copy so the original cache can be restored.
                    if (File.Exists(cached)) File.Delete(cached);
                    File.Move(cached + ".saved", cached);
                }
            }
            catch (Exception ex) { failures++; File.AppendAllText(log, "ERROR: " + ex + "\n"); }
            finally { window.ViewModel.Playback.Play(); window.Close(); File.AppendAllText(log, "Failures: " + failures + "\n"); app.Shutdown(failures == 0 ? 0 : 1); }
        }));
        app.Run(window); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
