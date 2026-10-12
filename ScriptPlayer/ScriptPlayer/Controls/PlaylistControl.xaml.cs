using System;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScriptPlayer.Dialogs;
using ScriptPlayer.Metadata;
using ScriptPlayer.Localization;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Controls
{
    /// <summary>
    /// Interaction logic for PlaylistControl.xaml
    /// </summary>
    public partial class PlaylistControl : UserControl
    {
        public static readonly DependencyProperty MetadataStatusMessageProperty = DependencyProperty.Register(
            nameof(MetadataStatusMessage), typeof(string), typeof(PlaylistControl), new PropertyMetadata(string.Empty));
        public string MetadataStatusMessage { get => (string)GetValue(MetadataStatusMessageProperty); private set => SetValue(MetadataStatusMessageProperty, value); }
        private string _metadataStatusFormat;
        private object[] _metadataStatusArguments = new object[0];

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            "ViewModel", typeof(MainViewModel), typeof(PlaylistControl), new PropertyMetadata(default(MainViewModel), OnViewModelPropertyChanged));

        private static void OnViewModelPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((PlaylistControl) d).ViewModelChanged((MainViewModel) e.OldValue, (MainViewModel) e.NewValue);
        }

        public static readonly DependencyProperty ShowHeatmapProperty = DependencyProperty.Register(
            "ShowHeatmap", typeof(bool), typeof(PlaylistControl), new PropertyMetadata(default(bool)));
        public static readonly DependencyProperty PreviewWidthProperty = DependencyProperty.Register(nameof(PreviewWidth), typeof(double), typeof(PlaylistControl), new PropertyMetadata(104.0));
        public double PreviewWidth { get => (double)GetValue(PreviewWidthProperty); private set => SetValue(PreviewWidthProperty, value); }
        public static readonly DependencyProperty DetailsVisibleProperty = DependencyProperty.Register(nameof(DetailsVisible), typeof(Visibility), typeof(PlaylistControl), new PropertyMetadata(Visibility.Collapsed));
        public Visibility DetailsVisible { get => (Visibility)GetValue(DetailsVisibleProperty); private set => SetValue(DetailsVisibleProperty, value); }

        public bool ShowHeatmap
        {
            get { return (bool) GetValue(ShowHeatmapProperty); }
            set { SetValue(ShowHeatmapProperty, value); }
        }

        private void ViewModelChanged(MainViewModel oldValue, MainViewModel newValue)
        {
            if (oldValue != null)
            {
                oldValue.Playlist.SelectedEntryMoved -= PlaylistOnSelectedEntryMoved;
            }

            if (newValue != null)
            {
                newValue.Playlist.SelectedEntryMoved += PlaylistOnSelectedEntryMoved;
            }
        }

        public PlaylistControl()
        {
            InitializeComponent();
            SizeChanged += OnSizeChanged;
            Loaded += (s, e) => { ChineseLocalization.LanguageChanged -= MetadataLanguageChanged; ChineseLocalization.LanguageChanged += MetadataLanguageChanged; MetadataLanguageChanged(this, EventArgs.Empty); };
            Unloaded += (s, e) => ChineseLocalization.LanguageChanged -= MetadataLanguageChanged;
        }

        private void SetMetadataStatus(string format, params object[] arguments)
        {
            _metadataStatusFormat = format;
            _metadataStatusArguments = arguments;
            MetadataLanguageChanged(this, EventArgs.Empty);
        }

        private void MetadataLanguageChanged(object sender, EventArgs e)
        {
            MetadataStatusMessage = string.IsNullOrEmpty(_metadataStatusFormat) ? string.Empty :
                ChineseLocalization.TranslateFormat(_metadataStatusFormat, _metadataStatusArguments);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs)
        {
            ShowHeatmap = this.ActualWidth > 350;
            bool detailed = ActualWidth >= 900;
            DetailsVisible = detailed ? Visibility.Visible : Visibility.Collapsed;
            PreviewWidth = ActualWidth < 760 ? 104 : 162;

            // Keep search and filters in the measured layout instead of overlapping the list.
            bool narrow = ActualWidth < 600;
            Grid.SetColumnSpan(SearchBorder, narrow ? 3 : 1);
            SearchBorder.Margin = narrow ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 8, 0);
            Grid.SetRow(TagFilter, narrow ? 1 : 0);
            Grid.SetColumn(TagFilter, narrow ? 0 : 1);
            Grid.SetColumnSpan(TagFilter, narrow ? 2 : 1);
            Grid.SetRow(FavoriteFilter, narrow ? 1 : 0);
        }

        public MainViewModel ViewModel
        {
            get => (MainViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private void PlaylistOnSelectedEntryMoved(object sender, EventArgs eventArgs)
        {
            if (ViewModel.Playlist.SelectedEntry == null)
                return;

            lstEntries.ScrollIntoView(ViewModel.Playlist.SelectedEntry);
        }

        private void PlaylistEntry_DoubleClicked(object sender, MouseButtonEventArgs e)
        {
            ListBoxItem item = sender as ListBoxItem;
            if (!(item?.DataContext is PlaylistEntry entry))
                return;

            ViewModel.Playlist.RequestPlayEntry(entry);
        }

        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ViewModel.Playlist.AddEntries(false, files);
        }

        private void LstEntries_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.Playlist.SetSelectedItems(((ListBox)sender).SelectedItems.Cast<PlaylistEntry>());
        }

        private void ToolTip_OnOpened(object sender, RoutedEventArgs e)
        {
            var tooltip = (ToolTip)sender;
            PlaylistEntry entry = ((FrameworkElement)tooltip.PlacementTarget).DataContext as PlaylistEntry;

            VideoDetailsPreview preview = tooltip.GetChildOfType<VideoDetailsPreview>();
            preview.ViewModel = ViewModel;
            preview.Entry = entry;
        }

        private void MnuScrollToCurrent(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Playlist?.FilteredEntries == null || ViewModel.LoadedFiles == null)
                return;

            var entry = ViewModel.Playlist.GetFilteredEntry(ViewModel.LoadedFiles);

            if (entry == null)
                return;

            ViewModel.Playlist.SelectedEntry = entry;
            ViewModel.Playlist.SetSelectedItems(new[] { entry });
            lstEntries.ScrollIntoView(entry);
        }

        private void MnuEditNfo_OnClick(object sender, RoutedEventArgs e)
        {
            PlaylistEntry entry = ((MenuItem)sender).CommandParameter as PlaylistEntry;
            if (entry == null)
                return;

            SetMetadataStatus(null);
            try
            {
                // NfoPath is the resolved association; scripts and their media may live in different folders.
                if (string.IsNullOrWhiteSpace(entry.NfoPath)) throw new InvalidOperationException("媒体元数据尚未加载，请稍后重试。");
                NfoEditorDialog dialog = new NfoEditorDialog(entry.NfoPath, entry.CreateMetadataDraft()) { Owner = Window.GetWindow(this) };
                if (dialog.ShowDialog() == true && dialog.SavedMetadata != null)
                    PublishMetadata(dialog.SavedMetadata);
            }
            catch (Exception ex) { SetMetadataStatus("保存 NFO 失败：{0}", ex.Message); }
        }

        private void MnuOpenSource_OnClick(object sender, RoutedEventArgs e)
        {
            PlaylistEntry entry = ((MenuItem)sender).CommandParameter as PlaylistEntry;
            Uri uri;
            if (entry == null || !Uri.TryCreate(entry.SourceUrl, UriKind.Absolute, out uri))
            {
                MessageBox.Show(Window.GetWindow(this), "此条目没有有效的 EroScripts 来源网址。", "EroScripts",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }

        private void Favorite_OnClick(object sender, RoutedEventArgs e)
        {
            PlaylistEntry entry = ((FrameworkElement)sender).DataContext as PlaylistEntry;
            if (entry == null)
                return;

            e.Handled = true;
            SetMetadataStatus(null);
            NfoMetadata committed;
            try { committed = ViewModel.Playlist.SaveEntryFavorite(entry, !entry.IsFavorite); }
            catch (Exception ex)
            {
                SetMetadataStatus("保存 NFO 失败：{0}", ex.Message);
                return;
            }
            PublishMetadata(committed);
        }

        private void PublishMetadata(NfoMetadata committed)
        {
            // Physical commit has already succeeded. Presentation failures must not invite another toggle.
            string refreshWarning;
            try { refreshWarning = ViewModel.Playlist.PublishCommittedMetadata(committed); }
            catch (Exception ex) { refreshWarning = ex.Message; }
            string warning = string.Join(Environment.NewLine, new[] { committed.LastSaveWarning, refreshWarning }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (!string.IsNullOrWhiteSpace(warning)) SetMetadataStatus("NFO 已保存，但有提示：{0}", committed.SidecarPath + Environment.NewLine + warning);
            else SetMetadataStatus("NFO 已保存：{0}", committed.SidecarPath);
        }

        private void UserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                ViewModel.Playlist.RemoveSelectedEntryCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
