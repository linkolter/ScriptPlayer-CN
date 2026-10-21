using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;

namespace ScriptPlayer.Controls
{
    public partial class QuickConnectHome : UserControl
    {
        public QuickConnectHome() => InitializeComponent();
        public event Action<string> DeviceSelected;
        private void Device_Click(object sender, RoutedEventArgs e) => DeviceSelected?.Invoke((string)((FrameworkElement)sender).Tag);
    }

    // Translate only the fixed wrapper of the existing status projection, never endpoints/axes.
    public sealed class TCodeStatusDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string status = values.Length > 0 ? values[0] as string ?? "" : "";
            if (values.Length < 2 || values[1] as string != "en") return status;
            const string prefix = "TCode / OSR2 / SR6（";
            if (!status.StartsWith(prefix, StringComparison.Ordinal) || !status.EndsWith("）", StringComparison.Ordinal)) return status;
            string body = status.Substring(prefix.Length, status.Length - prefix.Length - 1);
            if (body == "未连接") body = "not connected";
            else
            {
                int split = body.LastIndexOf('；');
                if (split >= 0)
                {
                    string axes = body.Substring(split + 1);
                    body = body.Substring(0, split) + "; " + (axes == "未加载脚本" ? "no script loaded" : axes);
                }
            }
            return "TCode / OSR2 / SR6 (" + body + ")";
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
