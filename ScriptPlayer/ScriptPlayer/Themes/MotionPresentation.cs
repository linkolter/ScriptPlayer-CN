using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Themes
{
    public enum InterfaceMotion { Interaction, Switch, Page, Transport, Menu, Panel, Cover }

    // Presentation clocks only. This module never receives media time or device references.
    public static class MotionPresentation
    {
        private sealed class Motion
        {
            public FrameworkElement Element;
            public double Opacity;
            public Transform Transform;
            public TranslateTransform Translation;
            public bool? Show;
            public bool HitTest;
            public Stopwatch Watch = Stopwatch.StartNew();
            public int Duration;
            public RoutedEventHandler Unloaded;
        }
        private static readonly Dictionary<FrameworkElement, Motion> running = new Dictionary<FrameworkElement, Motion>();
        private static SettingsViewModel settings;
        private static bool initialized, systemAnimations = true, suspended, lagReduced;
        private static int tier = 2, delayedAnimations;
        public static event Action PolicyChanged;
        public static int ActiveAnimations => running.Count;
        public static bool SystemAnimations => systemAnimations;
        public static bool AnimatePreviews => settings?.AutoPlayCoverAnimations == true && systemAnimations && !suspended && !IsSimplified;
        public static bool IsSimplified => settings?.SimplifyInterfaceAnimations == true && (tier < 2 || lagReduced);
        public static string EffectiveMode => settings == null || !settings.EnableInterfaceAnimations || !systemAnimations ? "Off"
            : IsSimplified ? "Light" : settings.InterfaceMotionMode;

        public static void Configure(SettingsViewModel value, bool systemEnabled, int renderingTier)
        {
            StopAll();
            settings = value;
            systemAnimations = systemEnabled;
            tier = renderingTier;
            if (!initialized)
            {
                initialized = true;
                EventManager.RegisterClassHandler(typeof(ButtonBase), Mouse.MouseEnterEvent, new MouseEventHandler(Interaction));
                EventManager.RegisterClassHandler(typeof(ButtonBase), UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler((s,e) => Interaction(s,e)), true);
                EventManager.RegisterClassHandler(typeof(CheckBox), FrameworkElement.LoadedEvent, new RoutedEventHandler((s,e) => SetValuePosition((CheckBox)s)), true);
                EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.CheckedEvent, new RoutedEventHandler(SwitchChanged), true);
                EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.UncheckedEvent, new RoutedEventHandler(SwitchChanged), true);
                EventManager.RegisterClassHandler(typeof(CheckBox), ToggleButton.IndeterminateEvent, new RoutedEventHandler(SwitchChanged), true);
            }
            PolicyChanged?.Invoke();
        }

        public static void Suspend(bool value)
        {
            suspended = value;
            if (value) StopAll();
            PolicyChanged?.Invoke();
        }

        public static void ResetPerformanceReduction()
        {
            lagReduced = false; delayedAnimations = 0;
            StopAll(); PolicyChanged?.Invoke();
        }

        public static bool Allows(InterfaceMotion effect)
        {
            if (settings == null || suspended || !systemAnimations || !settings.EnableInterfaceAnimations) return false;
            if (IsSimplified && (effect == InterfaceMotion.Page || effect == InterfaceMotion.Panel || effect == InterfaceMotion.Cover)) return false;
            switch (effect)
            {
                case InterfaceMotion.Interaction: return settings.AnimateInteraction;
                case InterfaceMotion.Switch: return settings.AnimateSwitches;
                case InterfaceMotion.Page: return settings.AnimatePages;
                case InterfaceMotion.Transport: return settings.AnimateTransport;
                case InterfaceMotion.Menu: return settings.AnimateMenus;
                case InterfaceMotion.Panel: return settings.AnimatePanels;
                case InterfaceMotion.Cover: return settings.AnimateCovers;
                default: return false;
            }
        }

        public static void StopAll()
        {
            foreach (var motion in running.Values.ToArray()) Finish(motion, true);
        }

        public static void FadeIn(FrameworkElement element, InterfaceMotion effect, bool slide = false)
        {
            if (element == null) return;
            double from = running.ContainsKey(element) ? element.Opacity : .86;
            Cancel(element);
            if (!Allows(effect) || !element.IsVisible) return;
            var motion = Begin(element, effect == InterfaceMotion.Page ? 120 : 90);
            if (slide && Allows(InterfaceMotion.Panel))
            {
                motion.Translation = new TranslateTransform(0, 6);
                var group = new TransformGroup(); group.Children.Add(motion.Transform); group.Children.Add(motion.Translation);
                element.SetCurrentValue(UIElement.RenderTransformProperty, group);
                motion.Translation.BeginAnimation(TranslateTransform.YProperty, Animation(6, 0, motion.Duration));
            }
            RunOpacity(motion, from, motion.Opacity);
        }

        public static void SetVisible(FrameworkElement element, bool show, InterfaceMotion effect)
        {
            if (running.TryGetValue(element, out var existing) && existing.Show == show) return;
            if (!running.ContainsKey(element) && (element.Visibility == Visibility.Visible) == show) return;
            // A zero-alpha first frame on the layered mpv overlay can pass the initial
            // mouse move to the native child. Keep the visible hit surface present.
            double from = running.ContainsKey(element) ? element.Opacity : (show ? .86 : element.Opacity);
            Cancel(element);
            element.IsHitTestVisible = show;
            // Hide immediately so dock reparenting/layout and hit testing retain their original contract.
            if (!show || !Allows(effect) || !element.IsLoaded)
            {
                element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                return;
            }
            element.Visibility = Visibility.Visible;
            var motion = Begin(element, show ? 100 : 70);
            motion.Show = show;
            motion.HitTest = show;
            RunOpacity(motion, from, show ? motion.Opacity : 0);
        }

        private static Motion Begin(FrameworkElement element, int duration)
        {
            var motion = new Motion { Element = element, Opacity = element.Opacity, Transform = element.RenderTransform,
                Duration = duration, HitTest = element.IsHitTestVisible };
            motion.Unloaded = (s,e) => Finish(motion, true);
            element.Unloaded += motion.Unloaded;
            running[element] = motion;
            return motion;
        }

        private static DoubleAnimation Animation(double from, double to, int duration) => new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(duration))
        { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };

        private static void RunOpacity(Motion motion, double from, double to)
        {
            var animation = Animation(from, to, motion.Duration);
            animation.Completed += (s,e) => Finish(motion, true, true);
            motion.Element.BeginAnimation(UIElement.OpacityProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private static void Cancel(FrameworkElement element)
        {
            if (running.TryGetValue(element, out var motion)) Finish(motion, false);
        }

        private static void Finish(Motion motion, bool settle, bool completed = false)
        {
            if (!running.TryGetValue(motion.Element, out var current) || current != motion) return;
            running.Remove(motion.Element);
            var element = motion.Element;
            element.Unloaded -= motion.Unloaded;
            element.BeginAnimation(UIElement.OpacityProperty, null);
            element.SetCurrentValue(UIElement.OpacityProperty, motion.Opacity);
            if (motion.Translation != null) element.SetCurrentValue(UIElement.RenderTransformProperty, motion.Transform);
            if (settle && motion.Show.HasValue)
            {
                element.IsHitTestVisible = motion.HitTest;
                element.Visibility = motion.Show.Value ? Visibility.Visible : Visibility.Collapsed;
            }
            if (completed && motion.Watch.ElapsedMilliseconds > motion.Duration + 160 && ++delayedAnimations >= 2 && !lagReduced)
            {
                lagReduced = true;
                if (settings?.SimplifyInterfaceAnimations == true) { StopAll(); PolicyChanged?.Invoke(); }
            }
        }

        private static void Interaction(object sender, MouseEventArgs e)
        {
            if (!Allows(InterfaceMotion.Interaction) || !(sender is Control control) || !control.IsEnabled) return;
            var surface = control.Template?.FindName("Surface", control) as FrameworkElement
                ?? control.Template?.FindName("NavBackground", control) as FrameworkElement;
            if (surface != null) FadeIn(surface, InterfaceMotion.Interaction);
        }

        private static readonly DependencyProperty SwitchPositionProperty = DependencyProperty.RegisterAttached("SwitchPosition", typeof(double), typeof(MotionPresentation), new PropertyMetadata(double.NaN));
        private static double Position(CheckBox check) => check.IsChecked == true ? 18 : check.IsChecked == false ? 0 : 9;
        private static void SetValuePosition(CheckBox check) => check.SetValue(SwitchPositionProperty, Position(check));
        private static void SwitchChanged(object sender, RoutedEventArgs e)
        {
            var check = (CheckBox)sender;
            double target = Position(check), previous = (double)check.GetValue(SwitchPositionProperty);
            SetValuePosition(check);
            var knob = check.Template?.FindName("SwitchKnob", check) as FrameworkElement;
            if (knob == null) return;
            if (running.TryGetValue(knob, out var prior) && prior.Translation != null)
                previous = (prior.Transform as TranslateTransform)?.X + prior.Translation.X ?? previous;
            Cancel(knob);
            knob.SetCurrentValue(UIElement.RenderTransformProperty, new TranslateTransform(target, 0));
            if (!check.IsLoaded || !Allows(InterfaceMotion.Switch) || double.IsNaN(previous)) return;
            var motion = Begin(knob, 100);
            motion.Translation = new TranslateTransform(previous - target, 0);
            var group = new TransformGroup(); group.Children.Add(motion.Transform); group.Children.Add(motion.Translation);
            knob.SetCurrentValue(UIElement.RenderTransformProperty, group);
            motion.Translation.BeginAnimation(TranslateTransform.XProperty, Animation(previous - target, 0, 100));
            RunOpacity(motion, 1, 1);
        }

        public static readonly DependencyProperty CoverHoverProperty = DependencyProperty.RegisterAttached("CoverHover", typeof(bool), typeof(MotionPresentation),
            new PropertyMetadata(false, (d,e) =>
            {
                if (!(d is FrameworkElement element)) return;
                if ((bool)e.NewValue) { element.MouseEnter += CoverEnter; element.MouseLeave += CoverLeave; }
                else { element.MouseEnter -= CoverEnter; element.MouseLeave -= CoverLeave; Cancel(element); }
            }));
        public static bool GetCoverHover(DependencyObject element) => (bool)element.GetValue(CoverHoverProperty);
        public static void SetCoverHover(DependencyObject element, bool value) => element.SetValue(CoverHoverProperty, value);
        private static void CoverEnter(object sender, MouseEventArgs e) => FadeIn((FrameworkElement)sender, InterfaceMotion.Cover);
        private static void CoverLeave(object sender, MouseEventArgs e) => Cancel((FrameworkElement)sender);
    }
}
