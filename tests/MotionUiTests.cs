using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using ScriptPlayer;
using ScriptPlayer.Themes;
using ScriptPlayer.ViewModels;
using ScriptPlayer.Localization;

internal static class MotionUiTests
{
    static MainWindow window; static string output; static int count, result;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); File.AppendAllText(Path.Combine(output,"results.log"),"PASS: " + name + "\n"); count++; }
    static void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);
    static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T value) yield return value;
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Find<T>(VisualTreeHelper.GetChild(root,i))) yield return child;
    }
    static void Snapshot(string name)
    {
        window.UpdateLayout(); var bmp = new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bmp.Render(window);
        var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using(var file=File.Create(Path.Combine(output,name+".png"))) png.Save(file);
    }
    [STAThread] static int Main(string[] args)
    {
        output=args[1]; Directory.CreateDirectory(output);
        var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose }; ChineseLocalization.Initialize();
        string id=Guid.NewGuid().ToString("N"); InstanceHandler.Startup("MotionTest-"+id,"MotionPipe-"+id);
        XNamespace p="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var resources=XDocument.Load(args[0]).Root.Element(p+"Application.Resources").Element(p+"ResourceDictionary");
        app.Resources=(ResourceDictionary)XamlReader.Parse(resources.ToString(),new ParserContext{BaseUri=new Uri("pack://application:,,,/ScriptPlayer;component/App.xaml")});
        window=new MainWindow(); window.InitializeComponent(); app.MainWindow=window;
        var deadline=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(50)};
        deadline.Tick+=(s,e)=>{File.AppendAllText(Path.Combine(output,"results.log"),"ERROR: deadline\n");Environment.Exit(2);}; deadline.Start();
        window.Loaded+=(s,e)=>app.Dispatcher.BeginInvoke(new Action(async()=>
        {
            try
            {
                await Task.Delay(1000); var settings=window.ViewModel.Settings;
                var realtime=window.ViewModel.RealtimePlayback;
                Check(realtime!=null,"Tests use the production realtime service");
                var version=typeof(ScriptPlayer.Playback.RealtimePlaybackService).GetField("version",BindingFlags.NonPublic|BindingFlags.Instance);
                long before=(long)version.GetValue(realtime);
                Call("NavigateTo","settings"); Call("SelectSettingsPage","Animations"); window.UpdateLayout();
                var mode=(ComboBox)window.FindName("InterfaceMotionModeSelector");
                Check(mode.IsVisible && mode.Items.Count==4,"Four animation presets are exposed in the settings subpage");
                foreach(string key in new[]{"Off","Light","Standard","Custom"}) { mode.SelectedValue=key; Check(settings.InterfaceMotionMode==key,"Mode selector applies stable key: "+key); }
                var panel=(FrameworkElement)window.FindName("SettingsPageHost");
                var expand=Find<Expander>(panel).Single(); expand.IsExpanded=true; window.UpdateLayout();
                foreach(string property in new[]{"AnimateInteraction","AnimateSwitches","AnimatePages","AnimateTransport","AnimateMenus","AnimatePanels","AnimateCovers","SimplifyInterfaceAnimations","AutoPlayCoverAnimations"})
                {
                    var toggle=(CheckBox)window.FindName(property+"Switch");
                    var info=typeof(SettingsViewModel).GetProperty(property); bool original=(bool)info.GetValue(settings);
                    toggle.IsChecked=!original; Check((bool)info.GetValue(settings)==!original,"Real toggle updates preference: "+property);
                }
                ((Button)window.FindName("ResetInterfaceMotion")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(settings.InterfaceMotionMode=="Light" && settings.SimplifyInterfaceAnimations && !settings.AutoPlayCoverAnimations && !settings.AnimateCovers,"Reset restores Light defaults");
                ((Button)window.FindName("PreviewInterfaceMotion")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(180); Check(MotionPresentation.ActiveAnimations==0,"Preview finishes without retained clocks");
                foreach(string language in new[]{"zh-CN","en"}) foreach(string theme in new[]{"Dark","Light"})
                {
                    settings.InterfaceLanguage=language; settings.InterfaceTheme=theme; window.Width=760; window.Height=620; window.UpdateLayout();
                    Check(((ScrollViewer)window.FindName("SettingsContentScroll")).ScrollableWidth<1,"Narrow motion settings fit: "+language+"/"+theme);
                    Check(language!="en" || ((TextBlock)window.FindName("SettingsPageHeading")).Text.EndsWith("Animations and performance"),"Motion heading follows language: "+language+"/"+theme);
                    Snapshot("motion-"+language+"-"+theme);
                }
                settings.InterfaceMotionMode="Standard"; settings.SimplifyInterfaceAnimations=false;
                var test=new Border{Width=100,Height=36,Background=Brushes.Gray};
                // Attach a visible fixture to the page without touching the native video or timeline.
                var section=(StackPanel)((ContentControl)panel).Content; section.Children.Insert(0,test); window.UpdateLayout();
                MotionPresentation.Configure(settings,true,2);
                MotionPresentation.SetVisible(test,false,InterfaceMotion.Menu); Check(!test.IsHitTestVisible && test.Visibility==Visibility.Collapsed,"Hidden element immediately stops hit testing");
                for(int i=0;i<20;i++) MotionPresentation.SetVisible(test,i%2==0,InterfaceMotion.Menu);
                MotionPresentation.SetVisible(test,true,InterfaceMotion.Menu); Check(test.IsHitTestVisible && test.Visibility==Visibility.Visible,"Rapid reverse makes the latest visible intent immediately clickable");
                await Task.Delay(220); Check(test.Opacity==1 && !test.HasAnimatedProperties && MotionPresentation.ActiveAnimations==0,"Rapid reverse settles at full opacity without clocks");
                MotionPresentation.FadeIn(test,InterfaceMotion.Page); settings.InterfaceMotionMode="Off"; Check(test.Opacity==1 && !test.HasAnimatedProperties,"Switching Off cancels an active animation");
                settings.InterfaceMotionMode="Standard"; MotionPresentation.Configure(settings,false,2);
                Check(Enum.GetValues(typeof(InterfaceMotion)).Cast<InterfaceMotion>().All(x=>!MotionPresentation.Allows(x)) && !MotionPresentation.AnimatePreviews,"Windows animation disable suppresses all effects and preview autoplay");
                settings.SimplifyInterfaceAnimations=true; MotionPresentation.Configure(settings,true,0);
                Check(settings.InterfaceMotionMode=="Standard" && MotionPresentation.EffectiveMode=="Light" && !MotionPresentation.Allows(InterfaceMotion.Page) && MotionPresentation.Allows(InterfaceMotion.Switch),"Software rendering reduces effects without overwriting the saved preset");
                settings.SimplifyInterfaceAnimations=false; MotionPresentation.Configure(settings,true,2);
                MotionPresentation.FadeIn(test,InterfaceMotion.Page); MotionPresentation.Suspend(true);
                Check(MotionPresentation.ActiveAnimations==0 && test.Opacity==1 && !MotionPresentation.Allows(InterfaceMotion.Page),"Minimized policy cancels clocks and suspends new effects"); MotionPresentation.Suspend(false);
                MotionPresentation.FadeIn(test,InterfaceMotion.Page); section.Children.Remove(test); await Task.Delay(60); Check(MotionPresentation.ActiveAnimations==0,"Unloaded fixture releases its animation");
                Call("ConfigureInterfaceMotion");
                var toggleSwitch=(CheckBox)window.FindName("AnimatePagesSwitch"); settings.SimplifyInterfaceAnimations=false; settings.AnimateSwitches=true;
                toggleSwitch.IsChecked=false; toggleSwitch.IsChecked=true; await Task.Delay(180); window.UpdateLayout();
                var knob=(FrameworkElement)toggleSwitch.Template.FindName("SwitchKnob",toggleSwitch);
                Check(Math.Abs(knob.RenderTransform.Value.OffsetX-18)<.1 && !knob.HasAnimatedProperties,"Switch settles in the checked position after rapid retargeting");
                System.Windows.Data.BindingOperations.ClearBinding(toggleSwitch,CheckBox.IsCheckedProperty);
                toggleSwitch.IsThreeState=true; toggleSwitch.IsChecked=null; await Task.Delay(180);
                Check(Math.Abs(knob.RenderTransform.Value.OffsetX-9)<.1,"Three-state switch retains its centered indeterminate position");
                toggleSwitch.IsChecked=false; await Task.Delay(180);
                Check(Math.Abs(knob.RenderTransform.Value.OffsetX)<.1,"Indeterminate to unchecked returns the knob to zero");
                toggleSwitch.IsChecked=true; toggleSwitch.IsChecked=false; await Task.Delay(180);
                Check(Math.Abs(knob.RenderTransform.Value.OffsetX)<.1,"Rapid checked to unchecked returns the knob to zero");
                settings.InterfaceMotionMode="Off"; toggleSwitch.IsChecked=true; toggleSwitch.IsChecked=false;
                Check(Math.Abs(knob.RenderTransform.Value.OffsetX)<.1 && !knob.HasAnimatedProperties,"Disabled animation updates the unchecked knob immediately");
                var policyEvent=typeof(MotionPresentation).GetField("PolicyChanged",BindingFlags.NonPublic|BindingFlags.Static);
                int listeners=((Delegate)policyEvent.GetValue(null))?.GetInvocationList().Length??0;
                var preview=new ScriptPlayer.Controls.VideoDetailsPreview { ViewModel=window.ViewModel };
                section.Children.Insert(0,preview); await Task.Delay(50);
                var gif=(ScriptPlayer.Shared.GifPlayer)preview.FindName("player");
                settings.AutoPlayCoverAnimations=true;
                MotionPresentation.Configure(settings,true,2);
                Check(gif.AutoPlay,"Visible preview consumes the independent autoplay preference");
                preview.Visibility=Visibility.Collapsed; Check(!gif.AutoPlay && !gif.HasAnimatedProperties,"Hidden preview stops its presentation clock");
                preview.Visibility=Visibility.Visible; Check(gif.AutoPlay,"Visible preview restores permitted autoplay");
                MotionPresentation.Configure(settings,false,2); Check(!gif.AutoPlay,"System reduction suppresses GIF autoplay");
                MotionPresentation.Configure(settings,true,2);
                section.Children.Remove(preview); await Task.Delay(50);
                Check((((Delegate)policyEvent.GetValue(null))?.GetInvocationList().Length??0)==listeners && !gif.AutoPlay,"Unloaded preview unsubscribes and clears autoplay");
                Call("ConfigureInterfaceMotion");
                await Task.Delay(850);
                Check(ReferenceEquals(realtime,window.ViewModel.RealtimePlayback) && (long)version.GetValue(realtime)==before,"Every animation preference leaves realtime configuration and output invalidation unchanged");
                Check(!(bool)typeof(MainWindow).GetField("pendingPlaybackSettings",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window),"Animation preferences never enqueue playback settings reapplication");
                settings.InterfaceLanguage="zh-CN"; settings.InterfaceTheme="Dark"; window.Width=1180; window.Height=940; expand.IsExpanded=true;
                Snapshot("motion-wide-dark");
                Call("NavigateTo","home"); Snapshot("home-icons-dark");
                File.AppendAllText(Path.Combine(output,"results.log"),"Failures: 0\nPasses: "+count+"\n");
            }
            catch(Exception ex) {File.AppendAllText(Path.Combine(output,"results.log"),"ERROR: "+ex+"\nFailures: 1\n");result=1;}
            finally {deadline.Stop(); window.Close(); Check(MotionPresentation.ActiveAnimations==0,"Window disposal releases presentation clocks"); app.Shutdown(Environment.ExitCode);}
        }));
        app.Run(window); return result;
    }
}
