using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Animation;
using ScriptPlayer.Localization;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private void ApplyAppearanceSettings(string changed = null)
        {
            if (ViewModel?.Settings == null) return;
            bool paletteChanged = changed == null || changed == nameof(SettingsViewModel.InterfaceTheme) || changed == nameof(SettingsViewModel.InterfaceAccent);
            if (paletteChanged)
            {
                Themes.AppearanceTheme.Apply(ViewModel.Settings.InterfaceTheme, ViewModel.Settings.InterfaceAccent, ViewModel.Settings.EnableInterfaceAnimations);
                foreach (var section in settingsSections.Values) Themes.AppearanceTheme.ApplyTo(section);
                try { int dark = ViewModel.Settings.InterfaceTheme == "Light" ? 0 : 1; DwmSetWindowAttribute(new System.Windows.Interop.WindowInteropHelper(this).Handle,20,ref dark,sizeof(int)); }
                catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
            }
            bool languageChanged = ChineseLocalization.CurrentLanguage != ViewModel.Settings.InterfaceLanguage;
            ChineseLocalization.SetLanguage(ViewModel.Settings.InterfaceLanguage);
            if (languageChanged)
            {
                unifiedSettingsEditor?.RefreshLanguagePresentation();
                foreach (var option in outputSourceLabels) option.Refresh();
                RefreshOutputDurationLabel();
                foreach (var entry in ViewModel.Playlist.Entries.Concat(recentMedia).Distinct()) entry.RefreshInterfaceLanguage();
                RefreshTimelinePresentation();
            }
            if (changed == null || languageChanged)
            {
            ChineseLocalization.TranslateTree(this);
            foreach (var section in settingsSections.Values) ChineseLocalization.TranslateTree(section);
            if (settingsSections.Count > 0)
            {
                SettingsPageHeading.Text = TranslateSettingsTitle(settingsTitles[selectedSettingsPage]);
                RebuildSettingsSearchIndex();
                FilterSettingsNavigation();
            }
            PageTitle.Text = ChineseLocalization.Translate(currentPage == "settings" ? "设置" : currentPage == "library" ? "媒体列表" : "主页");
            }
            if (changed == null || SettingsViewModel.IsMotionPreference(changed)) ConfigureInterfaceMotion();
            RefreshMotionStatus();
        }
        private FrameworkElement CreateAppearanceSettings()
        {
            var panel = new StackPanel();
            panel.Children.Add(AppearanceRow("界面语言", "切换立即生效。", AppearanceSelector("InterfaceLanguage", new[] { "zh-CN|简体中文", "en|English" }, "InterfaceLanguageSelector")));
            panel.Children.Add(AppearanceRow("显示主题", "切换立即生效。", AppearanceSelector("InterfaceTheme", new[] { "Dark|深色", "Light|浅色" }, "InterfaceThemeSelector")));
            panel.Children.Add(AppearanceRow("强调色", "切换立即生效。", AppearanceSelector("InterfaceAccent", new[] { "Orange|橙色", "Blue|蓝色", "Teal|青绿色", "Purple|紫色", "Rose|玫红色" }, "InterfaceAccentSelector")));
            var animation = new CheckBox { Name = "InterfaceAnimationsSwitch", Content = "" };
            RegisterName(animation.Name, animation);
            System.Windows.Automation.AutomationProperties.SetName(animation, "界面切换动画");
            animation.SetResourceReference(StyleProperty, "CompactToggleSwitch");
            animation.SetBinding(CheckBox.IsCheckedProperty, new Binding("Settings.EnableInterfaceAnimations") { Source = ViewModel, Mode = BindingMode.TwoWay });
            panel.Children.Add(AppearanceRow("界面切换动画", "关闭后页面直接显示。系统关闭动画时同样停用。", animation));
            var options = new Button { Content = "动画与性能", HorizontalAlignment = HorizontalAlignment.Left };
            options.Click += (s,e) => SelectSettingsPage("Animations");
            panel.Children.Add(options);
            return panel;
        }
        private ComboBox AppearanceSelector(string property, string[] options, string name)
        {
            var selector = new ComboBox { Name = name, MinWidth = 140, MaxWidth = 200, SelectedValuePath = "Tag", Margin = new Thickness(0,4,0,4) };
            RegisterName(name, selector);
            foreach (string value in options)
            {
                var parts = value.Split('|');
                selector.Items.Add(new ComboBoxItem { Tag = parts[0], Content = parts[1] });
            }
            selector.SetBinding(ComboBox.SelectedValueProperty, new Binding("Settings." + property) { Source = ViewModel, Mode = BindingMode.TwoWay });
            return selector;
        }
        private static FrameworkElement AppearanceRow(string title, string description, FrameworkElement editor)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,16,0) };
            labels.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
            var hint = new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0,5,0,0) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "MediaMutedBrush"); labels.Children.Add(hint);
            grid.Children.Add(labels); Grid.SetColumn(editor,1); grid.Children.Add(editor);
            var row = new Border { Child = grid, MinHeight = 80, Padding = new Thickness(16), Margin = new Thickness(0,0,0,8) };
            row.SetResourceReference(StyleProperty, "SettingsCard"); return row;
        }
        private static string TranslateSettingsTitle(string title) => string.Join(" / ", title.Split(new[] { " / " }, StringSplitOptions.None).Select(ChineseLocalization.Translate));
        private void RebuildSettingsSearchIndex()
        {
            foreach (var section in settingsSections)
            {
                var text = new List<string> { section.Key, settingsTitles[section.Key], TranslateSettingsTitle(settingsTitles[section.Key]) };
                CollectSettingsText(section.Value, new HashSet<DependencyObject>(), text);
                settingsSearchIndex[section.Key] = string.Join(" ", text);
            }
        }
        private void AnimatePageTransition(FrameworkElement page)
        {
            if (page == null) return;
            Themes.MotionPresentation.FadeIn(page, Themes.InterfaceMotion.Page);
        }
        private void StopInterfaceAnimations()
        {
            Themes.MotionPresentation.StopAll();
        }
    }
}
