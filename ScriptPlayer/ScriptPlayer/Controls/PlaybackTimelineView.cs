using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Controls
{
    // One timeline in both shells. The preview stays in the existing video overlay tree.
    public sealed class PlaybackTimelineView : UserControl
    {
        public Border HeatContainer { get; }
        public HeatMapSeekBar HeatSeek { get; }
        public MediaSeekBar ProgressSeek { get; }
        public TimelineMarkerBar MarkerBar { get; }
        public TextBlock CurrentTime { get; }
        public TextBlock TotalTime { get; }
        private readonly Grid track, surface;
        private readonly Border preview;
        private readonly ThumbnailPreview image;
        private readonly Image generatedImage;
        private readonly TimelinePreviewService previewService = new TimelinePreviewService();
        private CancellationTokenSource previewCancellation;
        private long previewRequest;
        public Func<string> PreviewExecutableProvider { get; set; }
        public BitmapSource GeneratedPreviewFrame => generatedImage.Source as BitmapSource;
        private readonly PositionBar positions;
        private readonly TextBlock previewTime, previewMarker;
        private TimelineViewModel model;
        private bool transportVisible = true;
        private bool compact;
        private long hoverSession;
        private SeekBar hoverSource;
        public bool IsPreviewVisible => preview.Visibility == Visibility.Visible;
        public string PreviewMarkerText => previewMarker.Text;
        public TimeSpan PreviewPosition => image.TimeStamp;
        // Leave a real bottom target below the persistent strip, rather than
        // treating the video area above the heatmap as a transport hot zone.
        public double BottomRevealHeight => !transportVisible && !compact ? 12 : 0;
        public double PersistentHeight => (model?.ShowHeatMap == true ? HeatSeek.Height : 0) + (model?.Markers.Count > 0 ? MarkerBar.Height : 0) + BottomRevealHeight;
        public bool TransportVisible
        {
            get => transportVisible;
            set { transportVisible = value; ApplyVisibility(); }
        }
        public bool Compact { get => compact; set { compact = value; ApplyVisibility(); } }
        public PlaybackTimelineView()
        {
            Focusable = false;
            Foreground = Brushes.White;
            Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0));
            surface = new Grid { Margin = new Thickness(16, 0, 16, 0), ClipToBounds = false };
            surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            surface.ColumnDefinitions.Add(new ColumnDefinition());
            surface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            for (int i = 0; i < 3; i++) surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            HeatSeek = new HeatMapSeekBar { Height = 18, ShowPreview = false, Cursor = Cursors.Hand };
            AutomationProperties.SetName(HeatSeek, "脚本热力图，点击跳转");
            HeatContainer = new Border { Child = HeatSeek, Margin = new Thickness(8, 0, 8, 0) };
            Grid.SetColumn(HeatContainer, 1); Grid.SetRow(HeatContainer, 1); surface.Children.Add(HeatContainer);
            MarkerBar = new TimelineMarkerBar { Height = 18, Margin = new Thickness(8, 0, 8, 0), ShowPreview = false, Cursor = Cursors.Hand };
            AutomationProperties.SetName(MarkerBar, "书签、AB 点和上次暂停退出位置");
            Grid.SetColumn(MarkerBar, 1); surface.Children.Add(MarkerBar);
            track = new Grid { Height = 22 };
            Grid.SetColumnSpan(track, 3); Grid.SetRow(track, 2); surface.Children.Add(track);
            track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); track.ColumnDefinitions.Add(new ColumnDefinition()); track.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
            CurrentTime = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            TotalTime = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            ProgressSeek = new MediaSeekBar { Height = 22, Margin = new Thickness(8, 0, 8, 0), ShowPreview = false, Cursor = Cursors.Hand };
            AutomationProperties.SetName(ProgressSeek, "播放进度");
            Grid.SetColumn(ProgressSeek, 1); Grid.SetColumn(TotalTime, 2); track.Children.Add(CurrentTime); track.Children.Add(ProgressSeek); track.Children.Add(TotalTime);
            image = new ThumbnailPreview { AutoSize = false, Width = 224, Height = 126 };
            generatedImage = new Image { Width = 224, Height = 126, Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
            positions = new PositionBar { Height = 22, Width = 224, TimeFrameContext = new TimeFrameContext { TotalDisplayedDuration = TimeSpan.FromSeconds(16) }, DrawCircles = false, DrawLines = false, DrawZero = true, Background = Brushes.Black };
            previewTime = new TextBlock { FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
            previewMarker = new TextBlock { Foreground = Brushes.LightSkyBlue, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxWidth = 224 };
            var stack = new StackPanel(); stack.Children.Add(image); stack.Children.Add(generatedImage); stack.Children.Add(positions); stack.Children.Add(previewTime); stack.Children.Add(previewMarker);
            preview = new Border { Child = stack, Background = new SolidColorBrush(Color.FromRgb(32, 32, 36)), BorderBrush = Brushes.DimGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Visibility = Visibility.Collapsed, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
            var previewLayer = new Canvas { Height = 0, ClipToBounds = false, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumnSpan(previewLayer, 3); Panel.SetZIndex(previewLayer, 5); previewLayer.Children.Add(preview); surface.Children.Add(previewLayer);
            foreach (var seek in new SeekBar[] { HeatSeek, ProgressSeek, MarkerBar })
            {
                seek.Seek += (s, r, t, phase) => model?.Seek(r, t, phase);
                seek.HoverChanged += PreviewHover;
                seek.HoverEnded += s => ClosePreview();
            }
            Content = surface;
            Unloaded += (s, e) => ClosePreview();
            IsVisibleChanged += (s, e) => { if (!IsVisible) ClosePreview(); };
            SizeChanged += (s, e) => ClosePreview();
        }
        public void Attach(TimelineViewModel value)
        {
            if (model != null) { model.PropertyChanged -= ModelChanged; model.SessionInvalidated -= SessionChanged; }
            model = value;
            if (model == null) return;
            model.PropertyChanged += ModelChanged; model.SessionInvalidated += SessionChanged;
            foreach (var seek in new SeekBar[] { HeatSeek, ProgressSeek, MarkerBar })
            {
                seek.SetBinding(SeekBar.ProgressProperty, new Binding("State.Position") { Source = model });
                seek.SetBinding(SeekBar.DurationProperty, new Binding("State.Duration") { Source = model });
            }
            HeatSeek.SetBinding(SeekBar.OverlayProperty, new Binding("HeatMap") { Source = model });
            MarkerBar.SetBinding(TimelineMarkerBar.MarkersProperty, new Binding("Markers") { Source = model });
            CurrentTime.SetBinding(TextBlock.TextProperty, new Binding("State.Position") { Source = model, Converter = new PlaybackTimeConverter() });
            TotalTime.SetBinding(TextBlock.TextProperty, new Binding("State.Duration") { Source = model, Converter = new PlaybackTimeConverter() });
            ApplyVisibility();
        }
        private void ModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TimelineViewModel.State))
            {
                // Replacing the cache invalidates the displayed frame, even at an unchanged pointer.
                ClosePreview(); ApplyVisibility();
            }
        }
        private void SessionChanged(object sender, EventArgs e)
        {
            ClosePreview();
            HeatSeek.ResetInteraction(); ProgressSeek.ResetInteraction(); MarkerBar.ResetInteraction();
        }
        private void ApplyVisibility()
        {
            HeatContainer.Visibility = model?.ShowHeatMap == true ? Visibility.Visible : Visibility.Collapsed;
            HeatSeek.Height = compact ? 12 : 18;
            surface.Margin = new Thickness(16, 0, 16, BottomRevealHeight);
            MarkerBar.Visibility = model?.Markers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            track.Visibility = transportVisible ? Visibility.Visible : Visibility.Collapsed;
            if (!transportVisible) ProgressSeek.ResetInteraction();
        }
        private void PreviewHover(SeekBar seek, TimeSpan time)
        {
            if (model == null || model.State.Status != PlaybackStatus.Ready || model.State.Duration <= TimeSpan.Zero) { ClosePreview(); return; }
            CancelPreviewRequest();
            hoverSource = seek; hoverSession = model.State.SessionId;
            generatedImage.Source = null; generatedImage.Visibility = Visibility.Collapsed;
            image.Thumbnails = model.Thumbnails;
            image.TimeStamp = time;
            image.Visibility = model.Thumbnails?.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            positions.Positions = model.Positions; positions.TimeFrameContext.Progress = time;
            positions.Visibility = model.Positions != null ? Visibility.Visible : Visibility.Collapsed;
            previewTime.Text = string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
            previewMarker.Text = model.MarkerLabel(time, seek.ActualWidth);
            preview.Visibility = Visibility.Visible;
            PositionPreview(seek, time);
            if (hoverSession != model.State.SessionId || hoverSource != seek) { ClosePreview(); return; }
            if (model.Thumbnails?.Count > 0 || PreviewExecutableProvider == null) return;
            var request = ++previewRequest;
            var cancellation = new CancellationTokenSource();
            previewCancellation = cancellation;
            _ = LoadGeneratedPreview(seek, time, hoverSession, model.State.MediaPath, request, cancellation);
        }

        private void PositionPreview(SeekBar seek, TimeSpan time)
        {
            preview.Measure(new Size(Math.Max(0, ActualWidth - 32), double.PositiveInfinity));
            var point = seek.TranslatePoint(new Point(seek.ActualWidth * time.TotalSeconds / model.State.Duration.TotalSeconds, 0), surface);
            double width = preview.DesiredSize.Width;
            preview.RenderTransform = new TranslateTransform(Math.Max(0, Math.Min(surface.ActualWidth - width, point.X - width / 2)), -preview.DesiredSize.Height - 8);
        }

        private async Task LoadGeneratedPreview(SeekBar seek, TimeSpan time, long session, string media, long request, CancellationTokenSource cancellation)
        {
            try
            {
                var frame = await previewService.RequestAsync(PreviewExecutableProvider(), media,
                    TimeSpan.FromSeconds(Math.Min(time.TotalSeconds, Math.Max(0, model.State.Duration.TotalSeconds - 0.1))), cancellation.Token);
                if (cancellation.IsCancellationRequested || request != previewRequest || model == null || session != model.State.SessionId
                    || model.State.Status != PlaybackStatus.Ready || media != model.State.MediaPath || hoverSource != seek || !IsPreviewVisible) return;
                if (frame == null) return;
                generatedImage.Source = frame; generatedImage.Visibility = Visibility.Visible;
                PositionPreview(seek, time);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Timeline preview: " + ex.Message); }
            finally
            {
                if (ReferenceEquals(previewCancellation, cancellation)) previewCancellation = null;
                cancellation.Dispose();
            }
        }

        private void CancelPreviewRequest()
        {
            previewRequest++;
            var cancellation = previewCancellation; previewCancellation = null;
            cancellation?.Cancel();
        }
        public void ClosePreview()
        {
            CancelPreviewRequest(); generatedImage.Source = null; generatedImage.Visibility = Visibility.Collapsed;
            preview.Visibility = Visibility.Collapsed; image.Thumbnails = null; positions.Positions = null; hoverSource = null;
        }
        public void DisposePreview() { ClosePreview(); PreviewExecutableProvider = null; previewService.Dispose(); }
    }
}
