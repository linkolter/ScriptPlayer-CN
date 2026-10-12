using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private readonly DispatcherTimer chromeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        private DateTime lastPlaybackActivity = DateTime.UtcNow;
        private DateTime lastNavigationActivity = DateTime.MinValue;
        private DateTime lastQuickSettingsActivity = DateTime.MinValue;
        private bool playbackChromeHidden;
        private bool navigationHidden;
        private bool wasPlaying;
        private bool keyboardChromeFocus;
        private DateTime playbackEntryTime;
        private bool PlaybackWindowIsActive => IsActive || _videoOverlayHost?.IsActive == true;
        private bool edgeQuickSettings;
        private bool autoHidePlaybackControls = true;
        private CursorPoint previousCursor;
        private static readonly TimeSpan EdgeHideDelay = TimeSpan.FromMilliseconds(1400);
        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint { public int X; public int Y; }
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out CursorPoint point);

        private void InitializePlaybackChrome()
        {
            chromeTimer.Tick += PlaybackChromeTick;
            chromeTimer.Start();
            PreviewKeyDown += PlaybackKeyActivity;
            PreviewMouseDown += PlaybackPointerActivity;
            VideoOverlayLayer.PreviewKeyDown += PlaybackKeyActivity;
            QuickSettingsPopup.Closed += (s, e) => { edgeQuickSettings = false; lastPlaybackActivity = DateTime.UtcNow; };
        }

        private void ResetPlaybackChrome()
        {
            QuickSettingsPopup.IsOpen = false;
            wasPlaying = ViewModel.PlaybackState.IsPlaying;
            navigationHidden = currentPage == "player" && autoHidePlaybackControls;
            playbackEntryTime = DateTime.UtcNow;
            playbackChromeHidden = navigationHidden;
            lastNavigationActivity = DateTime.MinValue;
            lastPlaybackActivity = DateTime.UtcNow;
            // A click on a home tile must not immediately reopen controls at the old cursor position.
            GetCursorPos(out previousCursor);
        }

        private void PlaybackKeyActivity(object sender, KeyEventArgs e)
        {
            if (InlineDialogService.IsOpen) return;
            if (e.Key == Key.Escape && QuickSettingsPopup.IsOpen) { QuickSettingsPopup.IsOpen = false; e.Handled = true; return; }
            if (e.Key == Key.Tab) keyboardChromeFocus = true;
            if (e.Key == Key.Tab || e.Key == Key.Space || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Up || e.Key == Key.Down)
                RevealPlaybackChrome();
        }

        private void PlaybackPointerActivity(object sender, MouseButtonEventArgs e)
        {
            if (currentPage == "player") ProcessPlaybackPointer(e.GetPosition(ShellRoot), true, DateTime.UtcNow);
        }

        private void PlaybackChromeTick(object sender, EventArgs e)
        {
            if (!shellReady) return;
            _videoOverlayHost?.Refresh();
            var now = DateTime.UtcNow;
            // MPV uses a native video surface, so WPF MouseMove alone misses video-area movement.
            if (GetCursorPos(out var cursor))
            {
                bool moved = cursor.X != previousCursor.X || cursor.Y != previousCursor.Y;
                previousCursor = cursor;
                if (PlaybackWindowIsActive)
                {
                    ProcessPlaybackPointer(ShellRoot.PointFromScreen(new Point(cursor.X, cursor.Y)), moved, now);
                    if (QuickSettingsPopup.IsOpen && QuickSettingsPopup.Child is FrameworkElement child)
                    {
                        var p = child.PointFromScreen(new Point(cursor.X, cursor.Y));
                        if (p.X >= 0 && p.Y >= 0 && p.X <= child.ActualWidth && p.Y <= child.ActualHeight)
                            lastQuickSettingsActivity = now;
                    }
                }
            }
            EvaluatePlaybackChrome(now);
        }

        private void ProcessPlaybackPointer(Point point, bool moved, DateTime now)
        {
            if (currentPage != "player" || !autoHidePlaybackControls || InlineDialogService.IsOpen) return;
            if (moved) keyboardChromeFocus = false;
            double width = ShellRoot.ActualWidth, height = ShellRoot.ActualHeight;
            if (point.X < 0 || point.Y < 0 || point.X >= width || point.Y >= height) return;
            if (Mouse.Captured != null) return;
            const double edge = 24;
            double heatHeight = playbackChromeHidden ? PlaybackTimeline.PersistentHeight : PlaybackTimeline.ActualHeight;
            // Keep the persistent seek strip stable and clickable while the transport is hidden.
            if (playbackChromeHidden && point.Y >= height - heatHeight
                && point.Y < height - PlaybackTimeline.BottomRevealHeight) return;
            // Bottom corners belong to the transport; a single edge never reveals all panels.
            double railWidth = NavigationRail.ActualWidth;
            bool bottom = (navigationHidden || point.X >= railWidth) && point.Y >= height - (playbackChromeHidden ? PlaybackTimeline.BottomRevealHeight : PlayerControls.ActualHeight + heatHeight);
            bool left = !bottom && point.X <= (navigationHidden ? edge : railWidth);
            bool right = !bottom && !left && point.X >= width - edge;
            bool changed = false;
            if (bottom && (moved || !playbackChromeHidden))
            {
                lastPlaybackActivity = now;
                changed |= playbackChromeHidden;
                playbackChromeHidden = false;
            }
            if (left && (moved || !navigationHidden))
            {
                lastNavigationActivity = now;
                changed |= navigationHidden;
                navigationHidden = false;
            }
            if (right && (moved || QuickSettingsPopup.IsOpen))
            {
                lastQuickSettingsActivity = now;
                if (!QuickSettingsPopup.IsOpen)
                {
                    edgeQuickSettings = true;
                    QuickSettingsPopup.DataContext = ViewModel;
                    UpdateFloatingLayout();
                    QuickSettingsPopup.IsOpen = true;
                }
            }
            if (changed) UpdateShellFullscreen();
        }

        private void EvaluatePlaybackChrome(DateTime now)
        {
            bool playing = ViewModel.PlaybackState.IsPlaying;
            if (currentPage != "player" || !autoHidePlaybackControls)
            {
                wasPlaying = playing;
                if (navigationHidden || playbackChromeHidden)
                {
                    navigationHidden = playbackChromeHidden = false;
                    UpdateShellFullscreen();
                }
                return;
            }
            bool changed = false;
            wasPlaying = playing;
            bool busy = ViewModel.PlaybackState.IsSeeking || Mouse.Captured != null || InlineDialogService.IsOpen;
            bool holdNavigation = busy || NavigationRail.IsMouseOver || (keyboardChromeFocus && NavigationRail.IsKeyboardFocusWithin);
            bool holdTransport = busy || PlayerControls.IsMouseOver || PlaybackTimeline.IsMouseOver
                || (keyboardChromeFocus && (PlayerControls.IsKeyboardFocusWithin || PlaybackTimeline.IsKeyboardFocusWithin))
                || (QuickSettingsPopup.IsOpen && !edgeQuickSettings);
            bool holdSettings = busy || QuickSettingsPopup.IsMouseOver || (keyboardChromeFocus && QuickSettingsPopup.IsKeyboardFocusWithin);
            // Each region owns its visibility. Reading the list must not pin an unrelated transport.
            if (!holdNavigation && !navigationHidden && now - lastNavigationActivity > EdgeHideDelay)
                { navigationHidden = true; changed = true; }
            if (!holdTransport && playing && !playbackChromeHidden && now - lastPlaybackActivity > EdgeHideDelay)
                { playbackChromeHidden = true; changed = true; }
            if (!holdSettings && edgeQuickSettings && QuickSettingsPopup.IsOpen && (!PlaybackWindowIsActive || now - lastQuickSettingsActivity > EdgeHideDelay))
                QuickSettingsPopup.IsOpen = false;
            if (!playing && playbackChromeHidden && now - playbackEntryTime > TimeSpan.FromSeconds(2))
            { playbackChromeHidden = false; changed = true; }
            if (changed) UpdateShellFullscreen();
        }

        private void RevealPlaybackChrome()
        {
            lastPlaybackActivity = DateTime.UtcNow;
            if (!playbackChromeHidden) return;
            playbackChromeHidden = false;
            UpdateShellFullscreen();
        }

        private void UpdateShellFullscreen()
        {
            bool showRail = !navigationHidden && (!ViewModel.IsFullscreen || (currentPage == "player" && autoHidePlaybackControls));
            NavigationRail.Visibility = showRail ? Visibility.Visible : Visibility.Collapsed;
            PlayerControls.Visibility = playbackChromeHidden ? Visibility.Collapsed : Visibility.Visible;
            PlaybackTimeline.TransportVisible = !playbackChromeHidden;
            PlaybackTimeline.Compact = currentPage != "player";
            ShellRoot.ColumnDefinitions[0].Width = new GridLength(showRail ? 260 : 0);
            bool inPlayer = currentPage == "player";
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(PlaybackDock) as System.Windows.Controls.Panel;
            var destination = inPlayer ? VideoOverlayLayer : ShellRoot;
            if (parent != destination)
            {
                parent?.Children.Remove(PlaybackDock);
                destination.Children.Add(PlaybackDock);
            }
            var flyoutParent = System.Windows.Media.VisualTreeHelper.GetParent(QuickSettingsPopup) as System.Windows.Controls.Panel;
            if (flyoutParent != destination) { flyoutParent?.Children.Remove(QuickSettingsPopup); destination.Children.Add(QuickSettingsPopup); }
            System.Windows.Controls.Grid.SetRow(PlaybackDock, inPlayer ? 0 : 2);
            ShellRoot.RowDefinitions[2].Height = inPlayer ? new GridLength(0) : GridLength.Auto;
            ShellRoot.ColumnDefinitions[0].Width = new GridLength(showRail ? (currentPage == "settings" ? 200 : 260) : 0);
            UpdateVideoLayout();
            RefreshVideoOverlayPosition();
            UpdateFloatingLayout();
            UpdateResponsiveLayout();
        }

        private void PlaybackDock_MouseDown(object sender, MouseButtonEventArgs e) => e.Handled = true;

        private void CloseQuickSettings_Click(object sender, RoutedEventArgs e) => QuickSettingsPopup.IsOpen = false;

        private void QuickSettings_Click(object sender, RoutedEventArgs e)
        {
            edgeQuickSettings = false;
            QuickSettingsPopup.DataContext = ViewModel;
            QuickSettingsPopup.IsOpen = !QuickSettingsPopup.IsOpen;
            if (QuickSettingsPopup.IsOpen) RevealPlaybackChrome();
            UpdateFloatingLayout();
        }

        private void UpdateFloatingLayout()
        {
            if (QuickSettingsPopup == null || PlaybackDock == null) return;
            double dockHeight = (playbackChromeHidden ? 0 : PlayerControls.ActualHeight) + PlaybackTimeline.ActualHeight;
            if (!playbackChromeHidden && dockHeight < PlayerControls.Height) dockHeight = PlayerControls.Height + 28;
            QuickSettingsPopup.Margin = new Thickness(12, 12, 12, dockHeight + 12);
            QuickSettingsPopup.Width = Math.Max(240, Math.Min(380, ShellRoot.ActualWidth - 32));
            QuickSettingsScroller.MaxHeight = Math.Max(100, ShellRoot.ActualHeight - dockHeight - 100);
        }

        private void UpdateResponsiveLayout()
        {
            if (TransportLayout == null) return;
            // Use the actual parent viewport, including an expanded playback list.
            // Child DesiredSize must not enlarge the dock beyond the native overlay.
            var viewport = System.Windows.Media.VisualTreeHelper.GetParent(PlaybackDock) as FrameworkElement;
            double available = viewport?.ActualWidth ?? PlaybackDock.ActualWidth;
            if (available > 0 && (double.IsNaN(PlaybackDock.Width) || Math.Abs(PlaybackDock.Width - available) > .5))
                PlaybackDock.Width = available;
            bool narrow = available > 0 && available < 1050;
            MediaInfoLayout.ColumnDefinitions[0].Width = new GridLength(narrow ? 0 : 76);
            TransportThumbnail.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
            TransportVolume.Width = narrow ? 60 : 90;
            bool compact = available > 0 && available < 720;
            ShuffleTransport.Visibility = RepeatTransport.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            TransportVolume.Visibility = available > 0 && available < 580 ? Visibility.Collapsed : Visibility.Visible;
            // Three 40px targets, fullscreen spacing, and the visible volume slider.
            TransportLayout.ColumnDefinitions[2].MinWidth = 3 * 40 + 12
                + (TransportVolume.Visibility == Visibility.Visible ? TransportVolume.Width : 0);
            MediaInfoButton.Visibility = available > 0 && available < 480 ? Visibility.Collapsed : Visibility.Visible;
            BrowsePage.Margin = new Thickness(ShellRoot.ActualWidth < 1050 ? 16 : 24, 20, ShellRoot.ActualWidth < 1050 ? 16 : 24, 20);
            UpdateSettingsLayout();
        }

        private void AllSettings_Click(object sender, RoutedEventArgs e)
        {
            QuickSettingsPopup.IsOpen = false;
            NavigateTo("settings");
        }

        private void ResetQuickSettings_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.ScriptDelay = TimeSpan.Zero;
            ViewModel.Settings.AudioDelay = TimeSpan.Zero;
            if (ViewModel.TimeSource != null) ViewModel.Playback.SetRate(1);
        }
    }
}
