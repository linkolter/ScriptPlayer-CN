using System;
using System.Windows;
using System.Windows.Controls;
namespace ScriptPlayer.Controls
{
    // An in-tree flyout: no popup HWND, activation change or mouse capture.
    public sealed class InlineFlyout : Border
    {
        public InlineFlyout() { Visibility = Visibility.Collapsed; }
        public bool IsOpen
        {
            get => Visibility == Visibility.Visible;
            set
            {
                if (value == IsOpen) return;
                var effect = Themes.MotionPresentation.Allows(Themes.InterfaceMotion.Panel) ? Themes.InterfaceMotion.Panel : Themes.InterfaceMotion.Menu;
                Themes.MotionPresentation.SetVisible(this, value, effect);
                if (value && effect == Themes.InterfaceMotion.Panel) Themes.MotionPresentation.FadeIn(this, effect, true);
                if (!value) Closed?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler Closed;
    }
}
