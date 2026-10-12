using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Classes;

namespace ScriptPlayer.ViewModels
{
    // Presentation state only: all transport commands go to the coordinator.
    public sealed class TimelineViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PlaybackCoordinator playback;
        private readonly List<Action> bookmarkUnsubscriptions = new List<Action>();
        private BookmarkFolderViewModel bookmarks;
        private TimeSpan? loopA, loopB, pausedExit;
        private bool disposed;
        public TimelineViewModel(PlaybackCoordinator playback)
        {
            this.playback = playback ?? throw new ArgumentNullException(nameof(playback));
            State = playback.State;
            playback.StateChanged += PlaybackChanged;
        }
        public PlaybackState State { get; private set; }
        public Brush HeatMap { get; private set; }
        public bool ShowHeatMap { get; private set; }
        public VideoThumbnailCollection Thumbnails { get; private set; }
        public PositionCollection Positions { get; private set; }
        public IReadOnlyList<TimelineMarker> Markers { get; private set; } = new TimelineMarker[0];
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler SessionInvalidated;
        private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        private void PlaybackChanged(object sender, EventArgs args)
        {
            var previous = State;
            State = playback.State;
            bool replaced = previous.SessionId != State.SessionId;
            if (replaced)
            {
                Thumbnails = null;
                Positions = null;
                HeatMap = null;
                ShowHeatMap = false;
                loopA = loopB = pausedExit = null;
                Changed(nameof(Thumbnails)); Changed(nameof(Positions)); Changed(nameof(HeatMap)); Changed(nameof(ShowHeatMap));
            }
            if (replaced || State.Status != PlaybackStatus.Ready) SessionInvalidated?.Invoke(this, EventArgs.Empty);
            Changed(nameof(State));
            if (replaced || previous.Duration != State.Duration || previous.Status != State.Status) RebuildMarkers();
        }
        public void SetPresentation(string media, Brush heatMap, bool showHeatMap, PositionCollection positions,
            VideoThumbnailCollection thumbnails, string thumbnailMedia, TimeSpan? a, TimeSpan? b, TimeSpan? exit)
        {
            bool matches = string.Equals(media, State.MediaPath, StringComparison.OrdinalIgnoreCase);
            var nextHeatMap = matches ? heatMap : null;
            bool nextShowHeatMap = matches && showHeatMap;
            var nextPositions = matches ? positions : null;
            var nextThumbnails = matches && string.Equals(thumbnailMedia, State.MediaPath, StringComparison.OrdinalIgnoreCase) ? thumbnails : null;
            bool heatChanged = !ReferenceEquals(HeatMap, nextHeatMap);
            bool visibilityChanged = ShowHeatMap != nextShowHeatMap;
            bool positionsChanged = !ReferenceEquals(Positions, nextPositions);
            bool thumbnailsChanged = !ReferenceEquals(Thumbnails, nextThumbnails);
            HeatMap = nextHeatMap; ShowHeatMap = nextShowHeatMap;
            Positions = nextPositions; Thumbnails = nextThumbnails;
            loopA = matches ? a : null; loopB = matches ? b : null; pausedExit = matches ? exit : null;
            // An unchanged background cache recheck must not invalidate an active hover.
            // Actual replacements and session invalidation still clear stale previews.
            if (heatChanged) Changed(nameof(HeatMap));
            if (visibilityChanged) Changed(nameof(ShowHeatMap));
            if (positionsChanged) Changed(nameof(Positions));
            if (thumbnailsChanged) Changed(nameof(Thumbnails));
            RebuildMarkers();
        }
        public void SetBookmarks(BookmarkFolderViewModel root)
        {
            if (ReferenceEquals(root, bookmarks)) return;
            bookmarks = root;
            ObserveBookmarks(); RebuildMarkers();
        }
        private void ObserveBookmarks()
        {
            foreach (var unsubscribe in bookmarkUnsubscriptions) unsubscribe();
            bookmarkUnsubscriptions.Clear();
            WatchFolder(bookmarks, new HashSet<BookmarkFolderViewModel>());
        }
        private void BookmarkChanged(object sender, EventArgs args) { ObserveBookmarks(); RebuildMarkers(); }
        private void Watch(INotifyPropertyChanged item)
        {
            PropertyChangedEventHandler handler = (s, e) => BookmarkChanged(s, e);
            item.PropertyChanged += handler;
            bookmarkUnsubscriptions.Add(() => item.PropertyChanged -= handler);
        }
        private void WatchCollection(INotifyCollectionChanged items)
        {
            NotifyCollectionChangedEventHandler handler = (s, e) => BookmarkChanged(s, e);
            items.CollectionChanged += handler;
            bookmarkUnsubscriptions.Add(() => items.CollectionChanged -= handler);
        }
        private void WatchFolder(BookmarkFolderViewModel folder, HashSet<BookmarkFolderViewModel> visited)
        {
            if (folder == null || !visited.Add(folder)) return;
            Watch(folder);
            if (folder.Bookmarks != null) { WatchCollection(folder.Bookmarks); foreach (var item in folder.Bookmarks) if (item != null) Watch(item); }
            if (folder.Folders != null) { WatchCollection(folder.Folders); foreach (var child in folder.Folders) WatchFolder(child, visited); }
        }
        private IEnumerable<BookmarkViewModel> Flatten(BookmarkFolderViewModel folder, HashSet<BookmarkFolderViewModel> visited)
        {
            if (folder == null || !visited.Add(folder)) yield break;
            if (folder.Bookmarks != null) foreach (var item in folder.Bookmarks) if (item != null) yield return item;
            if (folder.Folders != null) foreach (var child in folder.Folders) foreach (var item in Flatten(child, visited)) yield return item;
        }
        private void RebuildMarkers()
        {
            var result = new List<TimelineMarker>();
            if (State.Status == PlaybackStatus.Ready && State.Duration > TimeSpan.Zero)
            {
                foreach (var bookmark in Flatten(bookmarks, new HashSet<BookmarkFolderViewModel>()))
                    if (!string.IsNullOrWhiteSpace(bookmark.FilePath) && string.Equals(bookmark.FilePath, State.MediaPath, StringComparison.OrdinalIgnoreCase))
                        Add(result, bookmark.Timestamp, TimelineMarkerKind.Bookmark, string.IsNullOrWhiteSpace(bookmark.Label) ? Localization.ChineseLocalization.Translate("书签") : bookmark.Label);
                Add(result, loopA, TimelineMarkerKind.LoopA, Localization.ChineseLocalization.Translate("A 点"));
                Add(result, loopB, TimelineMarkerKind.LoopB, Localization.ChineseLocalization.Translate("B 点"));
                Add(result, pausedExit, TimelineMarkerKind.PausedExit, Localization.ChineseLocalization.Translate("上次暂停退出"));
            }
            var next = result.OrderBy(x => x.Position).ToArray();
            if (Markers.Count == next.Length && Markers.Zip(next,
                (left, right) => left.Position == right.Position && left.Kind == right.Kind && left.Label == right.Label).All(equal => equal)) return;
            Markers = next;
            Changed(nameof(Markers));
        }
        private void Add(List<TimelineMarker> target, TimeSpan? time, TimelineMarkerKind kind, string label)
        {
            if (time.HasValue && time.Value >= TimeSpan.Zero && time.Value <= State.Duration)
                target.Add(new TimelineMarker(time.Value, kind, label));
        }
        public void Seek(double relative, TimeSpan absolute, int phase) => playback.Seek(absolute, phase);
        public void SeekTo(TimeSpan time) => playback.SeekTo(time, TimeSpan.Zero);
        public string MarkerLabel(TimeSpan time, double width)
        {
            double tolerance = State.Duration.TotalSeconds * 7 / Math.Max(1, width);
            return string.Join(" · ", Markers.Where(x => Math.Abs((x.Position - time).TotalSeconds) <= tolerance).Select(x => x.Label).Distinct());
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; playback.StateChanged -= PlaybackChanged;
            foreach (var unsubscribe in bookmarkUnsubscriptions) unsubscribe();
            bookmarkUnsubscriptions.Clear();
        }
    }
}
