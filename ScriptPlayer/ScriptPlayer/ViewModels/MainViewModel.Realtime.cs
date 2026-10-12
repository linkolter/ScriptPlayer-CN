using System;
using System.Linq;
using System.Threading;
using System.Windows;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;

namespace ScriptPlayer.ViewModels
{
    public partial class MainViewModel
    {
        public RealtimePlaybackService RealtimePlayback { get; private set; }
        bool realtimeDisposed;
        bool realtimeScriptUpdating;
        int realtimeErrorPending;
        void AttachRealtimePlayback()
        {
            RealtimePlayback?.Dispose(); RealtimePlayback = null;
            if (realtimeDisposed || !(TimeSource is MpvTimeSource source)) return;
            var outputs = _devices.Select(device => (IPlaybackOutput)new PlaybackDeviceOutput(device)).ToList();
            outputs.Add(new TCodePlaybackOutput(_tCodeController));
            RealtimePlayback = new RealtimePlaybackService(source, outputs.ToArray());
            RealtimePlayback.OutputFailed += RealtimeOutputFailed;
            RefreshRealtimePlayback();
        }
        void RefreshRealtimePlayback()
        {
            if (RealtimePlayback == null || realtimeDisposed) return;
            var state = PlaybackState;
            if (_loading || realtimeScriptUpdating || _scriptHandler == null || Settings == null ||
                state.Status != PlaybackStatus.Ready || state.IsSeeking || state.MediaPath == null)
            {
                RealtimePlayback.Configure(null, 0, null); return;
            }
            var transform = new PositionTransform(_minScriptPosition, _maxScriptPosition,
                Settings.MinPosition, Settings.MaxPosition, Settings.InvertPosition, Settings.FilterMode, Settings.FilterRange);
            var engine = new ScriptPlaybackEngine(_scriptHandler.GetScript(), transform, Settings.ScriptDelay,
                Settings.SpeedMultiplier, Settings.MinSpeed, Settings.MaxSpeed);
            long generation = ((MpvTimeSource)TimeSource).ReadPlaybackSample().MediaGeneration;
            TimeSpan? first = LoopSelection ? LoopA : null, last = LoopSelection ? LoopB : null;
            if (first.HasValue && last.HasValue && first > last) { var swap = first; first = last; last = swap; }
            RealtimePlayback.Configure(state.MediaPath, generation, engine, first, last, CommandSource == CommandSource.Video);
        }
        void RealtimeOutputFailed(object sender, Exception error)
        {
            System.Diagnostics.Trace.WriteLine("Playback output: " + error);
            // Error reporting never synchronously waits for the UI and cannot flood its queue.
            if (Interlocked.Exchange(ref realtimeErrorPending, 1) != 0) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) { Interlocked.Exchange(ref realtimeErrorPending, 0); return; }
            dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref realtimeErrorPending, 0);
                if (realtimeDisposed) return;
                RefreshTCodeStatus();
                OsdShowMessage("设备输出失败：" + error.Message, TimeSpan.FromSeconds(5), "PlaybackOutputError");
            }));
        }
        void DisposeRealtimePlayback()
        {
            realtimeDisposed = true;
            RealtimePlayback?.Dispose(); RealtimePlayback = null;
        }
    }
}
