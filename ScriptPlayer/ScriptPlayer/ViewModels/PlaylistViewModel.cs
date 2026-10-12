using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using JetBrains.Annotations;
using Microsoft.VisualBasic.FileIO;
using ScriptPlayer.Localization;
using ScriptPlayer.Library;
using ScriptPlayer.Metadata;
using Newtonsoft.Json;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Helpers;

namespace ScriptPlayer.ViewModels
{
    public sealed class TagFilterOption
    {
        public TagFilterOption(string displayName, List<string> rawTags, int count)
        {
            DisplayName = displayName;
            RawTags = rawTags ?? new List<string>();
            Count = count;
        }

        public string DisplayName { get; }
        public List<string> RawTags { get; }
        public int Count { get; }
        public bool IsAll => RawTags.Count == 0;
        public string DisplayText => $"{DisplayName}  ·  {Count}";
        public override string ToString() => DisplayText;

        public static TagFilterOption CreateAll(int count)
        {
            return new TagFilterOption("全部标签", new List<string>(), count);
        }
    }

    public class PlaylistViewModel : INotifyPropertyChanged, IDisposable
    {
        private static readonly PathComparer PathComparer = new PathComparer();

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<PlaylistEntry> PlayEntry;

        private readonly List<PlaylistEntry> _previousEntries = new List<PlaylistEntry>();
        private readonly BlockingQueue<PlaylistEntry> _uncheckedPlaylistEntries = new BlockingQueue<PlaylistEntry>();

        private readonly Dispatcher _dispatcher;
        private readonly Random _rng = new Random();
        private readonly Thread _mediaInfoThread;
        private readonly DispatcherTimer _filterDebounceTimer;
        private readonly DispatcherTimer _metadataRefreshTimer;

        private bool _disposed;

        private bool _randomChapters;
        private bool _repeatSingleFile;
        private bool _shuffle;
        private bool _repeat;

        private bool _addedDuringDefer;
        private bool _deferLoading;

        private ObservableCollection<PlaylistEntry> _entries;
        private List<PlaylistEntry> _selectedEntries;
        private List<PlaylistEntry> _filteredEntries;
        private PlaylistEntry _selectedEntry;

        private string _filter;
        private bool _favoritesOnly;
        private List<TagFilterOption> _availableTags = new List<TagFilterOption>();
        private TagFilterOption _selectedTagFilter;
        private int _filterRefreshPending;
        private TimeSpan _totalDuration;
        private TimeSpan _selectedDuration;
        private string _totalDurationString = "0:00:00";
        private string _selectedDurationString = "0:00:00";
        private PlaylistEntry _currentEntry;
        private PlaylistEntry _previousEntry;
        private PlaylistEntry _nextEntry;
        private bool _allowDuplicates = false;

        public TimeSpan SelectedDuration
        {
            get => _selectedDuration;
            private set
            {
                if (value.Equals(_selectedDuration)) return;
                _selectedDuration = value;
                SelectedDurationString = $"{_selectedDuration.Days * 24 + _selectedDuration.Hours:0}:{_selectedDuration.Minutes:00}:{_selectedDuration.Seconds:00}";
                OnPropertyChanged();
            }
        }

        public event EventHandler<RequestEventArgs<string>> RequestMediaFileName;
        public event EventHandler<RequestEventArgs<string>> RequestVideoFileName;
        public event EventHandler<RequestEventArgs<string>> RequestScriptFileName;
        public event EventHandler<RequestEventArgs<string, string[]>> RequestScriptFileNames;
        public event EventHandler<RequestEventArgs<string>> RequestHeatmapFileName;
        public event EventHandler<RequestEventArgs<string>> RequestPreviewFileName;
        public event EventHandler<RequestEventArgs<string, string[]>> RequestAllRelatedFiles;

        public event EventHandler<RequestEventArgs<string>> RequestRenameFile;
        // Rename discovery deliberately bypasses legacy filename de-duplication used by move/delete.
        public Func<string, string, RelatedFileRenameResult> RenameRelatedFiles { get; set; }
        public RelatedFileRenameResult LastRenameResult { get; private set; }
        public string LastRenameReportPath { get; private set; }
        public event EventHandler<RequestEventArgs<string>> RequestDirectory;

        public event EventHandler<string[]> RequestGenerateThumbnails;
        public event EventHandler<string[]> RequestGenerateThumbnailBanners;
        public event EventHandler<string[]> RequestGeneratePreviews;
        public event EventHandler<string[]> RequestGenerateHeatmaps;
        public event EventHandler<Tuple<string[], bool>> RequestGenerateAll;
        public event EventHandler SelectedEntryMoved;
        public event EventHandler<bool> EntriesChanged;
        public event EventHandler NextOrPreviousChanged;

        public bool AllowLocalControl { get; set; }

        public ObservableCollection<PlaylistEntry> Entries
        {
            get => _entries;
            set
            {
                if (Equals(value, _entries)) return;
                UpdateEntryEvents(_entries, value);
                _entries = value;

                bool added = _entries != null && _entries.Count > 0;

                PlaylistEntriesHaveChanged(added);
                OnPropertyChanged();
            }
        }

        private void PlaylistEntriesHaveChanged(bool filesAdded)
        {
            if (_dispatcher == null || DeferLoading)
                return;

            UpdateAvailableTags();
            UpdateFilter();
            CommandManager.InvalidateRequerySuggested();
            UpdateTotalDuration();
            UpdateNextAndPrevious();
            OnEntriesChanged(filesAdded);
        }

        public List<PlaylistEntry> FilteredEntries
        {
            get => _filteredEntries;
            set
            {
                if (Equals(value, _filteredEntries)) return;
                _filteredEntries = value;
                OnPropertyChanged();
            }
        }

        private void UpdateEntryEvents(ObservableCollection<PlaylistEntry> oldValue, ObservableCollection<PlaylistEntry> newValue)
        {
            if (oldValue != null)
            {
                oldValue.CollectionChanged -= Entries_CollectionChanged;
            }

            if (newValue != null)
            {
                newValue.CollectionChanged += Entries_CollectionChanged;
            }
        }

        private void Entries_CollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            bool added = eventArgs.NewItems != null && eventArgs.NewItems.Count > 0;

            PlaylistEntriesHaveChanged(added);
        }

