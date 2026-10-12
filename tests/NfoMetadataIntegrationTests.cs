using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using ScriptPlayer.Controls;
using ScriptPlayer.Dialogs;
using ScriptPlayer.Metadata;
using ScriptPlayer.ViewModels;

internal static class NfoMetadataIntegrationTests
{
    private static int failures;
    private static string root;
    private static MainViewModel sharedVm;
#if NFO_CALLER_CURRENT
    [StructLayout(LayoutKind.Sequential)] private struct ClientRectangle { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct ClientPoint { public int X, Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetClientRect(IntPtr window, out ClientRectangle rectangle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool ClientToScreen(IntPtr window, ref ClientPoint point);
#endif
    private sealed class TestAssertionException : Exception { public TestAssertionException(string message) : base(message) { } }
    private sealed class CaseScope : IDisposable
    {
        public CaseScope() { sharedVm.Playlist.DeferLoading = true; sharedVm.Playlist.Entries.Clear(); }
        public void Dispose() { sharedVm.Playlist.Entries.Clear(); }
    }
    [STAThread] public static int Main(string[] arguments)
    {
        try
        {
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            string profile = Environment.GetEnvironmentVariable("APPDATA");
            if (string.IsNullOrWhiteSpace(profile) || profile.IndexOf("nfo-caller-profile-", StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException("Private APPDATA runner required; real profile access is forbidden.");
            root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures");
            if (!File.Exists(Path.Combine(root, ".scriptplayer-r02-private-fixture"))) throw new InvalidOperationException("Private fixture marker missing.");
            new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            ScriptPlayer.Localization.ChineseLocalization.Initialize();
#if NFO_CALLER_CURRENT
            if (arguments.Contains("--layout-probe-only"))
            {
                Run("Long NFO diagnostic remains scrollable and save/cancel stay inside the minimum dialog", LongDiagnosticLayout);
                return failures == 0 ? 0 : 1;
            }
#endif
            sharedVm = new MainViewModel();
            sharedVm.Playlist.DeferLoading = true;
            Run("Actual favorite failure does not publish an unsaved favorite", FavoriteFailure);
            Run("Actual editor title change preserves distinct OriginalTitle", EditorOriginalTitle);
            Run("Actual favorite refreshes every Entry sharing its NFO", SharedFavorite);
            Run("Actual favorite uses resolved media NFO across separate script directory", AssociatedDirectory);
            Run("Actual editor menu uses resolved media NFO across separate script directory", AssociatedEditor);
#if NFO_CALLER_CURRENT
            Run("Editor rejected save stays open with nonmodal status and unchanged snapshot", EditorFailure);
            Run("Publish uses committed snapshot without resetting playback/library state", CommittedProjection);
            Run("Saved file remains successful when a shared Entry refresh fails", RefreshFailure);
            Run("Actual editor title/tags publish shared projections and refresh available tag counts", SharedEditorProjection);
            Run("Actual editor keeps the deleted exact sidecar identity instead of rebinding shared NFO", DeletedExactEditor);
#endif
        }
        catch (Exception ex) { failures++; Console.WriteLine("ERROR: " + ex); }
        finally { if (sharedVm != null) sharedVm.Dispose(); if (Application.Current != null) Application.Current.Shutdown(); }
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }
    private static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS: " + name); }
        catch (TestAssertionException ex) { failures++; Console.WriteLine("FAIL: " + name + " :: " + ex.Message); }
        catch (Exception ex) { failures++; Console.WriteLine("ERROR: " + name + " :: " + ex); }
    }
    private static void Assert(bool ok, string message) { if (!ok) throw new TestAssertionException(message); }
    private static string Put(string folder, string name, string value = "private fixture")
    {
        string dir = Path.GetFullPath(Path.Combine(root, folder));
        if (!dir.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Fixture escaped private root.");
        Directory.CreateDirectory(dir); string path = Path.Combine(dir, name); File.WriteAllText(path, value); return path;
    }
    private static PlaylistEntry Add(MainViewModel vm, string path, string media, string script = null)
    {
        vm.Playlist.DeferLoading = true;
        var entry = new PlaylistEntry(path) { Duration = TimeSpan.FromSeconds(42), SelectedScriptName = "chosen-author.funscript" };
        vm.Playlist.Entries.Add(entry); entry.LoadLocalMetadata(media, script, script == null ? new string[0] : new[] { script }); return entry;
    }
    private static void Favorite(PlaylistControl control, PlaylistEntry entry)
    {
        var button = new Button { DataContext = entry };
        var args = new RoutedEventArgs(UIElement.MouseLeftButtonUpEvent, button);
        try { typeof(PlaylistControl).GetMethod("Favorite_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { button, args }); Assert(args.Handled, "Favorite event did not suppress item playback routing."); }
        catch (TargetInvocationException ex) {
#if NFO_CALLER_CURRENT
            throw new TestAssertionException("Current favorite handler leaked an exception: " + ex.InnerException);
#else
            if (!(ex.InnerException is IOException) && !(ex.InnerException is UnauthorizedAccessException)) throw;
#endif
        }
    }
    private static void FavoriteFailure()
    {
        string video = Put("favorite-fail", "Movie.mp4"), nfo = Put("favorite-fail", "Movie.nfo", "<movie><title>Loaded</title><favorite>false</favorite></movie>");
        string bytes = File.ReadAllText(nfo); File.SetAttributes(nfo, FileAttributes.ReadOnly);
        try { using (var scope = new CaseScope()) { var vm = sharedVm; var e = Add(vm, video, video); var control = new PlaylistControl { ViewModel = vm }; Favorite(control, e); Assert(!e.IsFavorite, "Entry favorite changed despite rejected readonly write."); Assert(File.ReadAllText(nfo) == bytes, "Readonly original changed.");
#if NFO_CALLER_CURRENT
            Assert(!string.IsNullOrWhiteSpace(control.MetadataStatusMessage) && control.MetadataStatusMessage.Contains(nfo), "Failure path is absent from nonmodal status.");
#endif
        } } finally { File.SetAttributes(nfo, FileAttributes.Normal); }
    }
    private static void EditThroughDialog(string path, string title)
    {
        var dialog = new NfoEditorDialog(path);
        dialog.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
            ((TextBox)dialog.FindName("txtTitle")).Text = title;
            typeof(NfoEditorDialog).GetMethod("BtnSave_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { null, new RoutedEventArgs(Button.ClickEvent) });
        }));
        Assert(dialog.ShowDialog() == true, "Successful editor did not close as saved.");
    }
    private static void EditorOriginalTitle()
    {
        string video = Put("editor-title", "Movie.mp4"); Put("editor-title", "Movie.nfo", "<movie><title>Current</title><originaltitle>Original source</originaltitle></movie>");
        EditThroughDialog(video, "Edited title"); var metadata = NfoMetadata.Load(video);
        Assert(metadata.Title == "Edited title" && metadata.OriginalTitle == "Original source", "Editing title overwrote distinct originaltitle.");
    }
    private static void SharedFavorite()
    {
        string video = Put("shared", "Movie.mp4"), script = Put("shared", "Movie-soft.funscript", "{\"actions\":[]}");
        Put("shared", "Movie.nfo", "<movie><title>Shared</title><favorite>false</favorite></movie>");
        using (var scope = new CaseScope()) { var vm = sharedVm; var first = Add(vm, video, video, script); var other = Add(vm, script, video, script);
            var preview = new DrawingImage(); var heatmap = new DrawingImage(); other.Preview = preview; other.HeatMap = heatmap; other.Playing = true;
            string exactScript = Put("shared", "Movie-special.funscript"), exactNfo = Put("shared", "Movie-special.nfo", "<movie><favorite>false</favorite></movie>");
            var exact = Add(vm, exactScript, null, exactScript);
            string unrelatedVideo = Put("shared-other-directory", "Movie.mp4"); Put("shared-other-directory", "Movie.nfo", "<movie><favorite>false</favorite></movie>"); var unrelated = Add(vm, unrelatedVideo, unrelatedVideo);
            using (var home = new HomeMediaViewModel(vm.Playlist))
            {
            Favorite(new PlaylistControl { ViewModel = vm }, first);
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            Assert(home.HasFavorites && home.Favorites.Contains(first) && home.Favorites.Contains(other) && !home.Favorites.Contains(exact) && !home.Favorites.Contains(unrelated), "Home favorites projection did not observe committed shared favorite.");
            Assert(first.IsFavorite && other.IsFavorite, "Shared sidecar Entry remained stale after favorite save."); Assert(!exact.IsFavorite && !unrelated.IsFavorite && !NfoMetadata.Load(exactNfo).Favorite, "Independent exact NFO or other-directory Entry was incorrectly refreshed.");
            Assert(other.Duration == TimeSpan.FromSeconds(42) && other.SelectedScriptName == "chosen-author.funscript" && other.Playing && ReferenceEquals(preview, other.Preview) && ReferenceEquals(heatmap, other.HeatMap), "Metadata refresh reset unrelated state or image references."); }
            }
    }
    private static void AssociatedDirectory()
    {
        string video = Put("associated/media", "Movie.mp4"), script = Put("associated/scripts", "Movie-soft.funscript", "{\"actions\":[]}");
        string nfo = Put("associated/media", "Movie.nfo", "<movie><title>Associated</title><favorite>false</favorite></movie>");
        using (var scope = new CaseScope()) { var vm = sharedVm; var entry = Add(vm, script, video, script); Favorite(new PlaylistControl { ViewModel = vm }, entry);
            Assert(NfoMetadata.Load(nfo).Favorite, "Favorite wrote wrong folder instead of resolved NFO."); Assert(!File.Exists(Path.ChangeExtension(script, ".nfo")), "Unexpected NFO created beside script."); }
    }
    private static void AssociatedEditor()
    {
        string video = Put("associated-editor/media", "Movie.mp4"), script = Put("associated-editor/scripts", "Movie-soft.funscript", "{\"actions\":[]}");
        string nfo = Put("associated-editor/media", "Movie.nfo", "<movie><title>Associated</title><originaltitle>Source</originaltitle></movie>");
        using (var scope = new CaseScope())
        {
            var vm = sharedVm;
            var entry = Add(vm, script, video, script); var control = new PlaylistControl { ViewModel = vm }; var host = new Window { Content = control, Width = 640, Height = 480 }; host.Show();
            try {
                host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
                    var dialog = Application.Current.Windows.OfType<NfoEditorDialog>().Single();
                    ((TextBox)dialog.FindName("txtTitle")).Text = "Associated edited";
                    typeof(NfoEditorDialog).GetMethod("BtnSave_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { null, new RoutedEventArgs(Button.ClickEvent) });
                }));
                typeof(PlaylistControl).GetMethod("MnuEditNfo_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { new MenuItem { CommandParameter = entry }, new RoutedEventArgs(MenuItem.ClickEvent) });
                Assert(NfoMetadata.Load(nfo).Title == "Associated edited", "Menu editor used script folder instead of Entry.NfoPath."); Assert(!File.Exists(Path.ChangeExtension(script, ".nfo")), "Editor created wrong-directory sidecar.");
            } finally { host.Close(); }
        }
    }
#if NFO_CALLER_CURRENT
    private static System.Collections.Generic.IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++) { var child = VisualTreeHelper.GetChild(parent, i); yield return child; foreach (var descendant in VisualChildren(child)) yield return descendant; }
    }
    private static void LongDiagnosticLayout()
    {
        Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/ScriptPlayer;component/Themes/MediaTheme.xaml", UriKind.Absolute) });
        string nfo = Put("long-diagnostic", "Movie.nfo", "<movie><title>Private layout fixture</title></movie>");
        var dialog = new NfoEditorDialog(nfo) { Width = 560, Height = 430 };
        var status = (TextBlock)dialog.FindName("txtStatus");
        status.Text = "保存失败，请检查恢复位置：" + new string('诊', 3000) + Environment.NewLine + nfo + ".r02-fixture.bak" + Environment.NewLine + nfo + ".r02-fixture.tmp";
        dialog.Show();
        try
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { })); dialog.UpdateLayout();
            var viewport = (FrameworkElement)dialog.Content;
            var buttons = VisualChildren(viewport).OfType<Button>().ToArray();
            var save = buttons.Single(button => button.IsDefault); var cancel = buttons.Single(button => button.IsCancel);
            Rect saveBounds = save.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), save.RenderSize));
            Rect cancelBounds = cancel.TransformToAncestor(viewport).TransformBounds(new Rect(new Point(), cancel.RenderSize));
            IntPtr fixtureHandle = new WindowInteropHelper(dialog).Handle;
            ClientRectangle client; var origin = new ClientPoint();
            if (!GetClientRect(fixtureHandle, out client) || !ClientToScreen(fixtureHandle, ref origin)) throw new InvalidOperationException("Could not measure this fixture window client viewport.");
            Rect clientBounds = new Rect(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
            var saveTop = save.PointToScreen(new Point()); var saveBottom = save.PointToScreen(new Point(save.ActualWidth, save.ActualHeight));
            var cancelTop = cancel.PointToScreen(new Point()); var cancelBottom = cancel.PointToScreen(new Point(cancel.ActualWidth, cancel.ActualHeight));
            var scroll = VisualChildren(viewport).OfType<ScrollViewer>().FirstOrDefault(view => VisualChildren(view).Contains(status));
            bool scrollable = scroll != null && scroll.ScrollableHeight > 0 && scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto;
            Console.WriteLine("INFO: dialog=" + dialog.ActualWidth + "x" + dialog.ActualHeight + "; grid=" + viewport.RenderSize + "; clientScreen=" + clientBounds + "; status=" + status.RenderSize + "; save=" + saveTop + "->" + saveBottom + "; cancel=" + cancelTop + "->" + cancelBottom + "; diagnosticScroll=" + (scroll == null ? "none" : scroll.ViewportHeight + "/" + scroll.ExtentHeight));
            bool boundsInside = clientBounds.Contains(saveTop) && clientBounds.Contains(saveBottom) && clientBounds.Contains(cancelTop) && clientBounds.Contains(cancelBottom);
            Assert(boundsInside && scrollable, "Minimum dialog lost reachable footer or scrollable diagnostic. save=" + saveBounds + "; cancel=" + cancelBounds + "; actualClient=" + clientBounds + "; scroll=" + scrollable);
            scroll.ScrollToBottom(); dialog.UpdateLayout(); Assert(scroll.VerticalOffset > 0, "Recovery diagnostic cannot scroll to remaining text.");
        }
        finally { dialog.Close(); }
    }
    private static void EditorFailure()
    {
        foreach (string mode in new[] { "readonly", "stale" })
        {
            string nfo = Put("editor-fail-" + mode, "Movie.nfo", "<movie><title>Loaded</title><originaltitle>Source</originaltitle></movie>");
            var dialog = new NfoEditorDialog(nfo); ((TextBox)dialog.FindName("txtTitle")).Text = "Draft";
            if (mode == "readonly") File.SetAttributes(nfo, FileAttributes.ReadOnly); else File.WriteAllText(nfo, "<movie><title>External</title></movie>");
            string expected = File.ReadAllText(nfo); dialog.Show();
            try { typeof(NfoEditorDialog).GetMethod("BtnSave_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { null, new RoutedEventArgs(Button.ClickEvent) });
                Assert(dialog.IsVisible && dialog.SavedMetadata == null, mode + " editor closed or reports saved after rejected write."); Assert(((TextBox)dialog.FindName("txtTitle")).Text == "Draft", "Rejected save lost editor draft.");
                Assert(((TextBlock)dialog.FindName("txtStatus")).Text.Contains(nfo), "Inline save failure does not expose path."); Assert(File.ReadAllText(nfo) == expected, "Rejected draft leaked to file."); }
            finally { File.SetAttributes(nfo, FileAttributes.Normal); dialog.Close(); }
        }
    }
    private static void CommittedProjection()
    {
        string video = Put("projection", "Movie.mp4"); Put("projection", "Movie.nfo", "<movie><title>Old</title></movie>");
        using (var scope = new CaseScope()) { var vm = sharedVm; var e = Add(vm, video, video); e.LoadLocalMetadata(video, Path.Combine(Path.GetDirectoryName(video), "Movie.funscript"), new string[0]); Assert(e.ScriptVariantCount == 0, "Explicit empty script variants changed count during Load."); var metadata = e.CreateMetadataDraft(); metadata.Title = "Committed"; metadata.Save(e.NfoPath); File.WriteAllText(e.NfoPath, "external malformed");
            Assert(vm.Playlist.PublishCommittedMetadata(metadata) == null && e.DisplayTitle == "Committed", "Publication reloaded external file instead of committed snapshot."); Assert(e.Duration == TimeSpan.FromSeconds(42) && e.SelectedScriptName == "chosen-author.funscript" && e.ScriptVariantCount == 0, "Publication reset media state or changed script count."); }
    }
    private static void RefreshFailure()
    {
        string video = Put("refresh-fail", "Movie.mp4"), script = Put("refresh-fail", "Movie-soft.funscript"); Put("refresh-fail", "Movie.nfo", "<movie><favorite>false</favorite></movie>");
        using (var scope = new CaseScope()) { var vm = sharedVm; var first = Add(vm, video, video, script); var other = Add(vm, script, video, script); other.PropertyChanged += (s,e) => { if (e.PropertyName == "IsFavorite") throw new InvalidOperationException("Fixture refresh observer failure"); };
            var control = new PlaylistControl { ViewModel = vm }; Favorite(control, first); Assert(NfoMetadata.Load(video).Favorite && first.IsFavorite, "Refresh failure turned actual saved file into failed commit."); Assert(control.MetadataStatusMessage.Contains("已保存") && control.MetadataStatusMessage.Contains("Fixture refresh"), "Saved-but-refresh-failed status missing."); }
    }
    private static void SharedEditorProjection()
    {
        string video = Put("shared-edit", "Movie.mp4"), script = Put("shared-edit", "Movie-soft.funscript"); Put("shared-edit", "Movie.nfo", "<movie><title>Old</title><originaltitle>Source</originaltitle><tag>OldTag</tag></movie>");
        using (var scope = new CaseScope())
        {
            var vm = sharedVm; var first = Add(vm, video, video, script); var other = Add(vm, script, video, script);
            bool titleNotified = false, tagsNotified = false; other.PropertyChanged += (s,e) => { titleNotified |= e.PropertyName == "DisplayTitle"; tagsNotified |= e.PropertyName == "RawTagItems"; };
            var control = new PlaylistControl { ViewModel = vm }; var host = new Window { Content = control, Width = 640, Height = 480 }; host.Show();
            try {
                host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { var dialog = Application.Current.Windows.OfType<NfoEditorDialog>().Single(); ((TextBox)dialog.FindName("txtTitle")).Text = "Shared edit"; ((TextBox)dialog.FindName("txtTags")).Text = "NewUniqueTag";
                    typeof(NfoEditorDialog).GetMethod("BtnSave_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { null, new RoutedEventArgs(Button.ClickEvent) }); }));
                typeof(PlaylistControl).GetMethod("MnuEditNfo_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { new MenuItem { CommandParameter = first }, new RoutedEventArgs(MenuItem.ClickEvent) });
                Assert(first.DisplayTitle == "Shared edit" && other.DisplayTitle == "Shared edit" && other.RawTagItems.SequenceEqual(new[] { "NewUniqueTag" }), "Editor commit did not update every shared projection.");
                Assert(titleNotified && tagsNotified && vm.Playlist.AvailableTags.Any(tag => tag.RawTags.Contains("NewUniqueTag") && tag.Count == 2) && !vm.Playlist.AvailableTags.Any(tag => tag.RawTags.Contains("OldTag")), "Tag filter counts or Home-facing property notifications stayed stale.");
                Assert(NfoMetadata.Load(video).OriginalTitle == "Source", "Shared editor changed source title.");
            } finally { host.Close(); }
        }
    }
    private static void DeletedExactEditor()
    {
        string script = Put("deleted-exact", "Movie-soft.funscript"), exact = Put("deleted-exact", "Movie-soft.nfo", "<movie><title>Exact</title></movie>");
        string shared = Put("deleted-exact", "Movie.nfo", "<movie><title>Shared must stay</title></movie>"); string sharedBytes = File.ReadAllText(shared);
        using (var scope = new CaseScope())
        {
            var vm = sharedVm; var entry = Add(vm, script, null, script); Assert(entry.NfoPath == exact, "Fixture did not resolve exact sidecar."); File.Delete(exact);
            var control = new PlaylistControl { ViewModel = vm }; var host = new Window { Content = control, Width = 640, Height = 480 }; host.Show();
            bool rejection = false;
            try {
                host.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { var dialog = Application.Current.Windows.OfType<NfoEditorDialog>().Single();
                    ((TextBox)dialog.FindName("txtTitle")).Text = "Draft after deletion";
                    typeof(NfoEditorDialog).GetMethod("BtnSave_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(dialog, new object[] { null, new RoutedEventArgs(Button.ClickEvent) });
                    rejection = dialog.IsVisible && dialog.SavedMetadata == null && ((TextBlock)dialog.FindName("txtStatus")).Text.Contains(exact); dialog.Close(); }));
                typeof(PlaylistControl).GetMethod("MnuEditNfo_OnClick", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(control, new object[] { new MenuItem { CommandParameter = entry }, new RoutedEventArgs(MenuItem.ClickEvent) });
                Assert(rejection && File.ReadAllText(shared) == sharedBytes && !File.Exists(exact), "Deleted exact sidecar silently rebound to shared NFO or recreated stale data.");
            } finally { host.Close(); }
        }
    }
#endif
}
