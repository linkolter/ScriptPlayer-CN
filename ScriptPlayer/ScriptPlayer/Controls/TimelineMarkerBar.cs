using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ScriptPlayer.Playback;
using ScriptPlayer.Shared;

namespace ScriptPlayer.Controls
{
    public sealed class TimelineMarkerBar : SeekBar
    {
        public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(nameof(Markers), typeof(IReadOnlyList<TimelineMarker>), typeof(TimelineMarkerBar), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public IReadOnlyList<TimelineMarker> Markers { get => (IReadOnlyList<TimelineMarker>)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (Duration <= TimeSpan.Zero || ActualWidth <= 0 || Markers == null) return;
            var a = Markers.FirstOrDefault(x => x.Kind == TimelineMarkerKind.LoopA);
            var b = Markers.FirstOrDefault(x => x.Kind == TimelineMarkerKind.LoopB);
            if (a != null && b != null)
            {
                double left = Math.Min(X(a), X(b)), right = Math.Max(X(a), X(b));
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(65, 255, 150, 63)), null, new Rect(left, 6, right - left, 9));
            }
            foreach (var item in Markers)
            {
                double x = Math.Max(3, Math.Min(ActualWidth - 3, X(item)));
                var brush = item.Kind == TimelineMarkerKind.Bookmark ? Brushes.LightSkyBlue : item.Kind == TimelineMarkerKind.PausedExit ? Brushes.MediumPurple : Brushes.SandyBrown;
                // Separate lanes keep a bookmark and an exit point at the same time visible.
                double y = item.Kind == TimelineMarkerKind.PausedExit ? 13 : item.Kind == TimelineMarkerKind.Bookmark ? 8 : 3;
                dc.DrawEllipse(brush, null, new Point(x, y), 3, 3);
                if (item.Kind == TimelineMarkerKind.LoopA || item.Kind == TimelineMarkerKind.LoopB)
                    dc.DrawText(new FormattedText(item.Kind == TimelineMarkerKind.LoopA ? "A" : "B", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), new Point(Math.Max(0, Math.Min(ActualWidth - 9, x - 4)), 5));
            }
        }
        private double X(TimelineMarker marker) => marker.Position.TotalSeconds / Duration.TotalSeconds * ActualWidth;
        protected override void OnSeek(double relative, TimeSpan absolute, int phase)
        {
            var nearest = Markers?.OrderBy(x => Math.Abs(X(x) - relative * ActualWidth)).FirstOrDefault();
            if (nearest != null && Math.Abs(X(nearest) - relative * ActualWidth) <= 7) absolute = nearest.Position;
            base.OnSeek(relative, absolute, phase);
        }
    }
}
