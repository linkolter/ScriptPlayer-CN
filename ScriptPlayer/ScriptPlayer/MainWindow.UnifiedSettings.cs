using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using ScriptPlayer.Dialogs;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private SettingsDialog unifiedSettingsEditor;
        private readonly Dictionary<string, FrameworkElement> settingsSections = new Dictionary<string, FrameworkElement>();

        private readonly Dictionary<string, string> settingsTitles = new Dictionary<string, string>();
        private readonly Dictionary<string, RadioButton> settingsNavigation = new Dictionary<string, RadioButton>();
        private readonly Dictionary<string, Expander> settingsGroups = new Dictionary<string, Expander>();
        private readonly Dictionary<string, string> settingsSearchIndex = new Dictionary<string, string>();
        private string selectedSettingsPage = "Overview";
        private bool updatingSettingsNavigation;

        private void EnsureUnifiedSettings()
        {
            if (unifiedSettingsEditor != null) return;
            unifiedSettingsEditor = new SettingsDialog(ViewModel.Settings);
            var pages = unifiedSettingsEditor.CreateUnifiedPages(this, ViewModel.Settings);
            AddPageActions(pages, "Buttplug", new[] { "启动 Intiface|StartButtplugCommand", "连接|ConnectButtplugCommand", "扫描设备|StartScanningButtplugCommand", "断开|DisconnectButtplugCommand" });
            AddPageActions(pages, "E-Stim Audio", new[] { "添加音频输出设备|AddEstimAudioCommand" });
            AddPageActions(pages, "Funstim Converter", new[] { "添加 Funstim 音频设备|AddFunstimAudioCommand" });
            var scripts = new StackPanel();
            scripts.Children.Add(pages["Scripts"]);
            scripts.Children.Add(CreateOutputEditor());
            pages["Scripts"] = scripts;
            unifiedSettingsEditor.SettingsApplied += (s,e) => { settingsSaveTimer.Stop(); settingsSaveTimer.Start(); };
            settingsSections["Overview"] = SettingsOverview;
            settingsTitles["Overview"] = "常用设置";
            var overviewButton = MakeSettingsButton("Overview", "常用设置");
            UnifiedSettingsSections.Children.Add(overviewButton);
            SettingsExtraPages.Children.Remove(SettingsConnectionPage);
            SettingsExtraPages.Children.Remove(SettingsToolsPage);
            pages["Connections"] = SettingsConnectionPage;
            pages["Tools"] = SettingsToolsPage;
            pages["Appearance"] = CreateAppearanceSettings();
            AddSettingsGroup("播放", pages, new[] {
                "General|播放与窗口", "Playlist|播放队列与循环", "Players|外部播放器连接", "Subtitles|字幕", "Crop Video|画面裁剪", "Displayed Time|时间显示", "Chapter Settings|章节与跳过" });
            AddSettingsGroup("脚本与同步", pages, new[] {
                "Scripts|脚本加载与匹配", "Timings|音画与脚本延迟", "Range|行程范围与位置过滤", "Speed|速度与限制", "SoftSeek|平滑跳转", "Gaps|空白片段处理" });
            AddSettingsGroup("设备", pages, new[] {
                "Connections|连接与管理", "The Handy|Handy 连接配置", "Buttplug|Buttplug 连接与启动", "Devices|设备输出行为", "E-Stim Audio|电刺激音频设备", "Funstim Converter|Funstim 转换" });
            AddSettingsGroup("媒体库与文件", pages, new[] {
                "Tools|媒体与脚本工具", "Paths|媒体与脚本搜索路径", "Favourite Folders|常用文件夹", "Format Preference|格式优先级", "FFmpeg|内置 FFmpeg 与预览生成" });
            AddSettingsGroup("操作与外观", pages, new[] {
                "Appearance|界面与外观", "Input|鼠标操作", "Input Mappings|快捷键", "Notifications|播放通知", "Remember|恢复与记忆", "Debug|脚本可视化与调试" });
            foreach (var section in settingsSections)
            {
                Localization.ChineseLocalization.TranslateTree(section.Value);
                var text = new List<string> { section.Key, settingsTitles[section.Key], TranslateSettingsTitle(settingsTitles[section.Key]) };
                CollectSettingsText(section.Value, new HashSet<DependencyObject>(), text);
                settingsSearchIndex[section.Key] = string.Join(" ", text);
            }
            FilterSettingsNavigation();
            SelectSettingsPage("Overview");
        }

        private void AddPageActions(Dictionary<string, FrameworkElement> pages, string id, string[] actions)
        {
            var panel = new StackPanel();
            var buttons = new WrapPanel { Margin = new Thickness(0,0,0,16) };
            foreach (string action in actions)
            {
                var parts = action.Split('|');
                var button = new Button { Content = parts[0], Margin = new Thickness(0,0,8,6) };
                button.SetBinding(Button.CommandProperty, new Binding(parts[1]) { Source = ViewModel });
                buttons.Children.Add(button);
            }
            panel.Children.Add(buttons);
            panel.Children.Add(pages[id]);
            pages[id] = panel;
        }

        private readonly List<OutputSourceLabel> outputSourceLabels = new List<OutputSourceLabel>();
        private TextBlock outputDurationLabel;
        private sealed class OutputSourceLabel : System.ComponentModel.INotifyPropertyChanged
        {
            public CommandSource Key { get; }
            private readonly string label;
            public string Value => Localization.ChineseLocalization.Translate(label);
            public OutputSourceLabel(CommandSource key, string text) { Key=key; label=text; }
            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            public void Refresh() => PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));
        }
        private void RefreshOutputDurationLabel()
        {
            if (outputDurationLabel == null) return;
            outputDurationLabel.SetBinding(TextBlock.TextProperty, new Binding("Settings.PatternSpeed") { Converter = (IValueConverter)FindResource("MillisecondsConverter"), StringFormat = Localization.ChineseLocalization.Translate("动作周期：{0:0} ms") });
        }
        private FrameworkElement CreateOutputEditor()
        {
            var panel = new StackPanel { DataContext = ViewModel };
            var heading = new TextBlock { Text = "设备输出源", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,16) };
            heading.SetResourceReference(TextBlock.FontSizeProperty, "MediaSectionFontSize");
            panel.Children.Add(heading);
            outputSourceLabels.Clear();
            outputSourceLabels.Add(new OutputSourceLabel(CommandSource.Video, "跟随媒体脚本"));
            outputSourceLabels.Add(new OutputSourceLabel(CommandSource.None, "停止输出"));
            outputSourceLabels.Add(new OutputSourceLabel(CommandSource.Random, "随机动作"));
            outputSourceLabels.Add(new OutputSourceLabel(CommandSource.Pattern, "循环动作模式"));
            var source = new ComboBox { SelectedValuePath = "Key", DisplayMemberPath = "Value", ItemsSource = outputSourceLabels };
            source.SetBinding(ComboBox.SelectedValueProperty, new Binding("CommandSource") { Mode = BindingMode.TwoWay });
            panel.Children.Add(source);
            panel.Children.Add(new TextBlock { Text = "循环动作模式", Margin = new Thickness(0,16,0,8) });
            var pattern = new ComboBox { DisplayMemberPath = "Name" };
            pattern.SetBinding(ComboBox.ItemsSourceProperty, new Binding("Patterns"));
            pattern.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedPattern") { Mode = BindingMode.TwoWay });
            panel.Children.Add(pattern);
            var duration = outputDurationLabel = new TextBlock { Margin = new Thickness(0,12,0,4) };
            duration.SetBinding(TextBlock.TextProperty, new Binding("Settings.PatternSpeed") { Converter = (IValueConverter)FindResource("MillisecondsConverter"), StringFormat = Localization.ChineseLocalization.Translate("动作周期：{0:0} ms") });
            panel.Children.Add(duration);
            var speed = new Slider { Minimum = 100, Maximum = 1000, TickFrequency = 10, IsSnapToTickEnabled = true };
            speed.SetBinding(Slider.ValueProperty, new Binding("Settings.PatternSpeed") { Mode = BindingMode.TwoWay, Converter = (IValueConverter)FindResource("MillisecondsConverter") });
            panel.Children.Add(speed);
            var test = new ComboBox { DisplayMemberPath = "Name", Margin = new Thickness(0,16,0,8) };
            test.SetBinding(ComboBox.ItemsSourceProperty, new Binding("TestPatterns"));
            test.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedTestPattern") { Mode = BindingMode.TwoWay });
            panel.Children.Add(test);
            var run = new Button { Content = "执行测试动作", HorizontalAlignment = HorizontalAlignment.Left };
            run.SetBinding(Button.CommandProperty, new Binding("ExecuteSelectedTestPatternCommand"));
            panel.Children.Add(run);
            return new Border { Style = (Style)FindResource("SettingsCard"), Child = panel, Margin = new Thickness(0,16,0,0) };
        }

        private RadioButton MakeSettingsButton(string id, string title)
        {
            var button = new RadioButton { Content = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap },
                GroupName = "SettingsPages", Style = (Style)FindResource("NavigationItem"),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 2, 0, 2), MinHeight = 40, Height = double.NaN };
            System.Windows.Automation.AutomationProperties.SetName(button, title);
            button.Click += (s,e) => SelectSettingsPage(id);
            settingsNavigation[id] = button;
            return button;
        }

        private void AddSettingsGroup(string title, Dictionary<string, FrameworkElement> pages, string[] items)
        {
            var children = new StackPanel { Margin = new Thickness(4,0,4,8) };
            var group = new Expander { Header = title, HeaderTemplate = (DataTemplate)FindResource("SettingsGroupHeaderTemplate"), Content = children, Margin = new Thickness(0,6,0,0) };
            UnifiedSettingsSections.Children.Add(group);
            foreach (string item in items)
            {
                var parts = item.Split('|');
                var editor = pages[parts[0]];
                // The viewport owns the available width; a legacy 400px minimum can
                // force horizontal clipping once cards and labels use readable spacing.
                editor.MinWidth = 0;
                if (parts[0] == "Input Mappings" || parts[0] == "Paths" || parts[0] == "Favourite Folders")
                    editor.SetBinding(FrameworkElement.HeightProperty, new Binding("ViewportHeight") { Source = SettingsContentScroll });
                settingsSections[parts[0]] = editor;
                settingsTitles[parts[0]] = title + " / " + parts[1];
                settingsGroups[parts[0]] = group;
                children.Children.Add(MakeSettingsButton(parts[0], parts[1]));
            }
        }

        private void SelectSettingsPage(string id)
        {
            if (!settingsSections.TryGetValue(id, out var page)) return;
            selectedSettingsPage = id;
            unifiedSettingsEditor?.CommitUnifiedCollections();
            SettingsPageHost.Content = page;
            SettingsPageHeading.Text = TranslateSettingsTitle(settingsTitles[id]);
            SettingsPageDescription.Text = Localization.ChineseLocalization.Translate(id == "Overview" ? "常用选项集中于此；更多设置可通过分类或搜索查找。" : "修改自动保存。长页面可滚动查看，列表支持独立滚动。");
            foreach (var pair in settingsNavigation) pair.Value.IsChecked = pair.Key == id;
            settingsGroups.TryGetValue(id, out var group);
            if (string.IsNullOrWhiteSpace(SettingsSearch.Text))
                foreach (var section in settingsGroups.Values) section.IsExpanded = section == group;
            updatingSettingsNavigation = true;
            try { CompactSettingsNavigator.SelectedValue = id; }
            finally { updatingSettingsNavigation = false; }
            settingsNavigation[id].BringIntoView();
            SettingsContentScroll.ScrollToTop();
            SettingsContentScroll.ScrollToLeftEnd();
            AnimatePageTransition(SettingsPageHost);
        }

        private static void CollectSettingsText(DependencyObject root, HashSet<DependencyObject> visited, List<string> text)
        {
            if (root == null || !visited.Add(root)) return;
            if (root is TextBlock label) text.Add(label.Text);
            if (root is ContentControl control && control.Content is string content) text.Add(content);
            if (root is HeaderedContentControl header && header.Header is string title) text.Add(title);
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                CollectSettingsText(child, visited, text);
        }

        private void SettingsSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (settingsSearchIndex.Count > 0) FilterSettingsNavigation();
        }

        private void FilterSettingsNavigation()
        {
            var words = (SettingsSearch.Text ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var matches = settingsSearchIndex.Where(x => words.All(word => x.Value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(x => x.Key).ToList();
            foreach (var item in settingsNavigation)
                item.Value.Visibility = matches.Contains(item.Key) ? Visibility.Visible : Visibility.Collapsed;
            foreach (var group in settingsGroups.Values.Distinct())
            {
                bool visible = matches.Any(id => settingsGroups.TryGetValue(id, out var owner) && owner == group);
                group.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (words.Length > 0) group.IsExpanded = visible;
                else group.IsExpanded = settingsGroups.TryGetValue(selectedSettingsPage, out var owner) && owner == group;
            }
            SettingsSearchStatus.Text = words.Length == 0 ? "搜索设置名称或选项；按 Enter 打开第一个结果。"
                : matches.Count == 0 ? "没有匹配的设置，请换个关键词或清除搜索。"
                : Localization.ChineseLocalization.TranslateFormat("找到 {0} 个设置页面；按 Enter 打开第一个结果。", matches.Count);
            SettingsSearchStatus.Text = Localization.ChineseLocalization.Translate(SettingsSearchStatus.Text);
            updatingSettingsNavigation = true;
            try
            {
                CompactSettingsNavigator.ItemsSource = matches.Select(id => new KeyValuePair<string, string>(id, TranslateSettingsTitle(settingsTitles[id]))).ToArray();
                CompactSettingsNavigator.SelectedValue = selectedSettingsPage;
            }
            finally { updatingSettingsNavigation = false; }
        }

        private void SettingsSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { SettingsSearch.Clear(); e.Handled = true; }
            if (e.Key == Key.Enter)
            {
                var first = settingsNavigation.FirstOrDefault(x => x.Value.Visibility == Visibility.Visible);
                if (first.Value != null) SelectSettingsPage(first.Key);
                e.Handled = true;
            }
        }

        private void ClearSettingsSearch_Click(object sender, RoutedEventArgs e) { SettingsSearch.Clear(); SettingsSearch.Focus(); }

        private void CompactSettingsNavigator_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // SelectedValue can still contain the previous key while SelectionChanged is firing.
            if (!updatingSettingsNavigation && e.AddedItems.Count > 0 && e.AddedItems[0] is KeyValuePair<string, string> item)
                SelectSettingsPage(item.Key);
        }

        private void UpdateSettingsLayout()
        {
            if (GridSettings == null || SettingsNavigationScroll == null || SettingsEditorLayout == null) return;
            double available = BrowsePage.ActualWidth;
            bool compact = available > 0 && available < 740;
            SettingsNavigationScroll.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            CompactSettingsNavigator.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            GridSettings.ColumnDefinitions[0].Width = new GridLength(compact ? 0 : 168);
            GridSettings.ColumnDefinitions[1].Width = new GridLength(compact ? 0 : 20);
        }

        private void OpenAdvancedSettings(string id)
        {
            NavigateTo("settings");
            EnsureUnifiedSettings();
            if (id != null) SelectSettingsPage(id);
        }

        private void AdvancedSettings_Click(object sender, RoutedEventArgs e) => OpenAdvancedSettings((sender as FrameworkElement)?.Tag as string);

        private bool selectingEngine;
        private void EngineSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!shellReady || selectingEngine || e.AddedItems.Count == 0 || !((e.AddedItems[0] as ComboBoxItem)?.Tag is PlaybackMode mode) || mode == ViewModel.PlaybackMode) return;
            var settings=ViewModel.Settings;
            bool missing = mode==PlaybackMode.Vlc && (string.IsNullOrWhiteSpace(settings.VlcEndpoint)||string.IsNullOrWhiteSpace(settings.VlcPassword))
                || mode==PlaybackMode.Whirligig && string.IsNullOrWhiteSpace(settings.WhirligigEndpoint)
                || mode==PlaybackMode.ZoomPlayer && string.IsNullOrWhiteSpace(settings.ZoomPlayerEndpoint)
                || mode==PlaybackMode.DeoVr && string.IsNullOrWhiteSpace(settings.DeoVrEndpoint)
                || mode==PlaybackMode.MpcHc && string.IsNullOrWhiteSpace(settings.MpcHcEndpoint)
                || mode==PlaybackMode.Kodi && settings.KodiHttpPort==0
                || mode==PlaybackMode.SamsungVr && settings.SamsungVrUdpPort==0
                || mode==PlaybackMode.GoProVrPlayer && settings.GoProVrPlayerUdpPort==0;
            selectingEngine=true;
            try
            {
                if (missing)
                {
                    EngineSelector.SetCurrentValue(System.Windows.Controls.Primitives.Selector.SelectedValueProperty, ViewModel.PlaybackMode);
                    OpenAdvancedSettings("Players");
                    SettingsPageDescription.Text="请先填写对应播放器的连接信息，再返回常用设置选择播放引擎。";
                }
                else ViewModel.PlaybackMode=mode;
            }
            finally { selectingEngine=false; }
        }

        private void AutoHideControls_Click(object sender, RoutedEventArgs e)
        {
            autoHidePlaybackControls = ((CheckBox)sender).IsChecked == true;
            SaveShellState();
            ResetPlaybackChrome();
            UpdateShellFullscreen();
        }
    }
}
