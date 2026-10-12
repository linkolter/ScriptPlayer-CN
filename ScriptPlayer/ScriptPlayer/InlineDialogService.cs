using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScriptPlayer.Shared;

namespace ScriptPlayer
{
    // Explicitly opted-in setting editors only. The async host never starts a nested message loop.
    public static class InlineDialogService
    {
        private sealed class Session { public TaskCompletionSource<bool?> Completion = new TaskCompletionSource<bool?>(TaskCreationOptions.RunContinuationsAsynchronously); public bool? Result; }
        private static readonly Dictionary<Window,Session> sessions = new Dictionary<Window,Session>();
        public static bool IsOpen => sessions.Count != 0;
        public static event Action<Window> Opened;
        public static async Task<bool?> ShowAsync(Window dialog)
        {
            var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (main == null) return dialog.ShowDialog();
            var video=(Grid)main.FindName("VideoOverlayLayer");
            var target=((FrameworkElement)main.FindName("GridVideo")).IsVisible ? video : (Grid)main.FindName("ShellRoot");
            var content=dialog.Content as FrameworkElement;
            if(content==null) throw new InvalidOperationException("设置页内容为空。");
            var session=new Session(); sessions.Add(dialog,session);
            bool enabled=GlobalCommandManager.IsEnabled;GlobalCommandManager.IsEnabled=false;
            var oldFocus=Keyboard.FocusedElement;
            var previous=target.Children.OfType<FrameworkElement>().Where(x=>x.Tag as string=="InlineDialogLayer").ToArray();
            foreach(var item in previous)item.IsEnabled=false;
            var layer=new Grid { Background=new SolidColorBrush(Color.FromArgb(175,0,0,0)),Tag="InlineDialogLayer" };
            Grid.SetRowSpan(layer,20);Grid.SetColumnSpan(layer,20);Panel.SetZIndex(layer,200);
            var card=new Border { BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(24),Margin=new Thickness(20),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Focusable=true };
            card.SetResourceReference(Border.BackgroundProperty,"MediaSurfaceBrush");
            card.SetResourceReference(Border.BorderBrushProperty,"MediaBorderBrush");
            card.SetResourceReference(System.Windows.Documents.TextElement.FontFamilyProperty,"MediaFontFamily");
            card.SetResourceReference(System.Windows.Documents.TextElement.FontSizeProperty,"MediaBodyFontSize");
            card.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,"MediaTextBrush");
            KeyboardNavigation.SetTabNavigation(card,KeyboardNavigationMode.Cycle);
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
            var heading=new DockPanel { Margin=new Thickness(0,0,0,18) };
            var close=new Button { Content=ScriptPlayer.Localization.ChineseLocalization.Translate("关闭"),Padding=new Thickness(12,6,12,6) };DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
            var title=new TextBlock { Text=ScriptPlayer.Localization.ChineseLocalization.Translate(dialog.Title),FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,16,0),VerticalAlignment=VerticalAlignment.Center };
            title.SetResourceReference(TextBlock.FontSizeProperty,"MediaSectionFontSize");heading.Children.Add(title);
            close.Click+=(s,e)=>Complete(dialog,false);layout.Children.Add(heading);
            dialog.Content=null;content.DataContext=dialog.DataContext;
            if (!double.IsNaN(dialog.Height)) content.MinHeight=Math.Max(content.MinHeight,Math.Max(140,dialog.Height-40));
            NormalizeBackgrounds(content);
            if (dialog is Dialogs.GeneratorSettingsDialog || dialog is Dialogs.ThumbnailGeneratorSettingsDialog
                || dialog is Dialogs.ThumbnailBannerGeneratorSettingsDialog || dialog is Dialogs.PreviewGeneratorSettingsDialog)
                NormalizeLegacyLayout(content);
            ScriptPlayer.Localization.ChineseLocalization.TranslateTree(content);
            foreach(object key in dialog.Resources.Keys)content.Resources[key]=dialog.Resources[key];
            var scroll=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto };
            Grid.SetRow(scroll,1);layout.Children.Add(scroll);card.Child=layout;layer.Children.Add(card);
            double width=double.IsNaN(dialog.Width)?660:Math.Max(440,dialog.Width);
            // Legacy fixed columns may scroll; a MaxWidth cap used to crop their rightmost controls.
            content.Width=double.NaN; content.MinWidth=0; content.MaxWidth=double.PositiveInfinity;
            Action fit=()=>{card.Width=Math.Max(260,Math.Min(width,target.ActualWidth-40));card.MaxHeight=Math.Max(180,target.ActualHeight-40);};
            SizeChangedEventHandler resized=(s,e)=>fit();target.SizeChanged+=resized;
            layer.MouseDown+=(s,e)=>e.Handled=true;
            layer.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){e.Handled=true;Complete(dialog,false);}};
            layer.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{if((e.OriginalSource as Button)?.IsCancel==true)Complete(dialog,false);}));
            EventHandler closed=(s,e)=>session.Completion.TrySetResult(session.Result);dialog.Closed+=closed;
            EventHandler mainClosed=(s,e)=>Complete(dialog,false);main.Closed+=mainClosed;
            try
            {
                target.Children.Add(layer);fit();main.UpdateLayout();card.Focus();
                if(dialog is Dialogs.ShortcutInputDialog shortcut)shortcut.PrepareInlineInput(content);
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent,dialog));
                Opened?.Invoke(dialog);
                return await session.Completion.Task;
            }
            finally
            {
                dialog.Closed-=closed;main.Closed-=mainClosed;target.SizeChanged-=resized;
                target.Children.Remove(layer);scroll.Content=null;sessions.Remove(dialog);
                foreach(var item in previous)item.IsEnabled=true;
                GlobalCommandManager.IsEnabled=enabled;
                if(oldFocus!=null)Keyboard.Focus(oldFocus);
            }
        }
        private static void NormalizeBackgrounds(DependencyObject root)
        {
            if (root is Panel panel && panel.Background is SolidColorBrush fill && fill.Color == Colors.White) panel.Background=Brushes.Transparent;
            if (root is Control control && !IsIconFont(control.FontFamily))
            { control.SetResourceReference(Control.FontFamilyProperty,"MediaFontFamily");control.SetResourceReference(Control.FontSizeProperty,"MediaBodyFontSize"); }
            if (root is TextBlock label && !IsIconFont(label.FontFamily))
            {
                bool warning=label.Foreground is SolidColorBrush color && color.Color.R>150 && color.Color.R>color.Color.G*1.4 && color.Color.R>color.Color.B*1.4;
                label.SetResourceReference(TextBlock.FontFamilyProperty,"MediaFontFamily");label.SetResourceReference(TextBlock.FontSizeProperty,"MediaBodyFontSize");
                label.SetResourceReference(TextBlock.ForegroundProperty,warning ? "MediaErrorBrush" : "MediaTextBrush");label.TextWrapping=TextWrapping.Wrap;
            }
            if (root is TextBox text) { text.Height=double.NaN;text.MinHeight=36;text.Padding=new Thickness(8,5,8,5);text.VerticalContentAlignment=VerticalAlignment.Center; }
            if (root is PasswordBox password) { password.Height=double.NaN;password.MinHeight=36;password.Padding=new Thickness(8,5,8,5); }
            if (root is Button button) { button.Height=double.NaN;button.MinHeight=36;button.Padding=new Thickness(12,7,12,7); }
            if (root is CheckBox check) { check.SetResourceReference(FrameworkElement.StyleProperty,"ToggleSwitch");check.Height=double.NaN; }
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray()) NormalizeBackgrounds(child);
        }
        private static bool IsIconFont(FontFamily font)
        {
            var source=font?.Source ?? "";
            return source.IndexOf("MDL2",StringComparison.OrdinalIgnoreCase)>=0 || source.IndexOf("Webdings",StringComparison.OrdinalIgnoreCase)>=0
                || source.IndexOf("Wingdings",StringComparison.OrdinalIgnoreCase)>=0 || source.IndexOf("FontAwesome",StringComparison.OrdinalIgnoreCase)>=0;
        }
        // Old generator forms positioned every row with absolute top margins. Keep their
        // existing controls, names and bindings, but let those rows measure their new fonts.
        private static void NormalizeLegacyLayout(DependencyObject root)
        {
            foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray()) NormalizeLegacyLayout(child);
            if (!(root is Grid grid) || grid.RowDefinitions.Count!=0 || grid.ColumnDefinitions.Count!=0) return;
            var children=grid.Children.OfType<FrameworkElement>().ToArray();
            if (children.Length<3 || children.Any(child=>Grid.GetRow(child)!=0 || Grid.GetColumn(child)!=0)
                || children.Count(child=>child.Margin.Top>0)<children.Length/2) return;
            var rows=new StackPanel { Margin=new Thickness(8) };
            WrapPanel row=null;double rowTop=double.MinValue;
            foreach(var child in children.OrderBy(child=>child.Margin.Top).ThenBy(child=>child.Margin.Left))
            {
                double top=child.Margin.Top;
                if(row==null || top-rowTop>8) { row=new WrapPanel { Margin=new Thickness(0,0,0,8) };rows.Children.Add(row);rowTop=top; }
                grid.Children.Remove(child);child.Margin=new Thickness(0,0,12,0);child.VerticalAlignment=VerticalAlignment.Center;
                if (child is TextBox input && double.IsNaN(input.Width)
                    && !System.Windows.Data.BindingOperations.IsDataBound(input,FrameworkElement.WidthProperty))
                { input.Width=220;input.MinWidth=Math.Max(input.MinWidth,160); }
                if(child is CheckBox check && check.Content is string caption) check.Content=new TextBlock { Text=caption,TextWrapping=TextWrapping.Wrap };
                row.Children.Add(child);
            }
            grid.Children.Add(rows);
        }
        public static void Complete(Window dialog,bool? result)
        {
            if(sessions.TryGetValue(dialog,out var session)) { session.Result=result;dialog.Close(); }
            else dialog.DialogResult=result;
        }
    }
}
