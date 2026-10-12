using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Newtonsoft.Json;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private Dictionary<string, TimeSpan> pausedExitPoints = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        private ListCollectionView playbackListView;
        private static string PausedExitPath => Path.Combine(Environment.ExpandEnvironmentVariables("%APPDATA%"), "ScriptPlayer", "PausedExitPoints.json");

        private void InitializePlaybackList()
        {
            // Separate from ordinary resume state: v3 and switching files must never overwrite it.
            try
            {
                if (File.Exists(PausedExitPath))
                {
                    var saved = JsonConvert.DeserializeObject<Dictionary<string, TimeSpan>>(File.ReadAllText(PausedExitPath));
                    if (saved != null)
                        foreach (var pair in saved.Where(x => x.Value >= TimeSpan.Zero))
                            pausedExitPoints[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            ViewModel.Playlist.EntriesChanged += PlaybackEntriesChanged;
            RefreshPlaybackList();
        }

        private void PlaybackEntriesChanged(object sender, bool added)
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefreshPlaybackList)); return; }
            RefreshPlaybackList();
        }

        private void RefreshPlaybackList()
        {
            if (playbackListView == null || playbackListView.SourceCollection != ViewModel.Playlist.Entries)
            {
                playbackListView = new ListCollectionView(ViewModel.Playlist.Entries);
                playbackListView.Filter = item =>
                {
                    var entry = (PlaylistEntry)item;
                    string query = ShellSearch.Text.Trim();
                    return !entry.Removed && (query.Length == 0 ||
                        (entry.DisplayTitle ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (entry.Fullname ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
                };
                PlaybackMediaList.ItemsSource = playbackListView;
            }
            foreach (var entry in ViewModel.Playlist.Entries.Concat(recentMedia))
                entry.PausedExitPosition = !string.IsNullOrEmpty(entry.Fullname) && pausedExitPoints.TryGetValue(entry.Fullname, out var position) ? (TimeSpan?)position : null;
            playbackListView.Refresh();
        }

        private void PlaybackMedia_Click(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).DataContext is PlaylistEntry entry)) return;
            if (string.Equals(entry.Fullname, ViewModel.LoadedMedia, StringComparison.OrdinalIgnoreCase)) return;
            if (!File.Exists(entry.Fullname)) { MessageBox.Show(this, "文件已移动或删除，请重新添加。", "找不到媒体"); return; }
            ViewModel.Playlist.RequestPlayEntry(entry);
        }

        private void SavePausedExitPoint()
        {
            var source = ViewModel.PlaybackState;
            string file = ViewModel.LoadedMedia;
            // Called before shutdown pauses/disposes the engine. A transition pause has no marker.
            if (source.Status != Playback.PlaybackStatus.Ready || source.IsPlaying || string.IsNullOrWhiteSpace(file) ||
                !string.Equals(file, ViewModel.PausedExitMedia, StringComparison.OrdinalIgnoreCase) ||
                source.Duration <= TimeSpan.Zero || source.Position < TimeSpan.Zero) return;
            pausedExitPoints[file] = source.Position > source.Duration ? source.Duration : source.Position;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PausedExitPath));
                string temporary = PausedExitPath + ".tmp";
                File.WriteAllText(temporary, JsonConvert.SerializeObject(pausedExitPoints, Formatting.Indented));
                if (File.Exists(PausedExitPath)) File.Replace(temporary, PausedExitPath, null);
                else File.Move(temporary, PausedExitPath);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        }
    }
}
