using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScriptPlayer.Shared.Devices;

namespace ScriptPlayer.Dialogs
{
    public sealed class TCodeWifiDialog : Window
    {
        private bool busy;
        public bool IsConnecting => busy;
        private static string ErrorMessage(Exception ex)
        {
            while (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            return ex.Message;
        }

        public TCodeWifiDialog(Func<string, Task> connectDevice, Action disconnectDevice)
        {
            Title = "OSR6 / SR6 Wi-Fi 连接";
            Width = 510;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Brushes.White;
            Foreground = Brushes.Black;
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.Children.Add(new TextBlock { Text = "通过 Wi-Fi 发送 TCode 多轴脚本", FontSize = 19, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) });
            panel.Children.Add(new TextBlock { Text = "设备地址（主机名或 IP : 端口）", Margin = new Thickness(0,0,0,6) });
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OSR6-WiFi.txt");
            string saved = "tcode.local:8000";
            try { if (File.Exists(path)) saved = File.ReadAllText(path).Trim(); } catch { }
            var address = new TextBox { Text = saved, FontSize = 16, Padding = new Thickness(8), Margin = new Thickness(0,0,0,12) };
            panel.Children.Add(address);
            panel.Children.Add(new TextBlock { Text = "电脑和设备需连接同一局域网。默认 UDP 端口为 8000。\n若 tcode.local 无法解析，请改填设备后台显示的 IP。", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0,0,0,12) });
            var status = new TextBlock { Text = "UDP 就绪仅表示发送端已打开，设备是否收到需在播放时确认。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,18) };
            panel.Children.Add(status);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var connect = new Button { Content = "连接", MinWidth = 90, Padding = new Thickness(10,7,10,7), IsDefault = true, Margin = new Thickness(0,0,8,0) };
            var disconnect = new Button { Content = "断开", MinWidth = 75, Padding = new Thickness(10,7,10,7), Margin = new Thickness(0,0,8,0) };
            var close = new Button { Content = "关闭", MinWidth = 75, Padding = new Thickness(10,7,10,7), IsCancel = true };
            close.Click += delegate { Close(); };
            Closing += (s,e) => { if (busy) e.Cancel = true; };
            connect.Click += async delegate
            {
                Uri endpoint;
                try { endpoint = TCodeTransport.ParseEndpoint(address.Text); }
                catch (Exception ex) { status.Text = ErrorMessage(ex); status.Foreground = Brushes.Firebrick; return; }
                busy = true; connect.IsEnabled = disconnect.IsEnabled = close.IsEnabled = address.IsEnabled = false;
                status.Text = "正在解析地址并打开 UDP 发送端…"; status.Foreground = Brushes.DimGray;
                try
                {
                    await connectDevice(endpoint.AbsoluteUri);

                    string saveWarning = "";
                    try { File.WriteAllText(path, endpoint.Authority, Encoding.UTF8); }
                    catch { saveWarning = "\n地址未能保存，下次需重新输入。"; }
                    status.Text = "UDP 发送端已就绪：" + endpoint.Authority + "\n关闭此面板后即可播放。UDP 不提供设备在线确认。" + saveWarning;
                    status.Foreground = Brushes.DarkGreen;
                }
                catch (Exception ex)
                {

                    status.Text = "连接失败：" + ErrorMessage(ex) + "\n请检查地址；主机名解析失败时可改用局域网 IP。";
                    status.Foreground = Brushes.Firebrick;
                }
                finally { busy = false; connect.IsEnabled = disconnect.IsEnabled = close.IsEnabled = address.IsEnabled = true; }
            };
            disconnect.Click += delegate { disconnectDevice(); status.Text = "已断开 TCode 连接。"; status.Foreground = Brushes.DimGray; };
            buttons.Children.Add(connect); buttons.Children.Add(disconnect); buttons.Children.Add(close);
            panel.Children.Add(buttons); Content = panel; 
        }
    }
}
