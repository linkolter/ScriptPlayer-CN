using System;
using System.IO;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ScriptPlayer.Localization;
using ScriptPlayer.ViewModels;
class InterfacePreferenceTests
{
    static int failures;
    static void Check(bool result,string name){Console.WriteLine((result?"PASS: ":"FAIL: ")+name);if(!result)failures++;}
    [STAThread] static int Main()
    {
        var settings=new SettingsViewModel();
        Check(settings.InterfaceLanguage=="zh-CN" && settings.InterfaceTheme=="Dark" && settings.InterfaceAccent=="Orange" && settings.EnableInterfaceAnimations,"Missing preferences preserve existing Chinese dark defaults");
        var serializer=new XmlSerializer(typeof(SettingsViewModel));
        using(var reader=new StringReader("<SettingsViewModel><InterfaceLanguage>unknown</InterfaceLanguage><InterfaceTheme>unknown</InterfaceTheme><InterfaceAccent>unknown</InterfaceAccent><EnableInterfaceAnimations>false</EnableInterfaceAnimations></SettingsViewModel>"))
        {
            var loaded=(SettingsViewModel)serializer.Deserialize(reader);
            Check(loaded.InterfaceLanguage=="zh-CN" && loaded.InterfaceTheme=="Dark" && loaded.InterfaceAccent=="Orange" && !loaded.EnableInterfaceAnimations,"Invalid stored choices fall back without losing animation preference");
        }
        using(var reader=new StringReader("<SettingsViewModel><ShowHeatMap>true</ShowHeatMap></SettingsViewModel>"))
        {
            var loaded=(SettingsViewModel)serializer.Deserialize(reader);
            Check(loaded.ShowHeatMap && loaded.InterfaceLanguage=="zh-CN" && loaded.EnableInterfaceAnimations,"Older settings XML loads with compatible presentation defaults");
        }
        string culture=System.Globalization.CultureInfo.CurrentCulture.Name;
        ChineseLocalization.Initialize();
        var literal=new TextBlock {Text="主页"}; var data=new TextBlock();data.SetBinding(TextBlock.TextProperty,new Binding{Source="主页"});
        var editor=new TextBox {Text="Settings"};
        ChineseLocalization.TranslateTree(literal);ChineseLocalization.TranslateTree(data);ChineseLocalization.TranslateTree(editor);
        for(int i=0;i<12;i++)
        {
            ChineseLocalization.SetLanguage("en");Check(literal.Text=="Home" && data.Text=="主页" && editor.Text=="Settings","English literal/data isolation cycle "+i);
            ChineseLocalization.SetLanguage("zh-CN");Check(literal.Text=="主页" && data.Text=="主页" && editor.Text=="Settings","Chinese exact restoration cycle "+i);
        }
        Check(System.Globalization.CultureInfo.CurrentCulture.Name==culture,"UI language never changes data culture");
        Check(ChineseLocalization.TranslateFormat("找到 {0} 个设置页面；按 Enter 打开第一个结果。", "用户数据") .Contains("用户数据"),"Format translation preserves data arguments");
        Check(SettingsViewModel.IsInterfacePreference(nameof(SettingsViewModel.InterfaceLanguage)) && !SettingsViewModel.IsInterfacePreference(nameof(SettingsViewModel.ScriptDelay)),"Presentation whitelist excludes synchronization settings");
        Console.WriteLine("Failures: "+failures);return failures==0?0:1;
    }
}