using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScriptPlayer.Dialogs;

class TCodeWifiDialogTests
{
    static int count;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        Console.WriteLine("PASS " + label); count++;
    }
    static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
    [STAThread]
    static int Main(string[] args)
    {
        string settings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OSR6-WiFi.txt");
        byte[] original = File.Exists(settings) ? File.ReadAllBytes(settings) : null;
        try
        {
            File.WriteAllText(settings, "127.0.0.1:8000");
            string connected = null;
            bool disconnected = false;
            var pending = new TaskCompletionSource<bool>();
            var dialog = new TCodeWifiDialog(endpoint => { connected = endpoint; return pending.Task; },
                () => disconnected = true);
            var panel = (StackPanel)dialog.Content;
            var address = panel.Children.OfType<TextBox>().Single();
            var status = panel.Children.OfType<TextBlock>().Last();
            var buttons = panel.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
            Check(address.Text == "127.0.0.1:8000", "previous endpoint loaded");
            address.Text = "host:0";
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(connected == null && status.Foreground == Brushes.Firebrick, "invalid address rejected before connecting");
            address.Text = "127.0.0.1:9123";
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(connected == "udp://127.0.0.1:9123/", "typed connect callback receives normalized UDP endpoint");
            Check(!address.IsEnabled && buttons.All(b => !b.IsEnabled), "controls disabled during connection");
            pending.SetResult(true); Pump();
            Check(status.Text.Contains("UDP 发送端已就绪") && buttons.All(b => b.IsEnabled), "successful connection restores controls and reports readiness");
            Check(File.ReadAllText(settings).Trim() == "127.0.0.1:9123", "successful endpoint saved");
            buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(disconnected && status.Text.Contains("已断开"), "typed disconnect callback and status");
            address.Text = "tcode.local:8000";
            status.Text = "UDP 就绪仅表示发送端已打开，设备是否收到需在播放时确认。";
            dialog.Content = null;
            var preview = new Border { Width = 510, Background = Brushes.White, Child = panel };
            preview.Measure(new Size(510, double.PositiveInfinity));
            preview.Arrange(new Rect(new Point(), preview.DesiredSize));
            preview.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(preview);
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(preview.ActualWidth * dpi.DpiScaleX),
                (int)Math.Ceiling(preview.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bitmap.Render(preview);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create(args[0])) encoder.Save(file);
            dialog.Close();

            var failedDialog = new TCodeWifiDialog(endpoint => Task.FromException(new IOException("模拟连接失败")), () => { });
            panel = (StackPanel)failedDialog.Content;
            status = panel.Children.OfType<TextBlock>().Last();
            address = panel.Children.OfType<TextBox>().Single();
            address.Text = "127.0.0.1:9999";
            buttons = panel.Children.OfType<StackPanel>().Single().Children.OfType<Button>().ToArray();
            buttons[0].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(status.Text.Contains("模拟连接失败") && buttons.All(b => b.IsEnabled), "connection failure reports error and restores controls");
            Check(File.ReadAllText(settings).Trim() == "127.0.0.1:9123", "failed connection does not replace saved endpoint");
            failedDialog.Close();
            Console.WriteLine("TOTAL " + count + " dialog checks passed");
            return 0;
        }
        catch (Exception ex) { Console.WriteLine(ex); return 1; }
        finally
        {
            if (original != null) File.WriteAllBytes(settings, original);
            else if (File.Exists(settings)) File.Delete(settings);
        }
    }
}
