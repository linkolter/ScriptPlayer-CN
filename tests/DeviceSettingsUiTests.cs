using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using ScriptPlayer;
using ScriptPlayer.Controls;
using ScriptPlayer.Dialogs;
using ScriptPlayer.Localization;
using ScriptPlayer.ViewModels;

internal static class DeviceSettingsUiTests
{
    private static MainWindow window;
    private static string output;
    private static int passed;
    private static TaskCompletionSource<bool> pendingConnection;
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException(name);
        passed++;
        File.AppendAllText(Path.Combine(output, "results.log"), "PASS: " + name + "\n");
    }
    private static void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, args);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static Dictionary<string, FrameworkElement> Sections => (Dictionary<string, FrameworkElement>)typeof(MainWindow).GetField("settingsSections", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
    private static void Snapshot(string name)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(output, name + ".png"))) encoder.Save(stream);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        output = args[1];
        Directory.CreateDirectory(output);
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        ChineseLocalization.Initialize();
        string instance = Guid.NewGuid().ToString("N");
        InstanceHandler.Startup("DeviceUi-Test-" + instance, "DeviceUi-Pipe-" + instance);
        XNamespace p = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources = XDocument.Load(args[0]).Root.Element(p + "Application.Resources").Element(p + "ResourceDictionary");
        app.Resources = (ResourceDictionary)XamlReader.Parse(resources.ToString(), new ParserContext { BaseUri = new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml") });
        window = new MainWindow();
        window.InitializeComponent();
        app.MainWindow = window;
        bool done = false;
        var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(55) };
        timeout.Tick += (s,e) => { if (!done) { File.AppendAllText(Path.Combine(output,"results.log"), "ERROR: test deadline\n"); Environment.Exit(2); } };
        window.Loaded += (s,e) => app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                await Task.Delay(400);
                var home = (QuickConnectHome)window.FindName("HomeDeviceConnect");
                Check(home.IsVisible, "Home starts with device selection");
                Check(home.TranslatePoint(new Point(),window).Y < ((FrameworkElement)window.FindName("RecentMediaScroll")).TranslatePoint(new Point(),window).Y, "Device selection is above recent media");
                Snapshot("home-devices");
                var routes = new[] { "The Handy", "TCode Wi-Fi", "TCode Serial", "TCode Bluetooth", "Device Manager", "Buttplug" };
                foreach (string route in routes)
                {
                    Call("NavigateTo", "home");
                    await Task.Delay(30);
                    var button = Descendants<Button>(home).Single(b => b.Tag as string == route);
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(60);
                    Check(ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content,Sections[route]), "Home opens matching device subpage: " + route);
                    Check(!InlineDialogService.IsOpen && Application.Current.Windows.OfType<Window>().Count(w => w.IsVisible) == 1, "Device route creates no modal or extra window: " + route);
                }
                Call("OpenDeviceSettings", "Connections");
                window.UpdateLayout();
                foreach (var button in Descendants<Button>(Sections["Connections"]).Where(b => b.Tag is string).ToArray())
                {
                    string route = (string)button.Tag;
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Task.Delay(40);
                    Check(ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content,Sections[route]), "Connection overview routes consistently: " + route);
                    Call("OpenDeviceSettings", "Connections");
                    await Task.Delay(30);
                }
                window.ViewModel.Settings.HandyDeviceId = "";
                Call("OpenDeviceSettings", "The Handy");
                var handy = Descendants<Button>(Sections["The Handy"]).Single(b => b.Name == "HandySettingsConnect");
                handy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Descendants<TextBlock>(Sections["The Handy"]).Any(t => t.Text.Contains("请先填写有效")), "Missing Handy key has actionable feedback without a connection request");
                Check(Descendants<PasswordBox>(Sections["The Handy"]).Any(), "Handy reuses masked live credential editor");
                window.UpdateLayout();
                Check(Descendants<PasswordBox>(Sections["The Handy"]).Single().ActualWidth > 180, "Handy key input retains usable width beside its explanations");
                Snapshot("handy-subpage");

                Call("OpenDeviceSettings", "Connections");
                await Task.Delay(60);
                int calls = 0;
                var pending = new TaskCompletionSource<bool>();
                pendingConnection = pending;
                var wifi = new TCodeWifiDialog(endpoint => { calls++; return pending.Task; }, () => { });
                var form = (StackPanel)wifi.Content;
                var task = InlineDialogService.ShowAsync(wifi);
                await Task.Delay(60);
                Check(!wifi.IsVisible && !InlineDialogService.IsOpen && InlineDialogService.IsDevicePageOpen, "Wi-Fi uses a settings editor instead of modal state");
                Check(Descendants<StackPanel>(Sections["TCode Wi-Fi"]).Contains(form), "Wi-Fi form belongs to the selected settings subpage");
                var address = form.Children.OfType<TextBox>().Single();
                var status = form.Children.OfType<TextBlock>().Last();
                var actions = form.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
                address.Text = "host:0";
                actions[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(calls == 0 && status.Text.Length > 0 && actions[0].IsEnabled, "Invalid endpoint never reaches the connector");
                address.Text = "127.0.0.1:9999";
                actions[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(calls == 1 && !address.IsEnabled, "Pending callback disables connection controls");
                Call("OpenDeviceSettings", "Connections");
                await Task.Delay(30);
                Check(!task.IsCompleted && ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content,Sections["TCode Wi-Fi"]), "Busy Wi-Fi prevents leaving and racing another transport");
                var topDisconnect = Descendants<Button>(Sections["TCode Wi-Fi"]).First(b => b.Content as string == "断开 TCode");
                topDisconnect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!task.IsCompleted && status.Text.Contains("正在解析"), "Page disconnect cannot bypass pending connection guard");
                var nav = (Dictionary<string,RadioButton>)typeof(MainWindow).GetField("settingsNavigation",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);
                nav["TCode Bluetooth"].IsChecked = true;
                nav["TCode Bluetooth"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(nav["TCode Wi-Fi"].IsChecked == true && nav["TCode Bluetooth"].IsChecked == false, "Rejected busy navigation restores the current settings radio");
                var compact = (ComboBox)window.FindName("CompactSettingsNavigator");
                compact.SelectedValue = "TCode Bluetooth";
                await Task.Delay(40);
                Check(compact.SelectedValue as string == "TCode Wi-Fi", "Rejected busy navigation restores the compact selector");
                var homeNav = (RadioButton)window.FindName("HomeNav");
                homeNav.IsChecked = true;
                homeNav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(((RadioButton)window.FindName("SettingsNav")).IsChecked == true && homeNav.IsChecked == false, "Rejected home navigation preserves the settings highlight");
                window.Close();
                Check(window.IsVisible && !task.IsCompleted, "Exit waits for the in-flight device operation instead of disposing its controller");
                Call("OpenDeviceSettings", "TCode Wi-Fi");
                Check(Descendants<StackPanel>(Sections["TCode Wi-Fi"]).Contains(form), "Returning reuses the same pending Wi-Fi form");
                var duplicate = new TCodeWifiDialog(endpoint => throw new Exception("Duplicate must not connect"), () => { });
                Check(await InlineDialogService.ShowAsync(duplicate) == false && !task.IsCompleted, "Duplicate Wi-Fi request does not replace the current session");
                pending.SetException(new IOException("injected connection failure"));
                await Task.Delay(80);
                Check(status.Text.Contains("injected connection failure") && actions.All(b => b.IsEnabled), "Failed callback reports error and restores controls");
                InlineDialogService.Complete(wifi, false);
                Check(await task == false && !InlineDialogService.IsDevicePageOpen, "Closing Wi-Fi releases its page session");
                Call("OpenDeviceSettings", "Connections");
                await Task.Delay(50);

                // Fake COM selector exercises the existing return protocol without opening a port.
                Call("OpenDeviceSettings", "TCode Bluetooth");
                Call("LeaveDeviceEditors", new object[] { null });
                await Task.Delay(60);
                var serial = new SerialPortSelector(new[] { "TEST_COM" }) { Title = "TCode test selector" };
                var serialTask = InlineDialogService.ShowAsync(serial);
                await Task.Delay(60);
                Check(!serial.IsVisible && ReferenceEquals(((ContentControl)window.FindName("SettingsPageHost")).Content,Sections["TCode Bluetooth"]), "Bluetooth uses the existing COM selector in its own subpage");
                Check(Descendants<TextBlock>(Sections["TCode Bluetooth"]).Any(t => t.Text.Contains("BLE GATT")), "Bluetooth explanation distinguishes SPP from unsupported BLE");
                var confirm = Descendants<Button>(Sections["TCode Bluetooth"]).Single(b => b.IsDefault);
                confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(await serialTask == true && serial.SelectedPort == "TEST_COM", "COM form preserves selected-port completion without device I/O");

                foreach (string language in new[] { "zh-CN", "en" })
                foreach (string theme in new[] { "Dark", "Light" })
                {
                    window.ViewModel.Settings.InterfaceLanguage = language;
                    window.ViewModel.Settings.InterfaceTheme = theme;
                    window.Width = 760;
                    Call("OpenDeviceSettings", "TCode Bluetooth");
                    await Task.Delay(80);
                    window.UpdateLayout();
                    var scroll = (ScrollViewer)window.FindName("SettingsContentScroll");
                    Check(scroll.ScrollableWidth < 1 && ((ComboBox)window.FindName("CompactSettingsNavigator")).IsVisible, "Narrow device page fits: " + language + "/" + theme);
                    Check(language != "en" || ((TextBlock)window.FindName("SettingsPageHeading")).Text.Contains("Bluetooth"), "Device navigation follows language: " + language + "/" + theme);
                    Snapshot("bluetooth-" + language + "-" + theme);
                }
                File.AppendAllText(Path.Combine(output,"results.log"), "Failures: 0\nPasses: " + passed + "\n");
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(output,"results.log"), "ERROR: " + ex + "\nFailures: 1\n");
                Environment.ExitCode = 1;
            }
            finally { pendingConnection?.TrySetCanceled(); await Task.Delay(40); done = true; timeout.Stop(); window.Close(); app.Shutdown(Environment.ExitCode); }
        }));
        timeout.Start();
        app.Run(window);
        return Environment.ExitCode;
    }
}
