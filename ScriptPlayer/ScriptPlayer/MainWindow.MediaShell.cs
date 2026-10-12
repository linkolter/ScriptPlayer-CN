using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json;
using ScriptPlayer.Dialogs;
using ScriptPlayer.Shared;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private string currentPage = "home";
        private readonly ObservableCollection<PlaylistEntry> recentMedia = new ObservableCollection<PlaylistEntry>();
        private readonly DispatcherTimer settingsSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        private SettingsViewModel observedSettings;
        private bool pendingPlaybackSettings;
        private HomeMediaViewModel homeMedia;

        private bool shellReady;
        private double previousVolume = 50;
        private bool rememberRecent = true;
        private static string ShellStatePath => Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"), "ScriptPlayer", "MediaShell.json");

        private sealed class MediaShellState
        {
            public bool RememberRecent { get; set; } = true;
            public bool AutoHideControls { get; set; } = true;
            public List<string> RecentFiles { get; set; } = new List<string>();
        }

        private void InitializeMediaShell()
        {
            RecentMediaItems.ItemsSource = recentMedia;
            homeMedia = new HomeMediaViewModel(ViewModel.Playlist);
            FavoriteMediaItems.ItemsSource = homeMedia.Favorites;
            FavoriteShelf.DataContext = homeMedia;
            try
            {
                if (File.Exists(ShellStatePath))
                {
                    var state = JsonConvert.DeserializeObject<MediaShellState>(File.ReadAllText(ShellStatePath));
                    rememberRecent = state?.RememberRecent ?? true;
                    autoHidePlaybackControls = state?.AutoHideControls ?? true;
                    if (rememberRecent)
                        foreach (var file in (state?.RecentFiles ?? new List<string>()).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(24))
                            recentMedia.Add(FindMediaEntry(file));
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            RememberRecentSwitch.IsChecked = rememberRecent;
            AutoHideControlsSwitch.IsChecked = autoHidePlaybackControls;
            InitializePlaybackList();
            InitializeTimeline();
            PlaybackDock.SizeChanged += (s, e) => { UpdateFloatingLayout(); UpdateResponsiveLayout(); };
            VideoOverlayLayer.SizeChanged += (s, e) => UpdateResponsiveLayout();
            ShellRoot.SizeChanged += (s, e) => UpdateResponsiveLayout();
            InitializePlaybackChrome();
            settingsSaveTimer.Tick += SaveCommonSettings;
            ViewModel.PropertyChanged += MediaShellModelChanged;
            AttachSettings();
            shellReady = true;
            NavigateTo(ViewModel.IsFullscreen || !string.IsNullOrEmpty(ViewModel.LoadedMedia) ? "player" : "home");
            UpdateRecentEmpty();
            // Opt in to dark native title bars when supported by Windows.
            try { int dark = ViewModel.Settings.InterfaceTheme == "Light" ? 0 : 1; DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private PlaylistEntry FindMediaEntry(string file)
        {
            return ViewModel.Playlist.Entries.FirstOrDefault(e => string.Equals(e.Fullname, file, StringComparison.OrdinalIgnoreCase)) ?? new PlaylistEntry(file);
        }

        private void AttachSettings()
        {
            settingsSaveTimer.Stop();
            pendingPlaybackSettings = false;
            if (observedSettings != null) observedSettings.PropertyChanged -= CommonSettingsChanged;
            observedSettings = ViewModel.Settings;
            if (unifiedSettingsEditor != null) unifiedSettingsEditor.Settings = observedSettings;
            if (observedSettings != null) observedSettings.PropertyChanged += CommonSettingsChanged;
            ApplyAppearanceSettings();
        }

        private void CommonSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            bool appearance = SettingsViewModel.IsInterfacePreference(e.PropertyName);
            if (appearance) ApplyAppearanceSettings(e.PropertyName);
            else pendingPlaybackSettings = true;
            if (e.PropertyName == nameof(SettingsViewModel.ShowHeatMap)) RefreshTimelinePresentation();
            settingsSaveTimer.Stop();
            settingsSaveTimer.Start();
        }

        private void SaveCommonSettings(object sender, EventArgs e)
        {
            settingsSaveTimer.Stop();
            try
            {
                if (pendingPlaybackSettings) ViewModel.ApplySettings(ViewModel.Settings);
                else ViewModel.SaveSettings();
                pendingPlaybackSettings = false;
            }
            catch (Exception ex) { MessageBox.Show(this, "无法保存设置：" + ex.Message, "ScriptPlayer"); }
        }

        private void MediaShellModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.Settings)) AttachSettings();
            if (e.PropertyName == nameof(MainViewModel.LoadedMedia) && !string.IsNullOrEmpty(ViewModel.LoadedMedia))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!shellReady) return;
                    var file = ViewModel.LoadedMedia;
                    if (!string.IsNullOrWhiteSpace(file) && !ViewModel.Playlist.Entries.Any(x => string.Equals(x.Fullname, file, StringComparison.OrdinalIgnoreCase)))
                        ViewModel.Playlist.AddEntries(false, new[] { file });
                    ViewModel.Playlist.SetCurrentEntry(ViewModel.LoadedFiles);
                    RefreshPlaybackList();
                    if (rememberRecent && !string.IsNullOrWhiteSpace(file))
                    {
                        var existing = recentMedia.FirstOrDefault(x => string.Equals(x.Fullname, file, StringComparison.OrdinalIgnoreCase));
                        if (existing != null) recentMedia.Remove(existing);
                        recentMedia.Insert(0, FindMediaEntry(file));
                        while (recentMedia.Count > 24) recentMedia.RemoveAt(recentMedia.Count - 1);
                        SaveShellState();
                        UpdateRecentEmpty();
                    }
                    NavigateTo("player");
                }));
            }
        }

        private void Navigate_Click(object sender, RoutedEventArgs e) => NavigateTo((string)((FrameworkElement)sender).Tag);
        private void NowPlaying_Click(object sender, RoutedEventArgs e) => NavigateTo("player");

        private void NavigateTo(string page)
        {
            if (!shellReady) return;
            string previousPage = currentPage;
            if (page == "player" && !ViewModel.IsFullscreen && string.IsNullOrEmpty(ViewModel.LoadedMedia) && string.IsNullOrEmpty(ViewModel.LoadedScript)
                && (ViewModel.PlaybackMode == PlaybackMode.Mpv || ViewModel.PlaybackMode == PlaybackMode.Local)) page = "home";
            if (ViewModel.IsFullscreen && page != "player") SetFullscreen(false, true);
            if (currentPage == "settings" && page != "settings") unifiedSettingsEditor?.CommitUnifiedCollections();
            QuickSettingsPopup.IsOpen = false;


            currentPage = page;
            ResetPlaybackChrome();
            if (page == "settings") { EnsureUnifiedSettings(); SettingsContentScroll.ScrollToTop(); }
            HomeNav.IsChecked = page == "home";
            LibraryNav.IsChecked = page == "library";
            NowPlayingNav.IsChecked = page == "player";
            BrowseNavigationLinks.Visibility = page == "player" ? Visibility.Collapsed : Visibility.Visible;
            PlaybackListPanel.Visibility = page == "player" ? Visibility.Visible : Visibility.Collapsed;
            SettingsNav.IsChecked = page == "settings";
            HomePage.Visibility = page == "home" ? Visibility.Visible : Visibility.Collapsed;
            GridPlaylist.Visibility = page == "library" ? Visibility.Visible : Visibility.Collapsed;
            GridSettings.Visibility = page == "settings" ? Visibility.Visible : Visibility.Collapsed;
            BrowsePage.Visibility = page == "player" ? Visibility.Collapsed : Visibility.Visible;
            GridVideo.Visibility = page == "player" ? Visibility.Visible : Visibility.Collapsed;
            PageTitle.Text = Localization.ChineseLocalization.Translate(page == "settings" ? "设置" : page == "library" ? "媒体列表" : "主页");
            GlobalCommandManager.IsEnabled = page != "settings";
            UpdateShellFullscreen();
            RefreshVideoOverlayPosition();
            if (previousPage != page) AnimatePageTransition(BrowsePage);
        }



        private void ShellSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!shellReady) return;
            if (currentPage == "player") { playbackListView?.Refresh(); return; }
            ViewModel.Playlist.Filter = ShellSearch.Text;
            NavigateTo("library");
        }

        private void RecentMedia_Click(object sender, RoutedEventArgs e)
        {
            string file = (string)((Button)sender).Tag;
            if (!File.Exists(file)) { MessageBox.Show(this, "文件已移动或删除，请重新添加。", "找不到媒体"); return; }
            ViewModel.LoadFile(file);
            NavigateTo("player");
        }

        private void HomeShelf_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var shelf = sender as ScrollViewer;
            if (shelf == null || shelf.ScrollableWidth <= 0 || e.Delta == 0) return;
            double target = Math.Max(0, Math.Min(shelf.ScrollableWidth, shelf.HorizontalOffset - e.Delta));
            if (Math.Abs(target - shelf.HorizontalOffset) < 0.1) return;
            shelf.ScrollToHorizontalOffset(target);
            e.Handled = true;
        }

        private void RememberRecent_Click(object sender, RoutedEventArgs e)
        {
            rememberRecent = RememberRecentSwitch.IsChecked == true;
            if (!rememberRecent) recentMedia.Clear();
            SaveShellState();
            UpdateRecentEmpty();
        }

        private void UpdateRecentEmpty() => RecentEmpty.Visibility = recentMedia.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private void SaveShellState()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ShellStatePath));
                File.WriteAllText(ShellStatePath, JsonConvert.SerializeObject(new MediaShellState { RememberRecent = rememberRecent, AutoHideControls = autoHidePlaybackControls, RecentFiles = recentMedia.Select(x => x.Fullname).ToList() }, Formatting.Indented));
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }

        private void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.ContextMenu.DataContext = ViewModel;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            button.ContextMenu.MinWidth = 220;
            button.ContextMenu.HorizontalOffset = Math.Min(0, button.ActualWidth - 220);
            button.ContextMenu.IsOpen = true;
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Volume > 0) { previousVolume = ViewModel.Volume; ViewModel.Volume = 0; }
            else ViewModel.Volume = (int)Math.Max(1, previousVolume);
        }

        private void DisposeMediaShell()
        {
            SavePausedExitPoint();
            homeMedia?.Dispose();
            homeMedia = null;
            DisposeTimeline();
            ViewModel.Playlist.EntriesChanged -= PlaybackEntriesChanged;
            shellReady = false;
            settingsSaveTimer.Stop();
            settingsSaveTimer.Tick -= SaveCommonSettings;
            ViewModel.PropertyChanged -= MediaShellModelChanged;
            settingsSaveTimer.Stop();
            pendingPlaybackSettings = false;
            if (observedSettings != null) observedSettings.PropertyChanged -= CommonSettingsChanged;
            chromeTimer.Stop();
            chromeTimer.Tick -= PlaybackChromeTick;
            PreviewKeyDown -= PlaybackKeyActivity;
            PreviewMouseDown -= PlaybackPointerActivity;
            VideoOverlayLayer.PreviewKeyDown -= PlaybackKeyActivity;
            unifiedSettingsEditor?.ReleaseUnifiedPages();
            settingsSaveTimer.Stop();
            unifiedSettingsEditor?.Close();
            SaveShellState();
            GlobalCommandManager.IsEnabled = true;
        }
    }
}