        private void UpdateTotalDuration()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(new Action(UpdateTotalDuration));
                return;
            }

            TotalDuration = Entries?.Aggregate(TimeSpan.Zero, (span, entry) => span + (entry.Duration ?? TimeSpan.Zero)) ?? TimeSpan.Zero;

            UpdateSelectedDuration();
        }

        private void UpdateSelectedDuration()
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(new Action(UpdateSelectedDuration));
                return;
            }

            SelectedDuration = SelectedEntries?.Aggregate(TimeSpan.Zero, (span, entry) => span + entry.Duration ?? TimeSpan.Zero) ?? TimeSpan.Zero;
        }

        public TimeSpan TotalDuration
        {
            get => _totalDuration;
            private set
            {
                if (value.Equals(_totalDuration)) return;
                _totalDuration = value;
                TotalDurationString = $"{_totalDuration.Days * 24 + _totalDuration.Hours:0}:{_totalDuration.Minutes:00}:{_totalDuration.Seconds:00}";
                OnPropertyChanged();

            }
        }

        public string SelectedDurationString
        {
            get => _selectedDurationString;
            private set
            {
                if (value == _selectedDurationString) return;
                _selectedDurationString = value;
                OnPropertyChanged();
            }
        }

        public string TotalDurationString
        {
            get => _totalDurationString;
            private set
            {
                if (value == _totalDurationString) return;
                _totalDurationString = value;
                OnPropertyChanged();
            }
        }

        public bool Shuffle
        {
            get => _shuffle;
            set
            {
                if (value == _shuffle) return;
                _shuffle = value;

                ClearNextAndPrevious();
                UpdateNextAndPrevious();
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged();
            }
        }

        private void UpdateNextEntryIfNull()
        {
            if (NextEntry == null)
                NextEntry = GetNextEntry();

            if (PreviousEntry == null || PreviousEntry == NextEntry)
                PreviousEntry = GetPreviousEntry();
        }

        private void UpdateNextEntry()
        {
            NextEntry = GetNextEntry();

            if (PreviousEntry == null || PreviousEntry == NextEntry)
                PreviousEntry = GetPreviousEntry();
        }

        public bool RandomChapters
        {
            get => _randomChapters;
            set
            {
                if (value == _randomChapters) return;
                _randomChapters = value;
                OnPropertyChanged();
            }
        }

        public bool Repeat
        {
            get => _repeat;
            set
            {
                if (value == _repeat) return;
                _repeat = value;

                UpdateNextEntryIfNull();
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged();
            }
        }

        public bool RepeatSingleFile
        {
            get => _repeatSingleFile;
            set
            {
                if (value == _repeatSingleFile) return;
                _repeatSingleFile = value;

                ClearNextAndPrevious();
                UpdateNextAndPrevious();
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged();
            }
        }

        private void ClearNextAndPrevious()
        {
            NextEntry = null;
            PreviousEntry = null;
        }

        private void UpdateNextAndPrevious()
        {
            if (Shuffle)
                UpdateNextAndPreviousIfNull();
            else
            {
                PreviousEntry = GetPreviousEntry();
                NextEntry = GetNextEntry();
            }
        }

        private void UpdateNextAndPreviousIfNull()
        {
            if (NextEntry == null)
                NextEntry = GetNextEntry();

            if (PreviousEntry == null || PreviousEntry == NextEntry)
                PreviousEntry = GetPreviousEntry();
        }

        public PlaylistEntry SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (Equals(value, _selectedEntry)) return;
                _selectedEntry = value;
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged();
            }
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (value == _filter) return;
                _filter = value;
                ScheduleTextFilterRefresh();
                OnPropertyChanged();
            }
        }


        public bool FavoritesOnly
        {
            get => _favoritesOnly;
            set
            {
                if (value == _favoritesOnly) return;
                _favoritesOnly = value;
                UpdateFilter();
                OnPropertyChanged();
            }
        }

        public List<TagFilterOption> AvailableTags
        {
            get => _availableTags;
            private set
            {
                _availableTags = value ?? new List<TagFilterOption>();
                OnPropertyChanged();
            }
        }

        public TagFilterOption SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                if (ReferenceEquals(value, _selectedTagFilter)) return;
                _selectedTagFilter = value;
                OnPropertyChanged();
                UpdateFilter();
            }
        }

        private PlaylistViewStyle _viewStyle;
        private PlaylistEntry _nonPlaylistEntry;

        public PlaylistViewStyle ViewStyle
        {
            get => _viewStyle;
            set
            {
                if (value == _viewStyle) return;
                _viewStyle = value;
                OnPropertyChanged();
            }
        }

        private void UpdateFilter()
        {
            FilteredEntries = FilterEntries(Entries, Filter);
        }

        private void UpdateAvailableTags()
        {
            string selectedName = SelectedTagFilter?.DisplayName;
            List<PlaylistEntry> entries = Entries?.Where(entry => entry != null && !entry.Removed).ToList()
                ?? new List<PlaylistEntry>();

            Dictionary<string, HashSet<string>> rawTagsByDisplay =
                new Dictionary<string, HashSet<string>>(StringComparer.CurrentCultureIgnoreCase);
            Dictionary<string, int> entryCounts =
                new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            foreach (PlaylistEntry entry in entries)
            {
                HashSet<string> displayTagsInEntry =
                    new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);

                foreach (string rawTag in (entry.RawTagItems ?? new List<string>())
                             .Where(tag => !string.IsNullOrWhiteSpace(tag))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string displayTag = ChineseTagTranslator.Translate(rawTag);
                    if (string.IsNullOrWhiteSpace(displayTag))
                        continue;

                    if (!rawTagsByDisplay.TryGetValue(displayTag, out HashSet<string> rawTags))
                    {
                        rawTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        rawTagsByDisplay.Add(displayTag, rawTags);
                    }

                    rawTags.Add(rawTag);
                    displayTagsInEntry.Add(displayTag);
                }

                foreach (string displayTag in displayTagsInEntry)
                    entryCounts[displayTag] = entryCounts.TryGetValue(displayTag, out int count) ? count + 1 : 1;
            }

            List<TagFilterOption> groups = rawTagsByDisplay
                .OrderBy(pair => pair.Key, StringComparer.CurrentCultureIgnoreCase)
                .Select(pair => new TagFilterOption(pair.Key, pair.Value.ToList(), entryCounts[pair.Key]))
                .ToList();

            groups.Insert(0, TagFilterOption.CreateAll(entries.Count));
            AvailableTags = groups;
            _selectedTagFilter = groups.FirstOrDefault(option =>
                                     string.Equals(option.DisplayName, selectedName,
                                         StringComparison.CurrentCultureIgnoreCase))
                                 ?? groups[0];
            OnPropertyChanged(nameof(SelectedTagFilter));
        }

        private List<PlaylistEntry> FilterEntries(IList<PlaylistEntry> entries, string filter)
        {
            List<PlaylistEntry> filtered = new List<PlaylistEntry>();

            foreach (PlaylistEntry entry in entries ?? new List<PlaylistEntry>())
                if ((!FavoritesOnly || entry.IsFavorite) && MatchesSelectedTag(entry) &&
                    (MatchesFilter(entry.Shortname, filter) ||
                     MatchesFilter(entry.DisplayTitle, filter) ||
                     MatchesFilter(entry.TagsDisplay, filter) ||
                     MatchesFilter(entry.RawTagsDisplay, filter) ||
                     MatchesFilter(entry.TypeDisplay, filter) ||
                     MatchesFilter(entry.ScriptVariantDisplay, filter)))
                    filtered.Add(entry);

            return filtered;
        }

        private bool MatchesSelectedTag(PlaylistEntry entry)
        {
            if (SelectedTagFilter == null || SelectedTagFilter.IsAll)
                return true;

            return entry.RawTagItems.Any(rawTag => SelectedTagFilter.RawTags.Contains(rawTag,
                StringComparer.OrdinalIgnoreCase));
        }

        private bool MatchesFilter(string text, string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return text.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        public RelayCommand<PlaylistEntry> OpenInExplorerCommand { get; set; }
        public RelayCommand<PlaylistEntry> RenameCommand { get; set; }
        public RelayCommand MoveCommand { get; set; }
        public RelayCommand MoveAndRemoveCommand { get; set; }
        public RelayCommand RecycleCommand { get; set; }
        public ScriptplayerCommand DeleteCommand { get; set; }

        public RelayCommand PlayNextEntryCommand { get; set; }
        public RelayCommand PlayPreviousEntryCommand { get; set; }
        public RelayCommand MoveSelectedEntryUpCommand { get; set; }
        public RelayCommand MoveSelectedEntryDownCommand { get; set; }
        public RelayCommand MoveSelectedEntryFirstCommand { get; set; }
        public RelayCommand MoveSelectedEntryLastCommand { get; set; }
        public ScriptplayerCommand RemoveSelectedEntryCommand { get; set; }

        public RelayCommand ClearPlaylistCommand { get; set; }
        public RelayCommand<bool> SortByDurationCommand { get; set; }
        public RelayCommand<bool> SortByNameCommand { get; set; }
        public RelayCommand<bool> SortByPathCommand { get; set; }
        public RelayCommand<bool> SortByMediaCreationTime { get; set; }
        public RelayCommand<bool> SortByScriptCreationTime { get; set; }
        public RelayCommand SortShuffleCommand { get; set; }
        public RelayCommand GenerateThumbnailsForSelectedVideosCommand { get; set; }
        public RelayCommand GenerateThumbnailBannersForSelectedVideosCommand { get; set; }
        public RelayCommand GeneratePreviewsForSelectedVideosCommand { get; set; }
        public RelayCommand GenerateHeatmapsForSelectedVideosCommand { get; set; }
        public RelayCommand GenerateAllForSelectedVideosCommand { get; set; }
        public RelayCommand GenerateAllForAllVideosCommand { get; set; }
        public RelayCommand RecheckAllCommand { get; set; }
        public RelayCommand<PlaylistViewStyle> SetViewStyleCommand { get; set; }
        public int EntryCount => Entries.Count;


        public PlaylistViewModel()
        {
            Entries = new ObservableCollection<PlaylistEntry>();
            SelectedEntries = new List<PlaylistEntry>();

            RenameCommand = new RelayCommand<PlaylistEntry>(ExecuteRenameCommand, SingleEntrySelected);
            MoveCommand = new ScriptplayerCommand(ExecuteMoveCommand, SelectionIsNotEmpty);
            MoveAndRemoveCommand = new ScriptplayerCommand(ExecuteMoveAndRemoveCommand, SelectionIsNotEmpty);
            DeleteCommand = new ScriptplayerCommand(ExecuteDeleteCommand, SelectionIsNotEmpty);
            RecycleCommand = new ScriptplayerCommand(ExecuteRecycleCommand, SelectionIsNotEmpty);

            OpenInExplorerCommand = new RelayCommand<PlaylistEntry>(ExecuteOpenInExplorer, EntryNotNull);
            MoveSelectedEntryDownCommand = new ScriptplayerCommand(ExecuteMoveSelectedEntryDown, CanMoveSelectedEntryDown);
            MoveSelectedEntryUpCommand = new ScriptplayerCommand(ExecuteMoveSelectedEntryUp, CanMoveSelectedEntryUp);
            MoveSelectedEntryLastCommand = new ScriptplayerCommand(ExecuteMoveSelectedEntryLast, CanMoveSelectedEntryDown);
            MoveSelectedEntryFirstCommand = new ScriptplayerCommand(ExecuteMoveSelectedEntryFirst, CanMoveSelectedEntryUp);
            RemoveSelectedEntryCommand = new ScriptplayerCommand(ExecuteRemoveSelectedEntry, SelectionIsNotEmpty)
            {
                CommandId = "RemoveSelectedPlaylistEntries",
                DisplayText = "Remove Selected Playlist Entries",
            };
            ClearPlaylistCommand = new ScriptplayerCommand(ExecuteClearPlaylist, CanClearPlaylist);
            PlayNextEntryCommand = new ScriptplayerCommand(ExecutePlayNextEntry, CanPlayNextEntry);
            PlayPreviousEntryCommand = new ScriptplayerCommand(ExecutePlayPreviousEntry, CanPlayPreviousEntry);
            SortByDurationCommand = new RelayCommand<bool>(ExecuteSortByDuration, AreTheMultipleEntries);
            SortByNameCommand = new RelayCommand<bool>(ExecuteSortByName, AreTheMultipleEntries);
            SortByPathCommand = new RelayCommand<bool>(ExecuteSortByPath, AreTheMultipleEntries);
            SortByMediaCreationTime = new RelayCommand<bool>(ExecuteSortByMediaCreationTime, AreTheMultipleEntries);
            SortByScriptCreationTime = new RelayCommand<bool>(ExecuteSortByScriptCreationTime, AreTheMultipleEntries);
            SortShuffleCommand = new RelayCommand(ExecuteSortShuffle, AreTheMultipleEntries);
            GenerateThumbnailsForSelectedVideosCommand = new RelayCommand(ExecuteGenerateThumbnailsForSelectedVideos, AreEntriesSelected);
            GenerateThumbnailBannersForSelectedVideosCommand = new RelayCommand(ExecuteGenerateThumbnailBannersForSelectedVideos, AreEntriesSelected);
            GeneratePreviewsForSelectedVideosCommand = new RelayCommand(ExecuteGeneratePreviewsForSelectedVideos, AreEntriesSelected);
            GenerateHeatmapsForSelectedVideosCommand = new RelayCommand(ExecuteGenerateHeatmapsForSelectedVideos, AreEntriesSelected);
            GenerateAllForSelectedVideosCommand = new RelayCommand(ExecuteGenerateAllForSelectedVideos, AreEntriesSelected);
            GenerateAllForAllVideosCommand = new RelayCommand(ExecuteGenerateAllForAllVideos, AreThereAnyEntries);
            RecheckAllCommand = new RelayCommand(ExecuteRecheckAll);
            SetViewStyleCommand = new RelayCommand<PlaylistViewStyle>(ExecuteSetViewStlye);

            _dispatcher = Dispatcher.CurrentDispatcher;
            _filterDebounceTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(140)
            };
            _filterDebounceTimer.Tick += FilterDebounceTimerOnTick;

            _metadataRefreshTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            _metadataRefreshTimer.Tick += MetadataRefreshTimerOnTick;

            UpdateAvailableTags();
            UpdateFilter();
            _mediaInfoThread = new Thread(MediaInfoLoop);
            _mediaInfoThread.Start();
        }

        private void ExecuteSetViewStlye(PlaylistViewStyle style)
        {
            ViewStyle = style;
        }

        private bool SingleEntrySelected(PlaylistEntry arg)
        {
            return EntryNotNull(arg) && _selectedEntries != null && _selectedEntries.Count == 1;
        }

        private void ExecuteDeleteCommand()
        {
            List<string> allFiles = new List<string>();
            var entriesToDelete = _selectedEntries.ToList();

            foreach (PlaylistEntry entry in entriesToDelete)
            {
                string originalFile = entry.Fullname;

                string[] relatedFiles = GetAllRelatedFiles(originalFile);

                if (relatedFiles == null || relatedFiles.Length == 0)
                    continue;

                allFiles.AddRange(relatedFiles);
            }

            var answer = MessageBox.Show($"Are you sure you want to PERMANENTLY delete these {allFiles.Count} files?",
                "Confirm delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                foreach (string file in allFiles)
                {
                    File.Delete(file);
                }

                RemoveEntries(entriesToDelete);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured while deleting files:\r\n" + ex.Message, "Delete files",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteRecycleCommand()
        {
            var entriesToDelete = _selectedEntries.ToList();

            MoveToRecycleBin(entriesToDelete);
        }

        public void MoveToRecycleBin(List<PlaylistEntry> entriesToDelete)
        {
            List<string> allFiles = new List<string>();

            foreach (PlaylistEntry entry in entriesToDelete)
            {
                string originalFile = entry.Fullname;

                string[] relatedFiles = GetAllRelatedFiles(originalFile);

                if (relatedFiles == null || relatedFiles.Length == 0)
                    continue;

                allFiles.AddRange(relatedFiles);
            }

            var answer = MessageBox.Show($"Are you sure you want to delete these {allFiles.Count} files?",
                "Confirm delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
                return;

            try
            {
                foreach (string file in allFiles)
                {
                    FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                }

                RemoveEntries(entriesToDelete);
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occured while deleting files:\r\n" + ex.Message, "Delete files",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteMoveAndRemoveCommand()
        {
            MoveSelectedEntriesToNewDirectory(true);
        }

        private void ExecuteMoveCommand()
        {
            MoveSelectedEntriesToNewDirectory(false);
        }

        private void MoveSelectedEntriesToNewDirectory(bool removeFromPlaylist)
        {
            string newDirectory = OnRequestDirectory(Path.GetDirectoryName(_selectedEntries.First().Fullname));
            if (string.IsNullOrEmpty(newDirectory))
                return;

            var entriesToMove = _selectedEntries.ToList();

            MoveEntriesToNewDirectory(entriesToMove, newDirectory, removeFromPlaylist, false);
        }

        public void MoveEntriesToNewDirectory(List<PlaylistEntry> entriesToMove, string newDirectory, bool removeFromPlaylist, bool playNext)
        {
            Dictionary<string, string> newNames = new Dictionary<string, string>();

            foreach (PlaylistEntry entry in entriesToMove)
            {
                string originalFile = entry.Fullname;
                string[] relatedFiles = GetAllRelatedFiles(originalFile);

                if (relatedFiles == null || relatedFiles.Length == 0)
                    continue;

                foreach (var kvp in relatedFiles.ToDictionary(k => k, v => GetNewPath(v, newDirectory), PathComparer))
                {
                    if (!newNames.ContainsKey(kvp.Key))
                        newNames.Add(kvp.Key, kvp.Value);
                }
            }

            try
            {
                bool allAlreadyInNewDir = true;

                foreach (string file in newNames.Keys)
                {
                    if (!PathComparer.Equals(Path.GetDirectoryName(file), newDirectory))
                        allAlreadyInNewDir = false;
                }

                if (allAlreadyInNewDir)
                {
                    MessageBox.Show("All related files are already in this directory!", "Move", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                foreach (string oldFile in newNames.Keys)
                {
                    if (File.Exists(newNames[oldFile]))
                    {
                        MessageBox.Show($"Can't move file!\r\n The file '{newNames[oldFile]}' already exists!",
                            "Move",
                            MessageBoxButton.OK, MessageBoxImage.Error);

                        return;
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Move failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newFileName = "";

            try
            {
                foreach (string oldFile in newNames.Keys)
                {
                    newFileName = newNames[oldFile];
                    File.Move(oldFile, newFileName);
                }

                if (removeFromPlaylist)
                {
                    RemoveEntries(entriesToMove);
                }
                else
                {
                    foreach (PlaylistEntry entry in entriesToMove)
                    {
                        entry.Fullname = newNames[entry.Fullname];
                        entry.Reset();
                        _uncheckedPlaylistEntries.Enqueue(entry);
                    }
                }

                if(playNext)
                    PlayNextEntry();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occured while moving '{newFileName}':\r\n{ex.Message}", "Move failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteRenameCommand(PlaylistEntry entry)
        {
            string commonName = Path.GetFileNameWithoutExtension(entry.Fullname);
            string newCommonName = OnRequestRenameFile(commonName);
            if (string.IsNullOrEmpty(newCommonName))
                return;
            var result = RenameEntry(entry, newCommonName);
            if (result.Status == RelatedFileRenameStatus.Success && result.Errors.Count == 0 || result.Status == RelatedFileRenameStatus.NoChange)
                return;
            string locations = string.Join("\r\n", result.Plan.Where(p => !string.Equals(p.OriginalPath, p.CurrentPath, StringComparison.Ordinal))
                .Select(p => p.CurrentPath + " → " + p.OriginalPath));
            string details = string.Join("\r\n", result.Errors.Select(e => e.Phase + ": " + e.Message));
            MessageBox.Show("改名结果：" + result.Status + "\r\n" + details
                + (locations.Length == 0 ? "" : "\r\n尚需恢复（当前 → 原路径）：\r\n" + locations)
                + (LastRenameReportPath == null ? "" : "\r\n完整路径记录：" + LastRenameReportPath), "关联文件改名",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public RelatedFileRenameResult RenameEntry(PlaylistEntry entry, string newCommonName)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            string originalPath = entry.Fullname;
            RelatedFileRenameResult result;
            var affectedEntries = Entries.Concat(new[] { entry }).Distinct().ToList();
            var selectedCandidates = new Dictionary<PlaylistEntry, string[]>();
            try
            {
                string oldStem = Path.GetFileNameWithoutExtension(originalPath);
                foreach (var candidate in affectedEntries)
                {
                    string selected = candidate.SelectedScriptName;
                    if (string.IsNullOrEmpty(selected) || !selected.StartsWith(oldStem, StringComparison.OrdinalIgnoreCase)) continue;
                    var scripts = OnRequestScriptFileNames(candidate.Fullname);
                    if (scripts == null)
                    {
                        string local = Path.Combine(Path.GetDirectoryName(candidate.Fullname), selected);
                        scripts = File.Exists(local) ? new[] { local } : new string[0];
                    }
                    selectedCandidates[candidate] = scripts.Where(p => string.Equals(Path.GetFileName(p), selected, StringComparison.OrdinalIgnoreCase))
                        .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                }
                result = RenameRelatedFiles != null ? RenameRelatedFiles(originalPath, newCommonName)
                    : RelatedFileRename.Rename(originalPath, newCommonName, new[] { Path.GetDirectoryName(originalPath) }, null);
            }
            catch (Exception ex)
            {
                result = new RelatedFileRenameResult { Status = RelatedFileRenameStatus.Rejected, OriginalPath = originalPath, NewStem = newCommonName };
                result.Errors.Add(new RelatedFileRenameError { Phase = "Discovery", Message = ex.Message });
            }
            LastRenameResult = result;
            LastRenameReportPath = null;
            // Keep the complete old/new/current mapping for diagnosis and future R10, without migrating history.
            if (result.Status != RelatedFileRenameStatus.NoChange)
            {
                try
                {
                    string appData = Environment.GetEnvironmentVariable("APPDATA");
                    if (string.IsNullOrWhiteSpace(appData) || !Path.IsPathRooted(appData))
                        throw new IOException("APPDATA must be an absolute path for rename reports.");
                    string directory = Path.Combine(appData, "ScriptPlayer", "RenameOperations");
                    Directory.CreateDirectory(directory);
                    string report = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".json");
                    using (var stream = new FileStream(report, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream)) writer.Write(JsonConvert.SerializeObject(result, Formatting.Indented));
                    LastRenameReportPath = report;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new RelatedFileRenameError { Phase = "Report", Message = "路径记录未能保存；内存结果仍保留：" + ex.Message });
                    Debug.WriteLine(ex);
                }
            }
            if (result.Status != RelatedFileRenameStatus.Success) return result;
            // Publish all affected entries only after the entire disk operation committed.
            foreach (var affected in affectedEntries)
            {
                var mapping = result.Plan.FirstOrDefault(p => PathComparer.Equals(p.OriginalPath, affected.Fullname));
                string selectedScript = affected.SelectedScriptName;
                string[] candidates;
                selectedCandidates.TryGetValue(affected, out candidates);
                var selectedMapping = candidates == null ? null : result.Plan.FirstOrDefault(p => candidates.Contains(p.OriginalPath, StringComparer.OrdinalIgnoreCase));
                // A same-named script outside this operation can still be the entry's local choice.
                bool ambiguous = candidates != null && candidates.Any(p => !result.Plan.Any(m => PathComparer.Equals(m.OriginalPath, p)));
                if (mapping == null && selectedMapping == null) continue;
                if (mapping != null) affected.Fullname = mapping.TargetPath;
                affected.Reset();
                affected.SelectedScriptName = selectedMapping == null || ambiguous ? selectedScript : Path.GetFileName(selectedMapping.TargetPath);
                _uncheckedPlaylistEntries.Enqueue(affected);
            }
            return result;
        }

        private static string GetNewPath(string filePath, string newDirectory)
        {
            string fileName = Path.GetFileName(filePath);

            return Path.Combine(newDirectory, fileName);
        }

        private void ExecuteRecheckAll()
        {
            RecheckAllEntries();
        }

        private void ExecuteOpenInExplorer(PlaylistEntry obj)
        {
            OpenInExplorer(obj.Fullname);
        }

        private bool EntryNotNull(PlaylistEntry arg)
        {
            return arg != null;
        }

        private bool EntriesNotNull(List<PlaylistEntry> arg)
        {
            if (arg == null)
                return false;

            return arg.Count > 0;
        }

        private bool AreEntriesSelected()
        {
            if (_selectedEntries == null)
                return false;
            return _selectedEntries.Count > 0;
        }

        private void ExecuteGenerateThumbnailsForSelectedVideos()
        {
            string[] videos = _selectedEntries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGenerateThumbnails(videos);
        }

        private void ExecuteGenerateThumbnailBannersForSelectedVideos()
        {
            string[] videos = _selectedEntries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGenerateThumbnailBanners(videos);
        }

        private void ExecuteGeneratePreviewsForSelectedVideos()
        {
            string[] videos = _selectedEntries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGeneratePreviews(videos);
        }

        private void ExecuteGenerateHeatmapsForSelectedVideos()
        {
            string[] videos = _selectedEntries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGenerateHeatmaps(videos);
        }

        public void GenerateAllForAll(bool visible)
        {
            string[] videos = Entries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGenerateAll(videos, visible);
        }

        private void ExecuteGenerateAllForAllVideos()
        {
            GenerateAllForAll(true);
        }

        private void ExecuteGenerateAllForSelectedVideos()
        {
            string[] videos = _selectedEntries.Select(e => OnRequestVideoFileName(e.Fullname)).Where(v => !string.IsNullOrEmpty(v)).ToArray();
            if (videos.Length == 0)
                return;

            OnRequestGenerateAll(videos, true);
        }

        private bool AreTheMultipleEntries()
        {
            return Entries.Count > 1;
        }

        private bool AreThereAnyEntries()
        {
            return Entries.Any();
        }

        private void ExecuteSortShuffle()
        {
            List<PlaylistEntry> entries = Entries.ToList();
            List<PlaylistEntry> newOrder = new List<PlaylistEntry>();

            Random r = new Random();
            while (entries.Count > 0)
            {
                int next = r.Next(0, entries.Count);
                newOrder.Add(entries[next]);
                entries.RemoveAt(next);
            }

            Entries = new ObservableCollection<PlaylistEntry>(newOrder);
        }

        private void ExecuteSortByDuration(bool ascending)
        {
            SetEntries(Entries.OrderBy(e => e.Duration), ascending);
        }

        private void ExecuteSortByName(bool ascending)
        {
            SetEntries(Entries.OrderBy(e => e.Shortname), ascending);
        }

        private void ExecuteSortByPath(bool ascending)
        {
            SetEntries(Entries.OrderBy(e => e.Fullname), ascending);
        }

        private void ExecuteSortByMediaCreationTime(bool ascending)
        {
            SetEntries(Entries.OrderBy(e => e.MediaCreationTime), ascending);
        }

        private void ExecuteSortByScriptCreationTime(bool ascending)
        {
            SetEntries(Entries.OrderBy(e => e.ScriptCreationTime), ascending);
        }
        
        private void SetEntries(IOrderedEnumerable<PlaylistEntry> entries, bool sortAscending)
        {
            Entries = new ObservableCollection<PlaylistEntry>(sortAscending ? entries : entries.Reverse());
        }

        private bool AreTheMultipleEntries(bool arg)
        {
            return AreTheMultipleEntries();
        }

        private void MediaInfoLoop()
        {
            while (!_disposed)
            {
                PlaylistEntry entry = _uncheckedPlaylistEntries.Dequeue();
                if (entry == null)
                    return;

                if (entry.Removed)
                    continue;

                string mediaFile = OnRequestMediaFileName(entry.Fullname);
                if (!string.IsNullOrEmpty(mediaFile) && File.Exists(mediaFile))
                {
                    entry.HasMedia = true;
                    entry.MediaCreationTime = File.GetCreationTime(mediaFile);

                    // EXTINF:0 in an M3U means that the duration has not been scanned yet.
                    if (entry.Duration == null || entry.Duration.Value <= TimeSpan.Zero)
                    {
                        entry.Duration = MediaHelper.GetDuration(mediaFile);
                        if (entry.Duration != null)
                            UpdateTotalDuration();
                    }
                }
                else
                {
                    entry.HasMedia = false;
                    entry.Duration = null;
                    entry.MediaCreationTime = DateTime.MinValue;
                }

                string[] scriptVariants = OnRequestScriptFileNames(entry.Fullname) ?? new string[0];
                string scriptFile = scriptVariants.FirstOrDefault() ?? OnRequestScriptFileName(entry.Fullname);
                if (!string.IsNullOrWhiteSpace(scriptFile) && File.Exists(scriptFile))
                {
                    entry.HasScript = true;
                    entry.ScriptCreationTime = File.GetCreationTime(scriptFile);
                }
                else
                {
                    entry.HasScript = false;
                    entry.ScriptCreationTime = DateTime.MinValue;
                }

                string heatMap = OnRequestHeatmapFileName(entry.Fullname);
                if (!string.IsNullOrEmpty(heatMap))
                {
                    entry.HeatMap = GetHeatMapImage(heatMap);
                }

                string preview = OnRequestPreviewFileName(entry.Fullname);
                if (!string.IsNullOrEmpty(preview))
                {
                    entry.Preview = GetPreviewImage(preview);
                }

                entry.LoadLocalMetadata(mediaFile, scriptFile, scriptVariants);
                ScheduleFilterRefresh();

                entry.UpdateStatus();
            }
        }

        private ImageSource GetPreviewImage(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    return GifFrameCollection.LoadFirst(stream, 512, 320);
                }
            }
            catch
            {
                return null;
            }
        }

        private ImageSource GetHeatMapImage(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = stream;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.DecodePixelWidth = 1024;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            }
            catch
            {
                return null;
            }
        }

        private bool CanClearPlaylist()
        {
            return _entries.Count > 0;
        }

        private void ExecuteClearPlaylist()
        {
            Clear();
        }

        private bool CanPlayPreviousEntry()
        {
            bool canPlaySameAgain = CurrentEntry != null;

            if (Repeat)
                return Entries.Count > 0 || canPlaySameAgain;

            if (RepeatSingleFile)
                return canPlaySameAgain;

            if (Entries.Count == 0)
                return false;

            if (CurrentEntry == null)
                return false;

            if (Shuffle)
                return Entries.Count > 1;

            int index = Entries.IndexOf(CurrentEntry);

            return index > 0;
        }

        public bool CanPlayNextEntry()
        {
            bool canPlaySameAgain = (CurrentEntry != null || _nonPlaylistEntry != null);

            if (Repeat)
                return Entries.Count > 0 || canPlaySameAgain;

            if (RepeatSingleFile)
                return canPlaySameAgain;

            if (Entries.Count == 0)
                return false;

            if (CurrentEntry == null)
                return true;

            if (Shuffle)
                return Entries.Count > 1;

            int index = Entries.IndexOf(CurrentEntry);

            return index < Entries.Count - 1;
        }

        private void ExecutePlayPreviousEntry()
        {
            PlayPreviousEntry();
        }

        public void PlayPreviousEntry()
        {
            if (!CanPlayPreviousEntry())
                return;

            PlaylistEntry entry = PreviousEntry;
            if (entry == null)
                return;

            OnPlayEntry(entry);
        }


        public void PlayNextEntry()
        {
            if (!CanPlayNextEntry())
                return;

            PlaylistEntry entry = NextEntry;
            if (entry == null)
                return;

            OnPlayEntry(entry);
        }

        private void ExecutePlayNextEntry()
        {
            PlayNextEntry();
        }

        private bool SelectionIsNotEmpty()
        {
            return !SelectionIsEmpty();
        }

        public void Clear()
        {
            foreach (var entry in Entries)
                entry.Removed = true;

            Entries.Clear();
            _previousEntries.Clear();
            PreviousEntry = null;
            NextEntry = null;
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteRemoveSelectedEntry()
        {
            if (!SelectionIsNotEmpty())
                return;

            RemoveEntries(_selectedEntries);
        }

        private void RemoveEntries(List<PlaylistEntry> selectedEntries)
        {
            try
            {
                DeferLoading = true;

                var itemsToRemove = selectedEntries.OrderBy(i => Entries.IndexOf(i)).ToList();
                int currentIndex = Entries.IndexOf(selectedEntries.First());

                foreach (var item in itemsToRemove)
                {
                    item.Removed = true;
                    Entries.Remove(item);

                    if (NextEntry == item)
                        NextEntry = null;

                    if (PreviousEntry == item)
                        PreviousEntry = null;

                    _previousEntries.Remove(item);
                }

                if (currentIndex < Entries.Count)
                    SelectedEntry = Entries[currentIndex];
                else if (Entries.Count > 0)
                    SelectedEntry = Entries[Entries.Count - 1];
                else
                    SelectedEntry = null;

            }
            finally
            {
                DeferLoading = false;
            }
        }

        private bool SelectionHasGap()
        {
            List<int> indices = _selectedEntries.Select(i => Entries.IndexOf(i)).ToList();
            return indices.Max() - indices.Min() + 1 != indices.Count;
        }

        private bool SelectionIsEmpty()
        {
            if (_selectedEntries == null)
                return true;

            return _selectedEntries.Count <= 0;
        }

        private bool CanMoveSelectedEntryUp()
        {
            return CanMoveSelectedEntry(true);
        }

        private bool CanMoveSelectedEntryDown()
        {
            return CanMoveSelectedEntry(false);
        }

        private bool CanMoveSelectedEntry(bool up)
        {
            if (SelectionIsEmpty())
                return false;

            if (SelectionHasGap())
                return true;

            return !_selectedEntries.Contains(up ? Entries.First() : Entries.Last());
        }

        private void ExecuteMoveSelectedEntryUp()
        {
            MoveSelection(true, false);
        }

        private void ExecuteMoveSelectedEntryDown()
        {
            MoveSelection(false, false);
        }

        private void ExecuteMoveSelectedEntryFirst()
        {
            MoveSelection(true, true);
        }

        private void ExecuteMoveSelectedEntryLast()
        {
            MoveSelection(false, true);
        }

        private void MoveSelection(bool up, bool allTheWay)
        {
            if (up && !CanMoveSelectedEntryUp()) return;
            if (!up && !CanMoveSelectedEntryDown()) return;

            try
            {
                DeferLoading = true;


                bool hasGap = SelectionHasGap();
                int indexShift = hasGap ? 0 : 1;

                var orderedSelection = _selectedEntries.OrderBy(i => Entries.IndexOf(i)).ToList();

                if (up)
                {
                    int firstIndex;

                    if (allTheWay)
                        firstIndex = 0;
                    else
                        firstIndex = Entries.IndexOf(orderedSelection.First()) - indexShift;

                    for (int i = 0; i < orderedSelection.Count; i++)
                    {
                        int currentIndex = Entries.IndexOf(orderedSelection[i]);
                        if (currentIndex != firstIndex + i)
                            Entries.Move(currentIndex, firstIndex + i);
                    }
                }
                else
                {
                    int lastIndex;

                    if (allTheWay)
                        lastIndex = Entries.Count - 1;
                    else
                        lastIndex = Entries.IndexOf(orderedSelection.Last()) + indexShift;

                    orderedSelection.Reverse();

                    for (int i = 0; i < orderedSelection.Count; i++)
                    {
                        int currentIndex = Entries.IndexOf(orderedSelection[i]);
                        if (currentIndex != lastIndex - i)
                            Entries.Move(currentIndex, lastIndex - i);
                    }
                }

                OnSelectedEntryMoved();
            }
            finally
            {
                DeferLoading = false;
            }
        }

        private void RecheckAllEntries()
        {
            _uncheckedPlaylistEntries.Clear();

            foreach (PlaylistEntry entry in Entries)
            {
                entry.Reset();
                _uncheckedPlaylistEntries.Enqueue(entry);
            }
        }

        public void ReloadEntryMetadata(PlaylistEntry entry)
        {
            if (entry == null)
                return;

            entry.Reset();
            _uncheckedPlaylistEntries.Enqueue(entry);
            UpdateAvailableTags();
            UpdateFilter();
        }

        public NfoMetadata SaveEntryFavorite(PlaylistEntry entry, bool favorite, NfoFileOperations files = null)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var draft = entry.CreateMetadataDraft();
            draft.Favorite = favorite;
            string target = string.IsNullOrWhiteSpace(entry.NfoPath) ? entry.Fullname : entry.NfoPath;
            draft.Save(target, files ?? new NfoFileOperations());
            return draft;
        }

        /// <summary>Publishes committed data directly; no Reset, rescanning or failure-prone disk reload.</summary>
        public string PublishCommittedMetadata(NfoMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.SidecarPath))
                throw new ArgumentException("缺少已提交的 NFO。", nameof(metadata));
            var warnings = new List<string>();
            string target = Path.GetFullPath(metadata.SidecarPath);
            foreach (var entry in Entries.Where(value => value != null && !value.Removed).ToArray())
            {
                if (string.IsNullOrWhiteSpace(entry.NfoPath)) continue;
                try
                {
                    if (string.Equals(Path.GetFullPath(entry.NfoPath), target, StringComparison.OrdinalIgnoreCase))
                        entry.ApplyCommittedMetadata(metadata);
                }
                catch (Exception ex) { warnings.Add(entry.Fullname + ": " + ex.Message); }
            }
            try { RefreshFilter(); }
            catch (Exception ex) { warnings.Add(ex.Message); }
            return warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings);
        }

        public void RefreshLocalMetadata()
        {
            foreach (PlaylistEntry entry in Entries.Where(value => value != null && !value.Removed))
            {
                string mediaFile = OnRequestMediaFileName(entry.Fullname);
                string[] scriptVariants = OnRequestScriptFileNames(entry.Fullname) ?? new string[0];
                string scriptFile = scriptVariants.FirstOrDefault() ?? OnRequestScriptFileName(entry.Fullname);
                entry.LoadLocalMetadata(mediaFile, scriptFile, scriptVariants);
            }

            UpdateAvailableTags();
            UpdateFilter();
        }

        public void RefreshFilter()
        {
            UpdateAvailableTags();
            UpdateFilter();
        }

        private void ScheduleFilterRefresh()
        {
            if (Interlocked.Exchange(ref _filterRefreshPending, 1) == 1)
                return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _filterRefreshPending, 0);
                if (_disposed)
                    return;

                _metadataRefreshTimer.Stop();
                _metadataRefreshTimer.Start();
            }), DispatcherPriority.Background);
        }

        private void ScheduleTextFilterRefresh()
        {
            if (_filterDebounceTimer == null)
                return;

            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private void FilterDebounceTimerOnTick(object sender, EventArgs eventArgs)
        {
            _filterDebounceTimer.Stop();
            UpdateFilter();
        }

        private void MetadataRefreshTimerOnTick(object sender, EventArgs eventArgs)
        {
            _metadataRefreshTimer.Stop();
            UpdateAvailableTags();
            UpdateFilter();
        }

        private void EnsureMediaInfo(PlaylistEntry entry)
        {
            if (entry.Status == PlaylistEntryStatus.Loading)
            {
                _uncheckedPlaylistEntries.Enqueue(entry);
            }
        }

        public void AddEntries(bool first, string[] filenames)
        {
            AddEntries(first, filenames.Select(fn => new PlaylistEntry(fn)));
        }

        public void AddEntries(bool first, IEnumerable<PlaylistEntry> entries)
        {
            try
            {
                int insertpos = 0;

                DeferLoading = true;
                foreach (PlaylistEntry entry in entries)
                {
                    if (!_allowDuplicates)
                    {
                        if (Entries.Any(e => string.Equals(e.Fullname, entry.Fullname, StringComparison.InvariantCultureIgnoreCase)))
                            continue;
                    }

                    _addedDuringDefer = true;
                    EnsureMediaInfo(entry);

                    if(first)
                        Entries.Insert(insertpos++, entry);
                    else
                        Entries.Add(entry);
                }
            }
            finally
            {
                DeferLoading = false;
            }
        }

        public void SetCurrentEntry(string[] files)
        {
            PlaylistEntry existingEntry = GetEntry(files);

            if (existingEntry == null)
            {
                _nonPlaylistEntry = new PlaylistEntry(files[0]);
            }
            else
            {
                _nonPlaylistEntry = null;
            }


            if (existingEntry == CurrentEntry)
                return;

            foreach (PlaylistEntry entry in Entries)
            {
                entry.Playing = entry == existingEntry;
            }

            CurrentEntry = existingEntry;

            PreviousEntry = GetPreviousEntry();
            NextEntry = GetNextEntry();
        }

        public PlaylistEntry CurrentEntry
        {
            get => _currentEntry;
            set
            {
                if (Equals(value, _currentEntry)) return;
                _currentEntry = value;

                _previousEntries.Remove(_currentEntry);
                _previousEntries.Insert(0, _currentEntry);
                OnPropertyChanged();
            }
        }

        public PlaylistEntry PreviousEntry
        {
            get => _previousEntry;
            set
            {
                if (Equals(value, _previousEntry)) return;
                _previousEntry = value;
                OnPropertyChanged();
                OnNextOrPreviousChanged();
            }
        }

        public PlaylistEntry NextEntry
        {
            get => _nextEntry;
            set
            {
                if (Equals(value, _nextEntry)) return;
                _nextEntry = value;
                OnPropertyChanged();
                OnNextOrPreviousChanged();
            }
        }

        public PlaylistEntry GetEntry(string[] files)
        {
            if (files == null || files.Length == 0)
                return null;

            if (Entries == null || Entries.Count == 0)
                return null;

            return Entries.FirstOrDefault(p => files.Contains(p.Fullname));
        }

        public PlaylistEntry GetFilteredEntry(string[] files)
        {
            if (files == null || files.Length == 0)
                return null;

            if (FilteredEntries == null || FilteredEntries.Count == 0)
                return null;

            return FilteredEntries.FirstOrDefault(p => files.Contains(p.Fullname));
        }

        public PlaylistEntry FirstEntry()
        {
            return Entries.FirstOrDefault();
        }

        public PlaylistEntry GetNextEntry()
        {
            if (RepeatSingleFile)
            {
                if (CurrentEntry != null)
                    return CurrentEntry;

                if (_nonPlaylistEntry != null)
                    return _nonPlaylistEntry;
            }

            if (Shuffle)
                return GetRandomEntry();

            if (CurrentEntry == null)
                return FirstEntry();

            int currentIndex = Entries.IndexOf(CurrentEntry);

            if (currentIndex == Entries.Count - 1)
            {
                if (Repeat)
                {
                    if (Entries.Count > 0)
                        return Entries.First();
                    if (CurrentEntry != null)
                        return CurrentEntry;
                }
                return null;
            }

            var nextEntry = Entries[currentIndex + 1];

            return nextEntry;
        }

        public PlaylistEntry GetPreviousEntry()
        {
            if (RepeatSingleFile && CurrentEntry != null)
                return CurrentEntry;

            if (Shuffle)
                return GetRandomEntry();

            if (CurrentEntry == null)
            {
                if (Repeat)
                    return LastEntry();

                return null;
            }


            int currentIndex = Entries.IndexOf(CurrentEntry);

            if (currentIndex == 0)
            {
                if (Repeat)
                {
                    if (Entries.Count > 0)
                        return Entries.First();
                    if (CurrentEntry != null)
                        return CurrentEntry;
                }
                return null;
            }

            if (currentIndex == -1)
            {
                return FirstEntry();
            }

            var previousEntry = Entries[currentIndex - 1];

            return previousEntry;
        }

        private PlaylistEntry LastEntry()
        {
            return Entries.LastOrDefault();
        }

        private PlaylistEntry GetRandomEntry()
        {
            if (Entries.Count == 0)
                return null;

            if (Entries.Count == 1)
                return Entries.Single();

            var otherEntries = Entries.ToList();
            if (CurrentEntry != null)
                otherEntries.Remove(CurrentEntry);

            //Makes selection a little less random
            const double factor = 0.5;
            int entryCount = (int)Math.Floor(otherEntries.Count * factor);
            var recentEntries = _previousEntries.Take(entryCount).ToList();

            otherEntries.RemoveAll(p => recentEntries.Contains(p));

            return otherEntries[_rng.Next(0, otherEntries.Count)];
        }

        protected virtual void OnPlayEntry(PlaylistEntry entry)
        {
            PreviousEntry = CurrentEntry;

            if(SelectEntryOnPlay)
                SelectedEntry = entry;

            CurrentEntry = entry;
            UpdateNextEntry();

            PlayEntry?.Invoke(this, entry);
        }

        public bool SelectEntryOnPlay { get; set; } = true;


        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RequestPlayEntry(PlaylistEntry entry)
        {
            OnPlayEntry(entry);
        }

        protected virtual string OnRequestRenameFile(string fileName)
        {
            RequestEventArgs<string> eventArgs = new RequestEventArgs<string>(fileName);
            RequestRenameFile?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.Value;
        }

        protected virtual string OnRequestMediaFileName(string fileName)
        {
            RequestEventArgs<string> eventArgs = new RequestEventArgs<string>(fileName);
            RequestMediaFileName?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.Value;
        }

        protected virtual string OnRequestScriptFileName(string fileName)
        {
            RequestEventArgs<string> eventArgs = new RequestEventArgs<string>(fileName);
            RequestScriptFileName?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.Value;
        }

        protected virtual string[] OnRequestScriptFileNames(string fileName)
        {
            RequestEventArgs<string, string[]> eventArgs = new RequestEventArgs<string, string[]>(fileName);
            RequestScriptFileNames?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.ValueOut;
        }

        protected virtual string OnRequestVideoFileName(string fileName)
        {
            RequestEventArgs<string> eventArgs = new RequestEventArgs<string>(fileName);
            RequestVideoFileName?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.Value;
        }

        protected virtual string[] OnRequestAllRelatedFiles(string fileName)
        {
            RequestEventArgs<string, string[]> eventArgs = new RequestEventArgs<string, string[]>(fileName);
            RequestAllRelatedFiles?.Invoke(this, eventArgs);

            return !eventArgs.Handled ? null : eventArgs.ValueOut;
        }

        public string[] GetAllRelatedFiles(string filePath)
        {
            return OnRequestAllRelatedFiles(filePath);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _uncheckedPlaylistEntries.Close();

            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Tick -= FilterDebounceTimerOnTick;
            _metadataRefreshTimer.Stop();
            _metadataRefreshTimer.Tick -= MetadataRefreshTimerOnTick;

            try
            {

                if (!_mediaInfoThread.Join(TimeSpan.FromSeconds(1)))
                    _mediaInfoThread.Abort();
            }
            catch
            {
                //noop
            }
        }

        protected virtual void OnSelectedEntryMoved()
        {
            SelectedEntryMoved?.Invoke(this, EventArgs.Empty);
        }

        public void SetSelectedItems(IEnumerable<PlaylistEntry> entries)
        {
            SelectedEntries = entries.ToList();
            UpdateSelectedDuration();
        }

        public List<PlaylistEntry> SelectedEntries
        {
            get => _selectedEntries;
            private set
            {
                if (Equals(value, _selectedEntries)) return;
                _selectedEntries = value;
                OnPropertyChanged();
            }
        }

        public bool DeferLoading
        {
            get => _deferLoading;
            set
            {
                if (_deferLoading == value)
                    return;

                _deferLoading = value;

                if (_deferLoading)
                {
                    _addedDuringDefer = false;
                }
                else
                {
                    PlaylistEntriesHaveChanged(_addedDuringDefer);
                }
            }
        }

        protected virtual void OnRequestGenerateThumbnails(string[] videos)
        {
            RequestGenerateThumbnails?.Invoke(this, videos);
        }

        protected virtual void OnRequestGenerateThumbnailBanners(string[] videos)
        {
            RequestGenerateThumbnailBanners?.Invoke(this, videos);
        }

        protected virtual void OnRequestGeneratePreviews(string[] videos)
        {
            RequestGeneratePreviews?.Invoke(this, videos);
        }

        public static void OpenInExplorer(string path)
        {
            ProcessStartInfo info = new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"");
            Process.Start(info);
        }

        protected virtual void OnRequestGenerateHeatmaps(string[] videos)
        {
            RequestGenerateHeatmaps?.Invoke(this, videos);
        }

        protected virtual void OnRequestGenerateAll(string[] videos, bool visible)
        {
            RequestGenerateAll?.Invoke(this, new Tuple<string[], bool>(videos, visible));
        }

        protected virtual void OnEntriesChanged(bool entriesAdded)
        {
            EntriesChanged?.Invoke(this, entriesAdded);
        }

        protected virtual void OnNextOrPreviousChanged()
        {
            NextOrPreviousChanged?.Invoke(this, EventArgs.Empty);
        }

        public void NotifyFileGenerated(string videoFile)
        {
            foreach(var entry in _entries.Where(e => string.Compare(e.Fullname, videoFile, StringComparison.InvariantCultureIgnoreCase) == 0).ToList())
                _uncheckedPlaylistEntries.Enqueue(entry);
        }

        public void SelectNewRandomEntry()
        {
            if (Shuffle)
                UpdateNextEntry();
        }

        protected virtual string OnRequestDirectory(string initialValue = "")
        {
            RequestEventArgs<string> e = new RequestEventArgs<string>(initialValue);
            RequestDirectory?.Invoke(this, e);
            if (!e.Handled)
                return null;

            return e.Value;
        }

        protected virtual string OnRequestHeatmapFileName(string initialValue = "")
        {
            RequestEventArgs<string> e = new RequestEventArgs<string>(initialValue);
            RequestHeatmapFileName?.Invoke(this, e);
            if (!e.Handled)
                return null;

            return e.Value;
        }

        protected virtual string OnRequestPreviewFileName(string initialValue = "")
        {
            RequestEventArgs<string> e = new RequestEventArgs<string>(initialValue);
            RequestPreviewFileName?.Invoke(this, e);
            if (!e.Handled)
                return null;

            return e.Value;
        }
    }
}
