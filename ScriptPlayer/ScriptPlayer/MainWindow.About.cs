using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private void CaptionMinimize_Click(object sender, RoutedEventArgs e) { SystemCommands.MinimizeWindow(this); }
        private void CaptionMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized) SystemCommands.RestoreWindow(this);
            else SystemCommands.MaximizeWindow(this);
            CaptionMaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
        private void CaptionClose_Click(object sender, RoutedEventArgs e) { Close(); }

        private async void VersionButton_Click(object sender, RoutedEventArgs e)
        {
            if (InlineDialogService.IsOpen) return;
            var content = new StackPanel();
            content.Loaded += (s, args) =>
            {
                // This prose page wraps within the existing inline host, unlike legacy fixed-width forms.
                DependencyObject parent = content;
                while (parent != null && !(parent is ScrollViewer)) parent = VisualTreeHelper.GetParent(parent);
                if (parent is ScrollViewer scroll) scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                foreach (UIElement item in content.Children)
                    if (item is TextBlock text && text.Tag is double size) text.FontSize = size;
            };
            AddAboutText(content, "ScriptPlayer", 28, true);
            AddAboutText(content, "Version v1.0.12.3", 18, true);
            AddAboutText(content, "让播放回归简单，让体验更加自由。", 14, false, true);
            AddAboutText(content, "关于本项目", 20, true);
            AddAboutText(content, "最初，我只是想要一个更加出色的本地脚本播放器。\n\n原版 ScriptPlayer 已经具备出色的脚本同步和设备控制能力，但在日常使用中，我总觉得媒体管理、界面交互和一些细节还有改进的空间。\n\n于是，从修改一个按钮、调整一个界面开始，慢慢重构了播放器的布局与交互逻辑，并陆续加入了新的功能。改着改着，它就变成了现在的模样。\n\n本项目基于开源 ScriptPlayer 进行深度二次开发，保留其核心功能，并围绕日常使用体验进行了扩展，包括：");
            AddAboutText(content, "• 集成 libmpv 播放核心与 FFmpeg 媒体处理能力。\n• 重新设计的 WPF 界面，简洁直观的操作体验。\n• 媒体库管理、封面预览、NFO 元数据与标签筛选。\n• Funscript 脚本同步、多轴脚本与设备控制。\n• 播放历史、进度记录及更多细节优化。");
            AddAboutText(content, "开发者的话", 20, true);
            AddAboutText(content, "这不是一个商业项目，也不是为了取代原版 ScriptPlayer。\n\n它只是一个普通爱好者，出于兴趣、热爱，以及一点点对使用体验的执着，逐渐打磨出来的作品。\n\n我希望它不只是能够正常工作，更希望每一次打开、选择、播放和操作，都足够自然、顺手。\n\n软件仍在持续完善中，难免存在不足。如果你喜欢它，欢迎使用；如果发现问题，也欢迎提出建议。");
            AddAboutText(content, "感谢原版 ScriptPlayer 的开发者，以及为开源社区作出贡献的每一个人。", 14, true);
            AddAboutText(content, "Enjoy your freedom.", 14, false, true);
            content.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 16) });
            var source = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            source.Inlines.Add(new Bold(new Run("项目来源： ")));
            var link = new Hyperlink(new Run("FredTungsten / ScriptPlayer"));
            link.SetResourceReference(TextElement.ForegroundProperty, "MediaAccentBrush");
            link.Click += (s, args) =>
            {
                try { Process.Start(new ProcessStartInfo("https://github.com/FredTungsten/ScriptPlayer") { UseShellExecute = true }); }
                catch (System.Exception ex) { MessageBox.Show(this, "无法打开项目来源链接：" + ex.Message, "ScriptPlayer"); }
            };
            source.Inlines.Add(link); content.Children.Add(source);
            AddAboutText(content, "版本： v1.0.12.3\n发布日期： 2026.10.11\n性质： 非官方社区衍生版本，持续维护中。");
            await InlineDialogService.ShowAsync(new Window { Title = "关于 ScriptPlayer", Width = 720, Content = content });
        }

        private static void AddAboutText(StackPanel panel, string text, double size = 14, bool bold = false, bool italic = false)
        {
            panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap,
                Tag = size, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                FontStyle = italic ? FontStyles.Italic : FontStyles.Normal,
                Margin = new Thickness(0, size >= 20 ? 12 : 0, 0, 14), LineHeight = size * 1.65 });
        }
    }
}