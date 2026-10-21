using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ScriptPlayer.Localization;
using ScriptPlayer.Themes;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer
{
    public partial class MainWindow
    {
        private bool motionInitialized;
        private TextBlock motionStatus;

        private void ConfigureInterfaceMotion()
        {
            if (!motionInitialized)
            {
                motionInitialized = true;
                SystemParameters.StaticPropertyChanged += MotionSystemChanged;
                StateChanged += MotionWindowStateChanged;
                RenderCapability.TierChanged += MotionRenderingTierChanged;
                MotionPresentation.PolicyChanged += RefreshMotionStatus;
            }
            MotionPresentation.Configure(ViewModel.Settings, SystemParameters.ClientAreaAnimation, RenderCapability.Tier >> 16);
            MotionPresentation.Suspend(WindowState == WindowState.Minimized);
        }

        private void MotionSystemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation)) return;
            Dispatcher.BeginInvoke(new Action(() => { if (motionInitialized) ConfigureInterfaceMotion(); }));
        }
        private void MotionWindowStateChanged(object sender, EventArgs e) => MotionPresentation.Suspend(WindowState == WindowState.Minimized);
        private void MotionRenderingTierChanged(object sender, EventArgs e) => ConfigureInterfaceMotion();
        private void DisposeInterfaceMotion()
        {
            if (!motionInitialized) return;
            motionInitialized = false;
            SystemParameters.StaticPropertyChanged -= MotionSystemChanged;
            StateChanged -= MotionWindowStateChanged;
            RenderCapability.TierChanged -= MotionRenderingTierChanged;
            MotionPresentation.PolicyChanged -= RefreshMotionStatus;
            MotionPresentation.Configure(null, false, 0);
        }

        private void RefreshMotionStatus()
        {
            AppearanceTheme.SetPopupAnimations(MotionPresentation.Allows(InterfaceMotion.Menu));
            if (motionStatus == null) return;
            string mode = MotionPresentation.EffectiveMode;
            string label = mode == "Off" ? "关闭" : mode == "Light" ? "轻量" : mode == "Standard" ? "标准" : "自定义";
            motionStatus.Text = ChineseLocalization.Translate("当前效果") + ": " + ChineseLocalization.Translate(label)
                + (MotionPresentation.IsSimplified ? " · " + ChineseLocalization.Translate("已自动简化") : "")
                + (!MotionPresentation.SystemAnimations ? " · " + ChineseLocalization.Translate("系统动画已关闭") : "");
        }

        private FrameworkElement CreateAnimationSettings()
        {
            var panel = new StackPanel();
            panel.Children.Add(AppearanceRow("动画模式", "轻量适合日常播放；标准增加页面与面板过渡。", AppearanceSelector("InterfaceMotionMode",
                new[] { "Off|关闭", "Light|轻量", "Standard|标准", "Custom|自定义" }, "InterfaceMotionModeSelector")));
            motionStatus = new TextBlock { Name = "InterfaceMotionStatus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,4,0,16) };
            RegisterName(motionStatus.Name, motionStatus);
            motionStatus.SetResourceReference(TextBlock.ForegroundProperty, "MediaMutedBrush");
            panel.Children.Add(motionStatus);
            panel.Children.Add(MotionSwitch("SimplifyInterfaceAnimations", "自动简化动画", "低图形能力或检测到持续卡顿时，只保留轻量效果。"));
            panel.Children.Add(new TextBlock { Text = "始终遵循 Windows 动画设置。最小化时停止界面动画；视频、时间轴与设备输出不参与动画。", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16,8,16,16) });
            var advanced = new StackPanel();
            foreach (var row in new[] {
                new[] { "AnimateInteraction", "按钮与导航反馈", "短暂的明暗反馈，点击立即响应。" },
                new[] { "AnimateSwitches", "滑动开关过渡", "只移动开关圆点，不改变设置生效时机。" },
                new[] { "AnimatePages", "页面切换", "主页、媒体列表与设置页淡入。" },
                new[] { "AnimateTransport", "底部控件出现", "控件淡入；隐藏即时完成，保留时间轴交互。" },
                new[] { "AnimateMenus", "菜单与浮层", "快捷设置与菜单淡入。" },
                new[] { "AnimatePanels", "侧面调整面板", "仅调整面板内容使用轻微位移。" },
                new[] { "AnimateCovers", "封面悬停反馈", "封面短暂淡入，不改变尺寸或排列。" } })
                advanced.Children.Add(MotionSwitch(row[0], row[1], row[2]));
            panel.Children.Add(new Expander { Header = "分项动画（修改后进入自定义）", Content = advanced, Margin = new Thickness(0,8,0,16) });
            panel.Children.Add(MotionSwitch("AutoPlayCoverAnimations", "自动播放动态预览", "控制媒体详情中的 GIF 预览；关闭后显示静态画面。"));
            var sample = new Border { Height = 52, CornerRadius = new CornerRadius(6), Margin = new Thickness(0,8,0,8), Child = new TextBlock { Text = "动画预览", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            sample.SetResourceReference(Border.BackgroundProperty, "MediaSelectionBrush");
            panel.Children.Add(sample);
            var actions = new WrapPanel();
            var preview = new Button { Name = "PreviewInterfaceMotion", Content = "预览当前效果", Margin = new Thickness(0,0,8,8) };
            RegisterName(preview.Name, preview);
            preview.Click += (s,e) => {
                var effect = MotionPresentation.Allows(InterfaceMotion.Panel) ? InterfaceMotion.Panel
                    : MotionPresentation.Allows(InterfaceMotion.Page) ? InterfaceMotion.Page : InterfaceMotion.Interaction;
                MotionPresentation.FadeIn(sample, effect, effect == InterfaceMotion.Panel);
            };
            var reset = new Button { Name = "ResetInterfaceMotion", Content = "恢复轻量默认", Margin = new Thickness(0,0,8,8) };
            RegisterName(reset.Name, reset);
            reset.Click += (s,e) => { ViewModel.Settings.InterfaceMotionMode = "Standard"; ViewModel.Settings.InterfaceMotionMode = "Light"; ViewModel.Settings.SimplifyInterfaceAnimations = true; ViewModel.Settings.AutoPlayCoverAnimations = false; MotionPresentation.ResetPerformanceReduction(); };
            actions.Children.Add(preview); actions.Children.Add(reset); panel.Children.Add(actions);
            RefreshMotionStatus();
            return panel;
        }
        private FrameworkElement MotionSwitch(string property, string title, string hint)
        {
            var check = new CheckBox { Name = property + "Switch", Content = "" };
            RegisterName(check.Name, check);
            System.Windows.Automation.AutomationProperties.SetName(check, title);
            check.SetResourceReference(StyleProperty, "CompactToggleSwitch");
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding("Settings." + property) { Source = ViewModel, Mode = BindingMode.TwoWay });
            return AppearanceRow(title, hint, check);
        }
    }
}
