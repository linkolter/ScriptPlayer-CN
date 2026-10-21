using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using ScriptPlayer.ViewModels;

internal static class MotionPreferenceTests
{
    static int count;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); count++; }
    static readonly XmlSerializer serializer = new XmlSerializer(typeof(SettingsViewModel));
    static SettingsViewModel Load(string xml) { using (var reader = new StringReader(xml)) return (SettingsViewModel)serializer.Deserialize(reader); }
    static SettingsViewModel Roundtrip(SettingsViewModel value) { using (var writer = new StringWriter()) { serializer.Serialize(writer, value); return Load(writer.ToString()); } }
    static string State(SettingsViewModel s) => string.Join("|", s.InterfaceMotionMode, s.EnableInterfaceAnimations, s.AnimateInteraction, s.AnimateSwitches, s.AnimatePages, s.AnimateTransport, s.AnimateMenus, s.AnimatePanels, s.AnimateCovers, s.SimplifyInterfaceAnimations, s.AutoPlayCoverAnimations);
    [STAThread] static int Main()
    {
        try
        {
            var settings = new SettingsViewModel();
            Check(settings.InterfaceMotionMode == "Light" && settings.EnableInterfaceAnimations && !settings.AnimatePages && !settings.AnimateCovers && !settings.AutoPlayCoverAnimations, "New profile has Light defaults without cover autoplay");
            Check(State(Load("<SettingsViewModel/>")) == State(settings), "Missing preferences preserve Light defaults");
            foreach (bool enabled in new[] { false, true })
            {
                var old = Load("<SettingsViewModel><EnableInterfaceAnimations>" + enabled.ToString().ToLowerInvariant() + "</EnableInterfaceAnimations></SettingsViewModel>");
                Check(old.InterfaceMotionMode == (enabled ? "Custom" : "Off") && old.EnableInterfaceAnimations == enabled && old.AnimatePages && old.AnimateMenus && old.AutoPlayCoverAnimations, "Legacy preference migrates without losing prior effects: " + enabled);
            }
            foreach (string mode in new[] { "Off", "Light", "Standard", "Custom" })
            {
                settings.InterfaceMotionMode = mode;
                Check(State(Roundtrip(settings)) == State(settings), "XML retains preset and every flag: " + mode);
                Check(State(settings.Duplicate()) == State(settings), "Duplicate retains preset and every flag: " + mode);
                foreach (bool old in new[] { true, false })
                foreach (bool reverse in new[] { true, false })
                {
                    string a = "<InterfaceMotionMode>" + mode + "</InterfaceMotionMode>", b = "<EnableInterfaceAnimations>" + old.ToString().ToLowerInvariant() + "</EnableInterfaceAnimations>";
                    var loaded = Load("<SettingsViewModel>" + (reverse ? b+a : a+b) + "</SettingsViewModel>");
                    Check(loaded.InterfaceMotionMode == mode && loaded.EnableInterfaceAnimations == (mode != "Off"), "Explicit mode wins over legacy flag in either order: " + mode + "/" + old + "/" + reverse);
                }
            }
            settings.AnimatePages = false; settings.AnimateCovers = true; settings.AnimateSwitches = false;
            settings.SimplifyInterfaceAnimations = false; settings.AutoPlayCoverAnimations = true;
            Check(settings.InterfaceMotionMode == "Custom", "Editing an effect selects Custom");
            foreach (string mode in new[] { "Custom", "Off" })
            {
                settings.InterfaceMotionMode = mode;
                Check(State(Roundtrip(settings)) == State(settings), "Saved custom flags survive XML while " + mode);
                Check(State(settings.Duplicate()) == State(settings), "Saved custom flags survive Duplicate while " + mode);
            }
            settings.AnimateMenus = !settings.AnimateMenus;
            Check(settings.EnableInterfaceAnimations && settings.InterfaceMotionMode == "Custom", "Editing an Off effect explicitly enables Custom");
            settings.InterfaceMotionMode = "Standard";
            settings.EnableInterfaceAnimations = false; settings.EnableInterfaceAnimations = true;
            Check(settings.AnimatePages && settings.AnimatePanels && settings.InterfaceMotionMode == "Custom", "Legacy master restores selected effects");
            settings.PropertyChanged += (s,e) => Check(SettingsViewModel.IsInterfacePreference(e.PropertyName), "Notification stays presentation-only: " + e.PropertyName);
            settings.InterfaceMotionMode = "Light";
            settings.SimplifyInterfaceAnimations = !settings.SimplifyInterfaceAnimations;
            settings.AutoPlayCoverAnimations = !settings.AutoPlayCoverAnimations;
            foreach (string name in new[] { "AnimateInteraction", "AnimateSwitches", "AnimatePages", "AnimateTransport", "AnimateMenus", "AnimatePanels", "AnimateCovers" })
            {
                var property = typeof(SettingsViewModel).GetProperty(name);
                property.SetValue(settings, !(bool)property.GetValue(settings));
            }
            Console.WriteLine("Failures: 0\nPasses: " + count); return 0;
        }
        catch (Exception ex) { Console.WriteLine("ERROR: " + ex + "\nFailures: 1"); return 1; }
    }
}
