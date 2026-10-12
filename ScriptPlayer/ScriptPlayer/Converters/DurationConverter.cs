using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ScriptPlayer.Converters
{
    public class DurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan duration)
            {
                if (duration <= TimeSpan.Zero)
                    return "读取中…";

                int hours = duration.Days * 24 + duration.Hours;
                return $"{hours:0}:{duration.Minutes:00}:{duration.Seconds:00}";
            }

            return "读取中…";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
