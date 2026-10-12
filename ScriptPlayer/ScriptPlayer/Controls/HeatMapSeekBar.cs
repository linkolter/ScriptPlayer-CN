using System;
using System.Windows;
using System.Windows.Media;
using ScriptPlayer.Shared;
namespace ScriptPlayer.Controls
{
    public sealed class PlaybackTimeConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var time = value is TimeSpan ? (TimeSpan)value : TimeSpan.Zero;
            return string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
    // Dedicated seek surface: its lifetime is independent of the transport controls.
    public sealed class HeatMapSeekBar : SeekBar
    {
        protected override void OnRender(DrawingContext dc)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            double top = 4, height = Math.Max(1, ActualHeight - 8);
            dc.DrawRoundedRectangle(Overlay ?? Brushes.DimGray, null, new Rect(0, top, ActualWidth, height), 3, 3);
            double ratio = Duration.TotalSeconds > 0 ? Math.Max(0, Math.Min(1, Progress.TotalSeconds / Duration.TotalSeconds)) : 0;
            double x = Math.Max(1, Math.Min(ActualWidth - 1, ratio * ActualWidth));
            dc.DrawLine(new Pen(Brushes.Black, 4), new Point(x, 1), new Point(x, ActualHeight - 1));
            dc.DrawLine(new Pen(Brushes.White, 2), new Point(x, 1), new Point(x, ActualHeight - 1));
        }
    }
}
