using System;
using System.Threading;
using ScriptPlayer.Shared.Controls;

namespace ScriptPlayer.Shared
{
    public sealed class MpvTimeSource : TimeSource, IDisposable
    {
        private readonly MpvController _controller;
        private readonly ISampleClock _clock;
        private readonly ManualResetEvent _stop = new ManualResetEvent(false);
        private readonly Thread _samplingThread;
        private MpvPlaybackSample _latest;
        private int _disposed;
        public event EventHandler<MpvPlaybackSample> RealtimeSampled;
        public MpvPlaybackSample LatestSample => Volatile.Read(ref _latest);
        public MpvPlaybackSample ReadPlaybackSample() => _controller.ReadPlaybackSample();
        public bool TryLoop(MpvPlaybackSample sample, TimeSpan start) => _controller.TryLoop(sample.Revision, sample.MediaGeneration, start);

        public MpvTimeSource(MpvController controller, ISampleClock clock)
        {
            _controller = controller;
            _clock = clock;
            _clock.Tick += ClockOnTick;
            _samplingThread = new Thread(SampleLoop) { IsBackground = true, Name = "ScriptPlayer Media Clock" };
            _samplingThread.Start();
        }

        private void SampleLoop()
        {
            while (!_stop.WaitOne(10))
            {
                try
                {
                    if (!_controller.IsInitialized) continue;
                    var sample = _controller.ReadPlaybackSample();
                    Volatile.Write(ref _latest, sample);
                    RealtimeSampled?.Invoke(this, sample);
                }
                catch (Exception error) { System.Diagnostics.Trace.WriteLine("Media clock: " + error); }
            }
        }

        public override string Name => "MPV 内嵌播放";
        public override bool ShowBanner => false;
        public override string ConnectInstructions => string.Empty;

        public string HardwareDecoder => _controller.HardwareDecoder;
        public string VideoCodec => _controller.VideoCodec;

        private void ClockOnTick(object sender, EventArgs e)
        {
            if (!_controller.IsInitialized)
            {
                IsConnected = false;
                return;
            }

            IsConnected = true;
            var sample = _controller.ReadPlaybackSample();
            if (sample.Duration > TimeSpan.Zero) Duration = sample.Duration;
            Progress = sample.Position;
            // A native seek suspends realtime output, but does not mean the user paused playback.
            IsPlaying = !_controller.IsPaused && sample.Duration > TimeSpan.Zero;
        }

        public override double PlaybackRate
        {
            get => _controller.Speed;
            set
            {
                _controller.Speed = value;
                OnPropertyChanged();
                OnPlaybackRateChanged(value);
            }
        }

        public override bool CanPlayPause => true;
        public override bool CanSeek => true;
        public override bool CanOpenMedia => true;

        public override void Play()
        {
            _controller.Play();
            IsPlaying = true;
        }

        public override void Pause()
        {
            _controller.Pause();
            IsPlaying = false;
        }

        public override void SetPosition(TimeSpan position)
        {
            _controller.Position = Math.Max(0, position.TotalSeconds);
            Progress = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _clock.Tick -= ClockOnTick;
            (_clock as IDisposable)?.Dispose();
            _stop.Set();
            _samplingThread.Join();
            RealtimeSampled = null;
            _stop.Dispose();
        }
    }
}
