using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ScriptPlayer.Controls
{
    // Show complete chips that fit, then expose all remaining tags through +N.
    public sealed class CompactTagStrip : UserControl
    {
        public static readonly DependencyProperty TagsProperty = DependencyProperty.Register(nameof(Tags), typeof(IEnumerable<string>), typeof(CompactTagStrip), new PropertyMetadata(null, (d, e) => ((CompactTagStrip)d).Refresh()));
        public IEnumerable<string> Tags { get => (IEnumerable<string>)GetValue(TagsProperty); set => SetValue(TagsProperty, value); }
        private readonly StackPanel items = new StackPanel { Orientation = Orientation.Horizontal };
        public CompactTagStrip() { Content = items; Height = 25; SizeChanged += (s, e) => Refresh(); }
        private Border Chip(string text, double maxWidth)
        {
            return new Border { Background = new SolidColorBrush(Color.FromRgb(57, 57, 61)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 5, 0), MaxWidth = Math.Max(1, maxWidth), Child = new TextBlock { Text = text, FontSize = 10, Foreground = Brushes.LightGray, TextTrimming = TextTrimming.CharacterEllipsis } };
        }
        private void Refresh()
        {
            items.Children.Clear();
            var tags = (Tags ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            ToolTip = string.Join(" · ", tags);
            double available = ActualWidth;
            if (available <= 0 || tags.Length == 0) return;
            int shown = 0;
            foreach (var tag in tags.Take(4))
            {
                double reserve = shown + 1 < tags.Length ? 42 : 0;
                var chip = Chip(tag, Math.Max(1, available - reserve));
                chip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (available - reserve < 45 || chip.DesiredSize.Width > available - reserve) break;
                chip.ToolTip = tag; items.Children.Add(chip); shown++; available -= chip.DesiredSize.Width;
            }
            if (shown < tags.Length) items.Children.Add(Chip("+" + (tags.Length - shown), available));
        }
    }
}
