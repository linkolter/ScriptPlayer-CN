using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ScriptPlayer.Shared
{
    public enum PanelCollapseAxis { None, Horizontal, Vertical }

    public static class HideOnHover
    {
        // Reserve a narrow, hit-testable dock edge instead of placing native
        // video over invisible controls. Only the main window opts into this.
        public static readonly DependencyProperty CollapseAxisProperty = DependencyProperty.RegisterAttached(
            "CollapseAxis", typeof(PanelCollapseAxis), typeof(HideOnHover),
            new PropertyMetadata(PanelCollapseAxis.None));

        private static readonly DependencyProperty OriginalMaximumProperty = DependencyProperty.RegisterAttached(
            "OriginalMaximum", typeof(double), typeof(HideOnHover), new PropertyMetadata(double.PositiveInfinity));

        public static void SetCollapseAxis(DependencyObject element, PanelCollapseAxis value) => element.SetValue(CollapseAxisProperty, value);
        public static PanelCollapseAxis GetCollapseAxis(DependencyObject element) => (PanelCollapseAxis)element.GetValue(CollapseAxisProperty);

        private static void SetPanelVisible(FrameworkElement element, bool visible)
        {
            element.Opacity = visible ? 1.0 : 0.0;
            double maximum = visible ? (double)element.GetValue(OriginalMaximumProperty) : 10.0;
            switch (GetCollapseAxis(element))
            {
                case PanelCollapseAxis.Horizontal: element.SetCurrentValue(FrameworkElement.MaxWidthProperty, maximum); break;
                case PanelCollapseAxis.Vertical: element.SetCurrentValue(FrameworkElement.MaxHeightProperty, maximum); break;
            }
        }

        public static readonly DependencyProperty OverridesHideOnHoverProperty = DependencyProperty.RegisterAttached(
            "OverridesHideOnHover", typeof(bool), typeof(HideOnHover), new PropertyMetadata(default(bool), OnIgnoresHideOnHoverPropertyChanged));

        private static void OnIgnoresHideOnHoverPropertyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(element))
                return;

            bool value = (bool)e.NewValue;

            ContextMenu menu = element as ContextMenu;
            if (menu == null) return;

            if (value)
            {
                menu.Opened += OnContextMenuOpening;
                menu.Closed += OnContextMenuClosing;
            }
            else
            {
                menu.Opened -= OnContextMenuOpening;
                menu.Closed -= OnContextMenuClosing;
            }
        }

        private static void OnContextMenuClosing(object sender, RoutedEventArgs routedEventArgs)
        {
            ContextMenuClosing(sender as ContextMenu);
        }

        private static void OnContextMenuOpening(object sender, RoutedEventArgs routedEventArgs)
        {
            ContextMenuOpening(sender as ContextMenu);
        }

        public static void SetOverridesHideOnHover(DependencyObject element, bool value)
        {
            element.SetValue(OverridesHideOnHoverProperty, value);
        }

        public static bool GetOverridesHideOnHover(DependencyObject element)
        {
            return (bool) element.GetValue(OverridesHideOnHoverProperty);
        }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.RegisterAttached(
            "IsActive", typeof(bool), typeof(HideOnHover), new PropertyMetadata(default(bool), OnIsActivePropertyChanged));

        private static void OnIsActivePropertyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(element))
                return;

            bool value = (bool) e.NewValue;

            FrameworkElement frameworkElement = element as FrameworkElement;
            if (frameworkElement == null) return;

            if (value)
            {
                if (GetCollapseAxis(element) != PanelCollapseAxis.None)
                    element.SetValue(OriginalMaximumProperty, GetCollapseAxis(element) == PanelCollapseAxis.Horizontal
                        ? frameworkElement.MaxWidth : frameworkElement.MaxHeight);
                frameworkElement.MouseEnter += OnMouseEnter;
                frameworkElement.MouseLeave += OnMouseLeave;
                frameworkElement.IsKeyboardFocusWithinChanged += OnKeyboardFocusChanged;
                if (!frameworkElement.IsMouseOver)
                    SetPanelVisible(frameworkElement, false);
            }
            else
            {
                frameworkElement.MouseEnter -= OnMouseEnter;
                frameworkElement.MouseLeave -= OnMouseLeave;
                frameworkElement.IsKeyboardFocusWithinChanged -= OnKeyboardFocusChanged;
                SetPanelVisible(frameworkElement, true);
            }
        }

        public static void SetIsActive(DependencyObject element, bool value)
        {
            element.SetValue(IsActiveProperty, value);
        }

        public static bool GetIsActive(DependencyObject element)
        {
            return (bool) element.GetValue(IsActiveProperty);
        }

        private static void OnMouseLeave(object sender, MouseEventArgs mouseEventArgs)
        {
            FrameworkElement element = sender as FrameworkElement;
            if (element == null)
                return;

            if (!element.IsKeyboardFocusWithin && !element.IsMouseCaptureWithin)
                SetPanelVisible(element, false);
        }

        private static void OnKeyboardFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var element = (FrameworkElement)sender;
            SetPanelVisible(element, element.IsKeyboardFocusWithin || element.IsMouseOver || element.IsMouseCaptureWithin);
        }

        public static void ContextMenuOpening(ContextMenu menu)
        {
            FrameworkElement target = FindPlacementTarget(menu);
            if(target != null)
                SetPanelVisible(target, true);
        }

        private static FrameworkElement FindPlacementTarget(ContextMenu menu)
        {
            FrameworkElement element = menu.PlacementTarget as FrameworkElement;

            while (element != null)
            {
                if (GetIsActive(element))
                {
                    return element;
                }

                element = VisualTreeHelper.GetParent(element) as FrameworkElement;
            }

            return null;
        }

        public static void ContextMenuClosing(ContextMenu menu)
        {
            FrameworkElement target = FindPlacementTarget(menu);
            if (target != null)
                if(!target.IsMouseOver && !target.IsKeyboardFocusWithin)
                    SetPanelVisible(target, false);
        }

        private static void OnMouseEnter(object sender, MouseEventArgs mouseEventArgs)
        {
            SetPanelVisible((FrameworkElement)sender, true);
        }
    }
}
