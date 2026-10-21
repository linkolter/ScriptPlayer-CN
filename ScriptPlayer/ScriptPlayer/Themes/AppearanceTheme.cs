using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace ScriptPlayer.Themes
{
    // A presentation-only palette. Shared styles resolve brush keys dynamically.
    public static class AppearanceTheme
    {
        public static string ThemeName { get; private set; } = "Dark";
        public static string AccentName { get; private set; } = "Orange";
        private static bool initialized;
        private static Dictionary<object,Color> currentPalette;
        private static PopupAnimation popupAnimation = PopupAnimation.Fade;
        public static void Apply(string theme, string accent, bool animations = true)
        {
            ThemeName = theme == "Light" ? "Light" : "Dark";
            AccentName = accent == "Blue" || accent == "Teal" || accent == "Purple" || accent == "Rose" ? accent : "Orange";
            popupAnimation = animations && SystemParameters.ClientAreaAnimation ? PopupAnimation.Fade : PopupAnimation.None;
            currentPalette = Palette();
            if (!initialized)
            {
                initialized = true;
                EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
                    new RoutedEventHandler((sender, args) => { var element = (FrameworkElement)sender; if (element.Resources.Count > 0 || element.Resources.MergedDictionaries.Count > 0) ApplyResources(element.Resources, currentPalette); }), true);
            }
            if (Application.Current == null) return;
            var palette = currentPalette;
            ApplyResources(Application.Current.Resources, palette);
            foreach (Window window in Application.Current.Windows) ApplyTo(window);
        }
        public static void ApplyTo(DependencyObject root)
        {
            Visit(root, new HashSet<DependencyObject>());
        }
        public static void SetPopupAnimations(bool enabled)
        {
            var value = enabled ? PopupAnimation.Fade : PopupAnimation.None;
            if (popupAnimation == value) return;
            popupAnimation = value;
            if (Application.Current == null) return;
            UpdatePopupResources(Application.Current.Resources);
            foreach (Window window in Application.Current.Windows) UpdatePopupTree(window, new HashSet<DependencyObject>());
        }
        private static void UpdatePopupResources(ResourceDictionary dictionary)
        {
            foreach (var child in dictionary.MergedDictionaries) UpdatePopupResources(child);
            if (dictionary.Keys.Cast<object>().Contains("MediaPopupAnimation")) dictionary["MediaPopupAnimation"] = popupAnimation;
        }
        private static void UpdatePopupTree(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (root == null || !visited.Add(root)) return;
            if (root is FrameworkElement element) UpdatePopupResources(element.Resources);
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) UpdatePopupTree(child, visited);
        }
        private static void Visit(DependencyObject root, HashSet<DependencyObject> visited)
        {
            if (root == null || !visited.Add(root) || currentPalette == null) return;
            if (root is FrameworkElement element) ApplyResources(element.Resources, currentPalette);
            var children = new List<DependencyObject>();
            try { foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) children.Add(child); } catch (InvalidOperationException) { }
            try { for (int i=0; i<VisualTreeHelper.GetChildrenCount(root); i++) children.Add(VisualTreeHelper.GetChild(root,i)); } catch (InvalidOperationException) { }
            foreach (var child in children) Visit(child, visited);
        }
        private static Dictionary<object, Color> Palette()
        {
            bool light = ThemeName == "Light";
            var values = new Dictionary<object, Color>();
            Action<object,string,string> add = (key,dark,pale) => values[key] = (Color)ColorConverter.ConvertFromString(light ? pale : dark);
            add("MediaBackgroundBrush", "#202020", "#F4F4F4"); add("MediaNavigationBrush", "#191919", "#EAEAEA");
            add("MediaSurfaceBrush", "#2D2D2D", "#FFFFFF"); add("MediaRaisedBrush", "#323232", "#F0F0F0");
            add("MediaBorderBrush", "#3C3C3C", "#D2D2D2"); add("MediaTextBrush", "#F3F3F3", "#202020");
            add("MediaMutedBrush", "#B5B5B5", "#606060"); add("MediaHoverBrush", "#3B3B3B", "#E5E5E5");
            add("MediaPressedBrush", "#464646", "#D8D8D8"); add("MediaOverlayBrush", "#EE282828", "#F5FFFFFF");
            add("MediaContentOverlayBrush", "#E6282828", "#F0FFFFFF"); add("MediaBadgeBrush", "#DD202020", "#E6FFFFFF");
            add("MediaDisabledBrush", "#929292", "#777777"); add("MediaSwitchOffBrush", "#353535", "#E5E5E5");
            add("MediaSwitchBorderBrush", "#A6A6A6", "#737373"); add("MediaSwitchKnobBrush", "#E6E6E6", "#555555");
            string accent = AccentName == "Blue" ? (light ? "#0067C0" : "#60B5FF")
                : AccentName == "Teal" ? (light ? "#007C78" : "#52D3C6")
                : AccentName == "Purple" ? (light ? "#7440B6" : "#BD9AF7")
                : AccentName == "Rose" ? (light ? "#B92C61" : "#FF85B5") : (light ? "#AF5200" : "#FF963F");
            var color = (Color)ColorConverter.ConvertFromString(accent);
            values["MediaAccentBrush"] = values["MediaFavoriteBrush"] = color;
            values["MediaAccentHoverBrush"] = Mix(color, light ? Colors.Black : Colors.White, .16);
            values["MediaAccentPressedBrush"] = Mix(color, Colors.Black, .20);
            values["MediaSelectionBrush"] = Mix(color, light ? Colors.White : Color.FromRgb(32,32,32), .85);
            values["MediaAccentTextBrush"] = light ? Colors.White : Color.FromRgb(24,24,24);
            values["PlayGradient"] = color;
            values[SystemColors.WindowBrushKey] = values["MediaSurfaceBrush"];
            values[SystemColors.WindowTextBrushKey] = values["MediaTextBrush"];
            values[SystemColors.ControlBrushKey] = values["MediaRaisedBrush"];
            values[SystemColors.ControlTextBrushKey] = values["MediaTextBrush"];
            values[SystemColors.HighlightBrushKey] = values["MediaSelectionBrush"];
            return values;
        }
        private static Color Mix(Color a, Color b, double ratio) => Color.FromRgb(
            (byte)(a.R * (1-ratio) + b.R * ratio), (byte)(a.G * (1-ratio) + b.G * ratio), (byte)(a.B * (1-ratio) + b.B * ratio));
        private static void ApplyResources(ResourceDictionary dictionary, Dictionary<object,Color> palette)
        {
            foreach (var child in dictionary.MergedDictionaries) ApplyResources(child, palette);
            var ownKeys = dictionary.Keys.Cast<object>().ToArray();
            if (ownKeys.Contains("MediaPopupAnimation")) dictionary["MediaPopupAnimation"] = popupAnimation;
            foreach (var key in ownKeys)
                if (palette.TryGetValue(key, out var color)) dictionary[key] = new SolidColorBrush(color);
        }
    }
}
