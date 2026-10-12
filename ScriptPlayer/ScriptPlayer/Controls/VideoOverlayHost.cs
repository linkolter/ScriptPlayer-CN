using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScriptPlayer.Controls
{
    /// <summary>
    /// Keeps interactive WPF overlays above MPV's native child window. Normal
    /// playback modes and the welcome page remain in the main WPF visual tree.
    /// Unlike Popup, this owned window has explicit client bounds and never
    /// changes monitor placement or becomes a desktop-wide topmost surface.
    /// </summary>
    internal sealed class VideoOverlayHost : IDisposable
    {
        private readonly Window owner;
        private readonly Grid target;
        private readonly FrameworkElement content;
        private readonly Func<bool> needsNativeOverlay;
        private Window overlay;
        private HwndSource source;
        private bool floating, updating, disposed;
        private Rect bounds = Rect.Empty;

        public VideoOverlayHost(Window owner, Grid target, FrameworkElement content, Func<bool> needsNativeOverlay)
        {
            this.owner = owner;
            this.target = target;
            this.content = content;
            this.needsNativeOverlay = needsNativeOverlay;
            owner.LocationChanged += Changed;
            owner.StateChanged += Changed;
            owner.Activated += Changed;
            owner.Deactivated += Changed;
            owner.IsVisibleChanged += VisibilityChanged;
            owner.IsEnabledChanged += VisibilityChanged;
            target.LayoutUpdated += Changed;
            Refresh();
        }

        public bool IsActive => owner.IsActive || overlay?.IsActive == true;

        public void Refresh()
        {
            if (disposed || updating) return;
            updating = true;
            try
            {
                bool required = needsNativeOverlay();
                if (required && !floating)
                {
                    EnsureWindow();
                    target.Children.Remove(content);
                    overlay.Content = content;
                    floating = true;
                }
                else if (!required && floating)
                {
                    overlay.Hide();
                    overlay.Content = null;
                    target.Children.Add(content);
                    floating = false;
                }
                if (!floating) return;
                if (!owner.IsVisible || !owner.IsEnabled || owner.WindowState == WindowState.Minimized ||
                    !target.IsVisible || target.ActualWidth < 1 || target.ActualHeight < 1)
                {
                    overlay.Hide();
                    bounds = Rect.Empty;
                    return;
                }

                var topLeft = target.PointToScreen(new Point());
                var bottomRight = target.PointToScreen(new Point(target.ActualWidth, target.ActualHeight));
                var nextBounds = new Rect(topLeft, bottomRight);
                if (nextBounds != bounds || !overlay.IsVisible)
                {
                    // Position in physical screen pixels so DPI changes, negative
                    // monitor coordinates and fullscreen use the same rectangle.
                    var transform = source.CompositionTarget.TransformFromDevice;
                    var size = transform.Transform(new Vector(nextBounds.Width, nextBounds.Height));
                    overlay.Width = size.X;
                    overlay.Height = size.Y;
                    overlay.Topmost = owner.Topmost;
                    SetWindowPos(source.Handle, IntPtr.Zero, (int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y),
                        (int)Math.Round(nextBounds.Width), (int)Math.Round(nextBounds.Height), 0x0014);
                    if (!overlay.IsVisible) overlay.Show();
                    // Show may apply WPF's cached placement; assert the same
                    // physical bounds once more after creating its visible HWND.
                    SetWindowPos(source.Handle, IntPtr.Zero, (int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y),
                        (int)Math.Round(nextBounds.Width), (int)Math.Round(nextBounds.Height), 0x0014);
                    bounds = nextBounds;
                }
            }
            finally { updating = false; }
        }

        private void EnsureWindow()
        {
            if (overlay != null) return;
            overlay = new Window
            {
                Owner = owner, Title = "ScriptPlayer 视频字幕层", WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize, AllowsTransparency = true,
                Background = Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false,
                Focusable = false, Width = 1, Height = 1, DataContext = owner.DataContext
            };
            var handle = new WindowInteropHelper(overlay).EnsureHandle();
            source = HwndSource.FromHwnd(handle);
            source.AddHook(WindowMessage);
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Clicking subtitles/notifications must keep keyboard focus on the
            // player; an owned connection dialog can then open without flicker.
            if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
            return IntPtr.Zero;
        }

        private void Changed(object sender, EventArgs e) => Refresh();
        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Refresh();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            owner.LocationChanged -= Changed;
            owner.StateChanged -= Changed;
            owner.Activated -= Changed;
            owner.Deactivated -= Changed;
            owner.IsVisibleChanged -= VisibilityChanged;
            owner.IsEnabledChanged -= VisibilityChanged;
            target.LayoutUpdated -= Changed;
            if (source != null && !source.IsDisposed) source.RemoveHook(WindowMessage);
            if (overlay != null) { overlay.Content = null; overlay.Close(); }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
