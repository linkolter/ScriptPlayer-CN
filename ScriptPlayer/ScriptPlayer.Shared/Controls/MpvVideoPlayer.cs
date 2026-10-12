using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using ScriptPlayer.Shared.Controls;

namespace ScriptPlayer.Shared
{
    public sealed class MpvVideoPlayer : HwndHost, IDisposable
    {
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsClipChildren = 0x02000000;

        private readonly MpvController _controller = new MpvController();
        private readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();
        private TaskCompletionSource<bool> _openCompleted;
        private IntPtr _hostWindow;
        private bool _disposed;

        public event EventHandler MediaOpened;
        public event EventHandler MediaEnded;

        public MpvVideoPlayer()
        {
            TimeSource = new MpvTimeSource(_controller,
                new DispatcherClock(Dispatcher, TimeSpan.FromMilliseconds(10)));
            _controller.FileLoaded += ControllerOnFileLoaded;
            _controller.FileEnded += ControllerOnFileEnded;
            _controller.FileFailed += ControllerOnFileFailed;
        }

        public TimeSource TimeSource { get; }
        public string OpenedFile { get; private set; }
        public bool IsRuntimeAvailable => MpvController.IsRuntimeAvailable;
        public string RuntimePath => MpvController.RuntimePath;

        public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
            nameof(Volume), typeof(double), typeof(MpvVideoPlayer),
            new PropertyMetadata(100.0, OnVolumeChanged));

        public double Volume
        {
            get => (double)GetValue(VolumeProperty);
            set => SetValue(VolumeProperty, value);
        }

        private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MpvVideoPlayer player = (MpvVideoPlayer)d;
            if (player._controller.IsInitialized)
                player._controller.Volume = (double)e.NewValue;
        }

        public async Task Open(string filename, TimeSpan startAt)
        {
            await _ready.Task;
            OpenedFile = filename;
            _openCompleted?.TrySetCanceled();
            _openCompleted = new TaskCompletionSource<bool>();
            var pending = _openCompleted;
            _controller.Open(filename, startAt);
            if (await Task.WhenAny(pending.Task, Task.Delay(TimeSpan.FromSeconds(30))) != pending.Task)
                pending.TrySetException(new TimeoutException("媒体加载超时。"));
            await pending.Task;
        }

        private void ControllerOnFileLoaded(object sender, string filename)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!string.Equals(filename, OpenedFile, StringComparison.OrdinalIgnoreCase)) return;
                _controller.Volume = Volume;
                _openCompleted?.TrySetResult(true);
                MediaOpened?.Invoke(this, EventArgs.Empty);
            }));
        }

        private void ControllerOnFileEnded(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => MediaEnded?.Invoke(this, EventArgs.Empty)));
        }

        private void ControllerOnFileFailed(object sender, string message)
        {
            Dispatcher.BeginInvoke(new Action(() => _openCompleted?.TrySetException(new InvalidOperationException(message))));
        }

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _hostWindow = CreateWindowEx(0, "static", string.Empty,
                WsChild | WsVisible | WsClipChildren, 0, 0, 1, 1,
                hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            if (_hostWindow == IntPtr.Zero)
            {
                Win32Exception error = new Win32Exception(Marshal.GetLastWin32Error());
                _ready.TrySetException(error);
                throw error;
            }

            try
            {
                _controller.Initialize(_hostWindow);
                _controller.Volume = Volume;
                _ready.TrySetResult(true);
            }
            catch (Exception error)
            {
                _ready.TrySetException(error);
            }

            return new HandleRef(this, _hostWindow);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            if (hwnd.Handle != IntPtr.Zero)
                DestroyWindow(hwnd.Handle);
            _hostWindow = IntPtr.Zero;
        }

        public new void Dispose()
        {
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                _controller.FileLoaded -= ControllerOnFileLoaded;
                _controller.FileEnded -= ControllerOnFileEnded;
                _controller.FileFailed -= ControllerOnFileFailed;
                _openCompleted?.TrySetCanceled();
                (TimeSource as IDisposable)?.Dispose();
                _controller.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(int exStyle, string className, string windowName,
            int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu,
            IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hwnd);
    }
}
