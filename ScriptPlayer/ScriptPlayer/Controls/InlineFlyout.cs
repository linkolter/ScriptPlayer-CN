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
                Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                if (!value) Closed?.Invoke(this, EventArgs.Empty);
            }
        }
        public event EventHandler Closed;
    }
}
