using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ScriptPlayer.Shared.Controls
{
    public sealed class MpvController : IDisposable
    {
        private IntPtr _handle;
        private Thread _eventThread;
        private volatile bool _disposed;
        private string _pendingFile;
        private TimeSpan _pendingStart;
        private volatile bool _pauseRequested;
        private readonly object _nativeGate = new object();
        private long _revision, _mediaGeneration;
        private string _loadedFile;
        private volatile bool _initialized;
        private bool _seekPending;

        public MpvPlaybackSample ReadPlaybackSample()
        {
            lock (_nativeGate)
                return new MpvPlaybackSample(_revision, _mediaGeneration, _loadedFile,
                    Position, Duration, Speed, !_pauseRequested && !_seekPending && !IsPaused);
        }
        public bool TryLoop(long revision, long generation, TimeSpan start)
        {
            lock (_nativeGate)
            {
                if (!IsInitialized || _revision != revision || _mediaGeneration != generation || _pauseRequested || IsPaused) return false;
                Position = start.TotalSeconds;
                return true;
            }
        }

        public event EventHandler<string> FileLoaded;
        public event EventHandler FileEnded;
        public event EventHandler PlaybackRestarted;
        public event EventHandler<string> FileFailed;

        public bool IsInitialized => _initialized && _handle != IntPtr.Zero && !_disposed;

        public static string RuntimePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libmpv-2.dll");
        public static bool IsRuntimeAvailable => File.Exists(RuntimePath);

        public void Initialize(IntPtr windowHandle)
        {
            if (IsInitialized)
                return;
            if (!IsRuntimeAvailable)
                throw new FileNotFoundException("未找到内嵌 MPV 运行库 libmpv-2.dll。", RuntimePath);

            _handle = MpvNative.mpv_create();
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("无法创建 MPV 播放器实例。");

            try
            {
                uint hwnd = unchecked((uint)windowHandle.ToInt64());
                SetOption("wid", hwnd.ToString(CultureInfo.InvariantCulture));
                SetOption("config", "no");
                SetOption("load-scripts", "no");
                SetOption("input-default-bindings", "no");
                SetOption("input-cursor", "no");
                SetOption("osc", "no");
                SetOption("terminal", "no");
                SetOption("idle", "yes");
                SetOption("keep-open", "yes");
                SetOption("profile", "fast");
                SetOption("vo", "gpu-next");
                SetOption("gpu-api", "d3d11");
                SetOption("gpu-context", "d3d11");
                SetOption("hwdec", "auto-safe");
                SetOption("hwdec-codecs", "all");
                SetOption("video-sync", "audio");
                SetOption("audio-client-name", "ScriptPlayer 中文版");

                int result = MpvNative.mpv_initialize(_handle);
                if (result < 0)
                    throw new InvalidOperationException("MPV 初始化失败，错误代码：" + result);
                _initialized = true;

                _eventThread = new Thread(EventLoop)
                {
                    IsBackground = true,
                    Name = "ScriptPlayer MPV Events"
                };
                _eventThread.Start();
            }
            catch
            {
                MpvNative.mpv_terminate_destroy(_handle);
                _handle = IntPtr.Zero;
                throw;
            }
        }

        public void Open(string filename, TimeSpan startAt)
        {
            EnsureInitialized();
            if (!File.Exists(filename))
                throw new FileNotFoundException("找不到视频文件。", filename);

            lock (_nativeGate)
            {
                _revision++; _mediaGeneration++; _loadedFile = null; _seekPending = false;
                _pendingFile = filename;
                _pendingStart = startAt < TimeSpan.Zero ? TimeSpan.Zero : startAt;
                _pauseRequested = false;
                Command("loadfile", filename, "replace");
            }
        }

        public void Play()
        {
            lock (_nativeGate)
            {
                _revision++; _pauseRequested = false;
                if (GetFlag("eof-reached", false)) Position = 0;
                SetFlag("pause", false);
            }
        }

        public void Pause()
        {
            lock (_nativeGate)
            {
                _revision++; _pauseRequested = true;
                SetFlag("pause", true);
            }
        }

        public double Position
        {
            get => GetDouble("time-pos");
            set { lock (_nativeGate) { _revision++; _seekPending = IsInitialized; SetDouble("time-pos", Math.Max(0, value)); } }
        }

        public double Duration => GetDouble("duration");

        public double Speed
        {
            get
            {
                double value = GetDouble("speed");
                return value <= 0 ? 1.0 : value;
            }
            set { lock (_nativeGate) { _revision++; SetDouble("speed", Math.Max(0.05, value)); } }
        }

        public double Volume
        {
            get => GetDouble("volume");
            set => SetDouble("volume", Math.Max(0, Math.Min(100, value)));
        }

        public bool IsPaused => GetFlag("pause", true);
        public string HardwareDecoder => GetString("hwdec-current");
        public string VideoCodec => GetString("video-codec");

        private void EventLoop()
        {
            while (!_disposed && _handle != IntPtr.Zero)
            {
                try
                {
                    IntPtr eventPointer = MpvNative.mpv_wait_event(_handle, 0.1);
                    if (eventPointer == IntPtr.Zero)
                        continue;

                    MpvEvent mpvEvent = Marshal.PtrToStructure<MpvEvent>(eventPointer);
                    switch (mpvEvent.EventId)
                    {
                        case MpvEventId.FileLoaded:
                            lock (_nativeGate)
                            {
                            string loadedPath = GetString("path");
                            if (!string.Equals(loadedPath, _pendingFile, StringComparison.OrdinalIgnoreCase)) break;
                            if (_pendingStart > TimeSpan.Zero)
                                Position = _pendingStart.TotalSeconds;
                            SetFlag("pause", _pauseRequested);
                            _loadedFile = loadedPath;
                            FileLoaded?.Invoke(this, loadedPath);
                            }
                            break;
                        case MpvEventId.EndFile:
                            if (mpvEvent.Data != IntPtr.Zero)
                            {
                                MpvEventEndFile endFile = Marshal.PtrToStructure<MpvEventEndFile>(mpvEvent.Data);
                                if (endFile.Reason == MpvEndFileReason.Eof)
                                    FileEnded?.Invoke(this, EventArgs.Empty);
                                else if (endFile.Reason == MpvEndFileReason.Error)
                                    FileFailed?.Invoke(this, "MPV 无法解码或读取该文件。");
                            }
                            break;
                        case MpvEventId.PlaybackRestart:
                            lock (_nativeGate) _seekPending = false;
                            PlaybackRestarted?.Invoke(this, EventArgs.Empty);
                            break;
                        case MpvEventId.Shutdown:
                            return;
                    }
                }
                catch
                {
                    if (_disposed)
                        return;
                }
            }
        }

        private void SetOption(string name, string value)
        {
            IntPtr nativeName = ToUtf8(name);
            IntPtr nativeValue = ToUtf8(value);
            try
            {
                int result = MpvNative.mpv_set_option_string(_handle, nativeName, nativeValue);
                if (result < 0)
                    throw new InvalidOperationException($"MPV 选项 {name} 设置失败，错误代码：{result}");
            }
            finally
            {
                Marshal.FreeHGlobal(nativeName);
                Marshal.FreeHGlobal(nativeValue);
            }
        }

        private void Command(params string[] arguments)
        {
            lock (_nativeGate) CommandCore(arguments);
        }
        private void CommandCore(params string[] arguments)
        {
            EnsureInitialized();
            List<IntPtr> nativeStrings = new List<IntPtr>();
            IntPtr nativeArray = IntPtr.Zero;
            try
            {
                foreach (string argument in arguments)
                    nativeStrings.Add(ToUtf8(argument));

                nativeArray = Marshal.AllocHGlobal(IntPtr.Size * (nativeStrings.Count + 1));
                for (int i = 0; i < nativeStrings.Count; i++)
                    Marshal.WriteIntPtr(nativeArray, i * IntPtr.Size, nativeStrings[i]);
                Marshal.WriteIntPtr(nativeArray, nativeStrings.Count * IntPtr.Size, IntPtr.Zero);

                int result = MpvNative.mpv_command(_handle, nativeArray);
                if (result < 0)
                    throw new InvalidOperationException("MPV 命令执行失败，错误代码：" + result);
            }
            finally
            {
                if (nativeArray != IntPtr.Zero)
                    Marshal.FreeHGlobal(nativeArray);
                foreach (IntPtr nativeString in nativeStrings)
                    Marshal.FreeHGlobal(nativeString);
            }
        }

        private double GetDouble(string property)
        {
            lock (_nativeGate) return GetDoubleCore(property);
        }
        private double GetDoubleCore(string property)
        {
            if (!IsInitialized) return 0;
            IntPtr name = ToUtf8(property);
            try
            {
                return MpvNative.mpv_get_property(_handle, name, MpvFormat.Double, out double value) < 0
                    ? 0
                    : value;
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }
        }

        private void SetDouble(string property, double value)
        {
            lock (_nativeGate) SetDoubleCore(property, value);
        }
        private void SetDoubleCore(string property, double value)
        {
            if (!IsInitialized) return;
            IntPtr name = ToUtf8(property);
            try
            {
                MpvNative.mpv_set_property(_handle, name, MpvFormat.Double, ref value);
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }
        }

        private bool GetFlag(string property, bool fallback)
        {
            lock (_nativeGate) return GetFlagCore(property, fallback);
        }
        private bool GetFlagCore(string property, bool fallback)
        {
            if (!IsInitialized) return fallback;
            IntPtr name = ToUtf8(property);
            try
            {
                return MpvNative.mpv_get_property(_handle, name, MpvFormat.Flag, out int value) < 0
                    ? fallback
                    : value != 0;
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }
        }

        private void SetFlag(string property, bool value)
        {
            lock (_nativeGate) SetFlagCore(property, value);
        }
        private void SetFlagCore(string property, bool value)
        {
            if (!IsInitialized) return;
            int flag = value ? 1 : 0;
            IntPtr name = ToUtf8(property);
            try
            {
                MpvNative.mpv_set_property(_handle, name, MpvFormat.Flag, ref flag);
            }
            finally
            {
                Marshal.FreeHGlobal(name);
            }
        }

        private string GetString(string property)
        {
            lock (_nativeGate) return GetStringCore(property);
        }
        private string GetStringCore(string property)
        {
            if (!IsInitialized) return string.Empty;
            IntPtr name = ToUtf8(property);
            IntPtr value = IntPtr.Zero;
            try
            {
                value = MpvNative.mpv_get_property_string(_handle, name);
                return value == IntPtr.Zero ? string.Empty : FromUtf8(value);
            }
            finally
            {
                Marshal.FreeHGlobal(name);
                if (value != IntPtr.Zero)
                    MpvNative.mpv_free(value);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("MPV 播放器尚未初始化。");
        }

        private static IntPtr ToUtf8(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes((value ?? string.Empty) + "\0");
            IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return pointer;
        }

        private static string FromUtf8(IntPtr pointer)
        {
            int length = 0;
            while (Marshal.ReadByte(pointer, length) != 0)
                length++;
            byte[] bytes = new byte[length];
            Marshal.Copy(pointer, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_eventThread != null && _eventThread.IsAlive)
                _eventThread.Join(TimeSpan.FromSeconds(1));

            lock (_nativeGate)
            {
                IntPtr handle = _handle;
                _handle = IntPtr.Zero;
                if (handle != IntPtr.Zero)
                    MpvNative.mpv_terminate_destroy(handle);
            }
        }
    }
}
