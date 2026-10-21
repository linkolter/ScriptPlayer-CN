using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ScriptPlayer.Dialogs;
using ScriptPlayer.Shared.TheHandy;
using ScriptPlayer.Localization;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private readonly Dictionary<string, ContentControl> deviceEditorHosts = new Dictionary<string, ContentControl>();
        private readonly Dictionary<string, Window> deviceEditorDialogs = new Dictionary<string, Window>();
        private string serialSettingsPage = "TCode Serial";

        private void CreateDeviceConnectionPages(Dictionary<string, FrameworkElement> pages)
        {
            var handy = new StackPanel();
            handy.Children.Add(DeviceDescription("首次填写密钥后点击连接。已有连接修改密钥时，保留原有自动刷新行为。"));
            handy.Children.Add(pages["The Handy"]);
            var status = DeviceDescription("连接结果请查看下方设备列表；发出请求不代表设备已在线。");
            var connect = new Button { Name = "HandySettingsConnect", Content = "连接 / 刷新 Handy", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0,12,0,8) };
            connect.Click += (s,e) =>
            {
                if (string.IsNullOrWhiteSpace(ViewModel.Settings.HandyDeviceId)
                    || ViewModel.Settings.HandyDeviceId == HandyHelper.DefaultDeviceId)
                {
                    status.Text = ChineseLocalization.Translate("请先填写有效的 Handy 连接密钥，再点击连接。");
                    return;
                }
                ViewModel.ConnectHandyDirectlyCommand.Execute(null);
                status.Text = ChineseLocalization.Translate("已请求连接或刷新 Handy；请在设备列表确认结果。");
            };
            handy.Children.Add(connect);
            handy.Children.Add(status);
            handy.Children.Add(CreateManagedDevicesContent());
            pages["The Handy"] = DeviceCard(handy);
            pages["TCode Wi-Fi"] = CreateTCodeConnectionPage("TCode Wi-Fi",
                "电脑与设备连接同一局域网。UDP 就绪表示发送端已打开，不代表设备在线。", "打开连接配置");
            pages["TCode Serial"] = CreateTCodeConnectionPage("TCode Serial",
                "通过 USB 串口连接 OSR6 / SR6，复用现有 TCode 输出。", "刷新串口");
            pages["TCode Bluetooth"] = CreateTCodeConnectionPage("TCode Bluetooth",
                "先在 Windows 中配对支持蓝牙串口（SPP）的设备，再选择其传出 COM 端口。使用现有 115200 串口输出；不支持仅有 BLE GATT 的设备。", "刷新蓝牙串口");
            var manager = new StackPanel();
            manager.Children.Add(DeviceDescription("下方开关控制已有设备是否参与输出。TCode 连接状态单独显示。"));
            manager.Children.Add(TCodeConnectionStatus());
            manager.Children.Add(CreateManagedDevicesContent());
            pages["Device Manager"] = DeviceCard(manager);
        }

        private FrameworkElement CreateManagedDevicesContent()
        {
            // Reuse the live device list and its existing enable switches; no new output state.
            var dialog = new DeviceManagerDialog(ViewModel.Devices);
            var content = (FrameworkElement)dialog.Content;
            dialog.Content = null;
            content.DataContext = dialog;
            foreach (object key in dialog.Resources.Keys) content.Resources[key] = dialog.Resources[key];
            dialog.Close();
            return content;
        }

        private FrameworkElement CreateTCodeConnectionPage(string id, string explanation, string action)
        {
            var panel = new StackPanel();
            panel.Children.Add(DeviceDescription(explanation));
            panel.Children.Add(TCodeConnectionStatus());
            var buttons = new WrapPanel { Margin = new Thickness(0,8,0,16) };
            var refresh = new Button { Content = action, Margin = new Thickness(0,0,8,6) };
            refresh.Click += (s,e) => EnsureDeviceConnectionEditor(id, true);
            var disconnect = new Button { Content = "断开 TCode", Margin = new Thickness(0,0,8,6) };
            disconnect.Click += (s,e) => { if (CanChangeDevicePage()) ViewModel.DisconnectTCodeCommand.Execute(null); };
            buttons.Children.Add(refresh);
            buttons.Children.Add(disconnect);
            panel.Children.Add(buttons);
            var host = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
            deviceEditorHosts[id] = host;
            panel.Children.Add(host);
            return DeviceCard(panel);
        }

        private TextBlock TCodeConnectionStatus()
        {
            var status = DeviceDescription("");
            var binding = new MultiBinding { Converter = new Controls.TCodeStatusDisplayConverter() };
            binding.Bindings.Add(new Binding("TCodeStatus") { Source = ViewModel });
            binding.Bindings.Add(new Binding("Settings.InterfaceLanguage") { Source = ViewModel });
            status.SetBinding(TextBlock.TextProperty, binding);
            return status;
        }

        private TextBlock DeviceDescription(string text)
        {
            var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,0,0,12) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "MediaMutedBrush");
            return label;
        }

        private Border DeviceCard(FrameworkElement content) => new Border { Style = (Style)FindResource("SettingsCard"), Child = content };

        private void OpenDeviceSettings(string id)
        {
            SettingsSearch.Clear();
            OpenAdvancedSettings(id);
        }

        private void DeviceSetup_Click(object sender, RoutedEventArgs e) => OpenDeviceSettings((string)((FrameworkElement)sender).Tag);

        private void EnsureDeviceConnectionEditor(string id, bool refresh = false)
        {
            if (id != "TCode Wi-Fi" && id != "TCode Serial" && id != "TCode Bluetooth") return;
            if (!CanChangeDevicePage()) return;
            if (id != "TCode Wi-Fi") serialSettingsPage = id;
            if (deviceEditorDialogs.TryGetValue(id, out var current))
            {
                if (refresh && current is SerialPortSelector selector)
                {
                    try
                    {
                        var ports = SerialPort.GetPortNames().OrderBy(port => port, StringComparer.OrdinalIgnoreCase).ToList();
                        string previous = selector.SelectedPort;
                        selector.Ports = ports;
                        selector.SelectedPort = ports.FirstOrDefault(port => port == previous) ?? ports.FirstOrDefault();
                    }
                    catch (Exception)
                    {
                        SettingsPageDescription.Text = ChineseLocalization.Translate("无法枚举 COM 端口。请检查 Windows 设备管理器后重试。");
                    }
                }
                return;
            }
            if (id != "TCode Wi-Fi")
            {
                try
                {
                    if (SerialPort.GetPortNames().Length == 0)
                    {
                        var empty = DeviceDescription("没有可用 COM 端口。USB 设备请检查连接；蓝牙设备请先完成 SPP 配对，再刷新串口。");
                        ChineseLocalization.TranslateTree(empty);
                        deviceEditorHosts[id].Content = empty;
                        return;
                    }
                }
                catch (Exception)
                {
                    var error = DeviceDescription("无法枚举 COM 端口。请检查 Windows 设备管理器后重试。");
                    ChineseLocalization.TranslateTree(error);
                    deviceEditorHosts[id].Content = error;
                    return;
                }
            }
            // Existing commands retain ownership of connection, persistence and output semantics.
            if (id == "TCode Wi-Fi") ViewModel.ConnectTCodeWifiCommand.Execute(null);
            else ViewModel.ConnectTCodeCommand.Execute(null);
        }

        internal string DeviceDialogPage(Window dialog)
        {
            if (dialog is TCodeWifiDialog) return "TCode Wi-Fi";
            if (dialog is SerialPortSelector && dialog.Title.IndexOf("TCode", StringComparison.OrdinalIgnoreCase) >= 0)
                return serialSettingsPage;
            return null;
        }

        internal Window ExistingDeviceEditor(string id) => deviceEditorDialogs.TryGetValue(id, out var dialog) ? dialog : null;

        internal void ShowDeviceEditorPage(string id) => OpenDeviceSettings(id);

        internal void AttachDeviceEditor(string id, Window dialog, FrameworkElement content)
        {
            EnsureUnifiedSettings();
            deviceEditorDialogs[id] = dialog;
            deviceEditorHosts[id].Content = content;
            content.Margin = new Thickness(0);
            OpenDeviceSettings(id);
        }

        internal void DetachDeviceEditor(string id, Window dialog)
        {
            if (!deviceEditorDialogs.TryGetValue(id, out var current) || current != dialog) return;
            deviceEditorDialogs.Remove(id);
            deviceEditorHosts[id].Content = null;
        }

        internal bool CanChangeDevicePage()
        {
            if (!deviceEditorDialogs.Values.OfType<TCodeWifiDialog>().Any(dialog => dialog.IsConnecting)) return true;
            SettingsPageDescription.Text = ChineseLocalization.Translate("设备正在连接，请等待结果后再切换、断开或退出。");
            // WPF selectors update their value before Click/SelectionChanged reaches the gate.
            Action restore = () =>
            {
                if (!string.IsNullOrEmpty(SettingsSearch.Text)) SettingsSearch.Clear();
                foreach (var pair in settingsNavigation) pair.Value.IsChecked = pair.Key == selectedSettingsPage;
                updatingSettingsNavigation = true;
                try { CompactSettingsNavigator.SelectedValue = selectedSettingsPage; }
                finally { updatingSettingsNavigation = false; }
                HomeNav.IsChecked = currentPage == "home";
                LibraryNav.IsChecked = currentPage == "library";
                NowPlayingNav.IsChecked = currentPage == "player";
                SettingsNav.IsChecked = currentPage == "settings";
            };
            restore();
            // SelectedValue is committed after SelectionChanged, so restore once that event finishes.
            Dispatcher.BeginInvoke(restore);
            return false;
        }

        private bool LeaveDeviceEditors(string keep = null)
        {
            if (!CanChangeDevicePage()) return false;
            foreach (var pair in deviceEditorDialogs.ToArray())
                if (pair.Key != keep) InlineDialogService.Complete(pair.Value, false);
            return true;
        }
    }
}
