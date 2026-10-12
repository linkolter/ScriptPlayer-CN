using System;
using System.Windows;
using System.Windows.Media;
using ScriptPlayer.Shared;

namespace ScriptPlayer.Controls
{
    // Retain the existing seek/drag and thumbnail behavior, with a larger pointer target.
    public sealed class MediaSeekBar : SeekBar
    {
        public MediaSeekBar() { SetResourceReference(ForegroundProperty, "MediaAccentBrush"); }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            double width = Math.Max(0, ActualWidth), y = ActualHeight / 2;
            if (width <= 0 || ActualHeight <= 0) return;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(115, 115, 115)), null,
                new Rect(0, y - 2, ActualWidth, 4), 2, 2);
            if (Overlay != null && Overlay != Brushes.Transparent)
                dc.DrawRectangle(Overlay, null, new Rect(0, y - 2, ActualWidth, 4));
            double ratio = Duration.TotalSeconds > 0 ? Math.Max(0, Math.Min(1, Progress.TotalSeconds / Duration.TotalSeconds)) : 0;
            // SeekBar clips its drawing. Reserve the full knob radius at both ends.
            double radius = Math.Min(10, Math.Min(width / 2, ActualHeight / 2));
            double x = Math.Max(radius, Math.Min(width - radius, ratio * width));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(68, 68, 68)), null, new Point(x, y), radius, radius);
            dc.DrawEllipse(Foreground ?? Brushes.DarkOrange, null, new Point(x, y), Math.Min(5, radius), Math.Min(5, radius));
        }
    }
}
