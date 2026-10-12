using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
class SeekBoundsProbe
{
    [STAThread] static int Main(string[] args)
    {
        var assembly=Assembly.LoadFrom(args[0]);
        var type=assembly.GetType("ScriptPlayer.Controls.MediaSeekBar",true);
        var bar=(FrameworkElement)Activator.CreateInstance(type);
        type.GetProperty("Duration").SetValue(bar,TimeSpan.FromSeconds(10),null);
        int failures=0;
        foreach(int width in new[]{24,120,502}) foreach(int second in new[]{0,10})
        {
            bar.Measure(new Size(width,26)); bar.Arrange(new Rect(0,0,width,26));
            type.GetProperty("Progress").SetValue(bar,TimeSpan.FromSeconds(second),null);
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) type.GetMethod("OnRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bar,new object[]{dc});
            var bounds=visual.Drawing.Bounds;
            bool valid=bounds.Left>=0 && bounds.Right<=width && bounds.Top>=0 && bounds.Bottom<=26;
            Console.WriteLine((valid?"PASS":"FAIL")+": width="+width+" second="+second+" drawing="+bounds);
            if(!valid)failures++;
        }
        Console.WriteLine("Failures: "+failures); return failures==0?0:1;
    }
}