using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;

namespace ScriptPlayer.ViewModels
{
    /// <summary>Small presentation projection over the library; never owns playback state.</summary>
    public sealed class HomeMediaViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly PlaylistViewModel playlist;
        private readonly Dispatcher dispatcher;
        private readonly object refreshGate = new object();
        private readonly HashSet<PlaylistEntry> observedEntries = new HashSet<PlaylistEntry>();
        private ObservableCollection<PlaylistEntry> entries;
        private DispatcherOperation pendingRefresh;
        private volatile bool disposed;

        public ObservableCollection<PlaylistEntry> Favorites { get; } = new ObservableCollection<PlaylistEntry>();
        public bool HasFavorites => Favorites.Count > 0;
        public event PropertyChangedEventHandler PropertyChanged;

        public HomeMediaViewModel(PlaylistViewModel playlist)
        {
            this.playlist = playlist ?? throw new ArgumentNullException(nameof(playlist));
            dispatcher = Dispatcher.CurrentDispatcher;
            playlist.PropertyChanged += PlaylistChanged;
            AttachEntries();
        }

        private void PlaylistChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(PlaylistViewModel.Entries))
                ScheduleRefresh();
        }

        private void AttachEntries()
        {
            if (disposed) return;
            if (!ReferenceEquals(entries, playlist.Entries))
            {
                if (entries != null) entries.CollectionChanged -= EntriesChanged;
                entries = playlist.Entries;
                if (entries != null) entries.CollectionChanged += EntriesChanged;
            }
            Refresh();
        }

        private void EntriesChanged(object sender, NotifyCollectionChangedEventArgs e) => ScheduleRefresh();

        private void EntryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(PlaylistEntry.IsFavorite)
                || e.PropertyName == nameof(PlaylistEntry.Removed) || e.PropertyName == nameof(PlaylistEntry.Fullname))
                ScheduleRefresh();
        }

        private void ScheduleRefresh()
        {
            lock (refreshGate)
            {
                if (disposed || pendingRefresh != null || dispatcher.HasShutdownStarted) return;
                // AddEntries raises one notification per file. Project the final batch once,
                // after foreground UI work, rather than scan the growing library for each add.
                pendingRefresh = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    lock (refreshGate)
                    {
                        pendingRefresh = null;
                        if (disposed) return;
                    }
                    AttachEntries();
                }));
            }
        }

        private void Refresh()
        {
            if (disposed) return;
            var source = entries?.Where(entry => entry != null).ToList() ?? new List<PlaylistEntry>();
            var current = new HashSet<PlaylistEntry>(source);
            foreach (var old in observedEntries.Where(entry => !current.Contains(entry)).ToList())
            {
                old.PropertyChanged -= EntryChanged;
                observedEntries.Remove(old);
            }
            foreach (var entry in current)
                if (observedEntries.Add(entry)) entry.PropertyChanged += EntryChanged;

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var favorites = source.Where(entry => entry.IsFavorite && !entry.Removed
                    && !string.IsNullOrWhiteSpace(entry.Fullname) && paths.Add(entry.Fullname))
                .Take(6).ToList();
            if (Favorites.SequenceEqual(favorites)) return;
            bool hadFavorites = HasFavorites;
            Favorites.Clear();
            foreach (var entry in favorites) Favorites.Add(entry);
            if (hadFavorites != HasFavorites)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFavorites)));
        }

        public void Dispose()
        {
            DispatcherOperation refresh;
            lock (refreshGate)
            {
                if (disposed) return;
                disposed = true;
                refresh = pendingRefresh;
                pendingRefresh = null;
            }
            refresh?.Abort();
            playlist.PropertyChanged -= PlaylistChanged;
            if (entries != null) entries.CollectionChanged -= EntriesChanged;
            foreach (var entry in observedEntries) entry.PropertyChanged -= EntryChanged;
            observedEntries.Clear();
            entries = null;
        }
    }
}
