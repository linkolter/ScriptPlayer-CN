using System;
using System.ComponentModel;
using ScriptPlayer.Playback;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private TimelineViewModel timelineModel;
        private long timelineSession = -1;
        private PlaybackStatus timelineStatus;
        private void InitializeTimeline()
        {
            timelineModel = new TimelineViewModel(ViewModel.Playback);
            PlaybackTimeline.PreviewExecutableProvider = () => ViewModel.Settings.FfmpegPath;
            PlaybackTimeline.Attach(timelineModel);
            // Keep existing automation entry points while ownership moves into the component.
            RegisterName("PersistentHeatMap", PlaybackTimeline.HeatContainer);
            RegisterName("HeatMapPreview", PlaybackTimeline.HeatSeek);
            RegisterName("SeekBar", PlaybackTimeline.ProgressSeek);
            RegisterName("CurrentPlaybackTime", PlaybackTimeline.CurrentTime);
            RegisterName("TotalPlaybackTime", PlaybackTimeline.TotalTime);
            ViewModel.PropertyChanged += TimelineMetadataChanged;
            ViewModel.Playback.StateChanged += TimelinePlaybackChanged;
            RefreshTimelinePresentation();
        }
        private void TimelinePlaybackChanged(object sender, EventArgs e)
        {
            var state = ViewModel.PlaybackState;
            if (timelineSession == state.SessionId && timelineStatus == state.Status) return;
            timelineSession = state.SessionId; timelineStatus = state.Status;
            RefreshTimelinePresentation();
        }
        private void TimelineMetadataChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.LoadedMedia):
                case nameof(MainViewModel.LoadedScript):
                case nameof(MainViewModel.HeatMap):
                case nameof(MainViewModel.Positions):
                case nameof(MainViewModel.Thumbnails):
                case nameof(MainViewModel.ThumbnailsMediaPath):
                case nameof(MainViewModel.LoopA):
                case nameof(MainViewModel.LoopB):
                case nameof(MainViewModel.Bookmarks):
                    RefreshTimelinePresentation(); break;
            }
        }
        private void RefreshTimelinePresentation()
        {
            if (timelineModel == null) return;
            TimeSpan? exit = !string.IsNullOrWhiteSpace(ViewModel.LoadedMedia) && pausedExitPoints.TryGetValue(ViewModel.LoadedMedia, out var value) ? (TimeSpan?)value : null;
            timelineModel.SetBookmarks(ViewModel.Bookmarks);
            timelineModel.SetPresentation(ViewModel.LoadedMedia, ViewModel.HeatMap,
                ViewModel.Settings.ShowHeatMap && !string.IsNullOrEmpty(ViewModel.LoadedScript), ViewModel.Positions,
                ViewModel.Thumbnails, ViewModel.ThumbnailsMediaPath, ViewModel.LoopA, ViewModel.LoopB, exit);
        }
        private void DisposeTimeline()
        {
            ViewModel.PropertyChanged -= TimelineMetadataChanged;
            ViewModel.Playback.StateChanged -= TimelinePlaybackChanged;
            PlaybackTimeline.DisposePreview(); PlaybackTimeline.Attach(null);
            timelineModel?.Dispose(); timelineModel = null;
        }
    }
}
