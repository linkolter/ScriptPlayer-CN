using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using ScriptPlayer.ViewModels;

internal static class ReleasePrivacyTests
{
    class UnsupportedSettings : SettingsViewModel { }
    static int failures;
    const string Key = "fixture-private-handy-key-DO-NOT-USE";
    static void Check(bool value, string message) { Console.WriteLine((value ? "PASS: " : "FAIL: ") + message); if (!value) failures++; }
    static string Secret(string settings) => Path.Combine(Path.GetDirectoryName(settings), "Private", "HandyConnection.dpapi");
    static string Status(SettingsViewModel vm) => (string)typeof(SettingsViewModel).GetProperty("PrivateSettingsStatus")?.GetValue(vm);
    static string NewFile(string root, string name) { var path = Path.Combine(root, name, "Settings.xml"); Directory.CreateDirectory(Path.GetDirectoryName(path)); return path; }
    static void Legacy(string path) => File.WriteAllText(path, "<SettingsViewModel><HandyDeviceId>" + Key + "</HandyDeviceId><InterfaceLanguage>en</InterfaceLanguage></SettingsViewModel>");
    static bool HasPlaintext(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains(Key) || Encoding.Unicode.GetString(File.ReadAllBytes(path)).Contains(Key);
    [STAThread] static int Main(string[] args)
    {
        var root = args[0]; Directory.CreateDirectory(root);
        var path = NewFile(root, "legacy"); Legacy(path);
        var vm = SettingsViewModel.FromFile(path);
        Check(vm.HandyDeviceId == Key && vm.InterfaceLanguage == "en", "Legacy credential and ordinary settings load intact");
        var duplicate = vm.Duplicate();
        Check(duplicate.HandyDeviceId == Key, "Settings editor duplication preserves the in-memory credential");
        var xml = new StringWriter(); new XmlSerializer(typeof(SettingsViewModel)).Serialize(xml, duplicate);
        Check(!xml.ToString().Contains(Key) && !xml.ToString().Contains("<HandyDeviceId>"), "Direct XML export excludes the Handy connection key");
        vm.Save(path);
        Check(!HasPlaintext(path), "Successful migration removes plaintext from ordinary settings");
        Check(File.Exists(Secret(path)), "Credential is stored separately from settings");
        Check(SettingsViewModel.FromFile(path).HandyDeviceId == Key, "Reload retains the migrated credential");
        if (File.Exists(Secret(path)))
        {
            Check(!HasPlaintext(Secret(path)), "Private credential file contains no plaintext key");
            byte[] plain = ProtectedData.Unprotect(File.ReadAllBytes(Secret(path)), null, DataProtectionScope.CurrentUser);
            try { Check(Encoding.UTF8.GetString(plain) == Key, "Windows current-user protection round-trips the credential"); }
            finally { Array.Clear(plain, 0, plain.Length); }
            File.Delete(path);
            Check(SettingsViewModel.FromFile(path).HandyDeviceId == Key, "Missing ordinary settings does not discard an existing private credential");
            vm.Save(path);
            vm.HandyDeviceId = string.Empty; vm.Save(path);
            Check(SettingsViewModel.FromFile(path).HandyDeviceId == string.Empty, "Explicitly clearing a key persists without reviving legacy data");
        }
        var blocked = NewFile(root, "blocked-private"); Legacy(blocked); var before = File.ReadAllBytes(blocked);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(blocked), "Private"), "fixture-directory-blocker");
        var failed = SettingsViewModel.FromFile(blocked); failed.Save(blocked);
        Check(before.SequenceEqual(File.ReadAllBytes(blocked)), "Private write failure leaves legacy settings byte-for-byte intact");
        Check(!string.IsNullOrWhiteSpace(Status(failed)) && !Status(failed).Contains(Key), "Private write failure reports a redacted actionable status");
        Check(SettingsViewModel.FromFile(blocked).HandyDeviceId == Key, "Failed migration does not lose the old connection key");
        var brokenXml = NewFile(root, "broken-xml"); new SettingsViewModel { HandyDeviceId = Key }.Save(brokenXml);
        var intactSecret = File.Exists(Secret(brokenXml)) ? File.ReadAllBytes(Secret(brokenXml)) : new byte[0];
        File.WriteAllText(brokenXml, "<SettingsViewModel><broken>");
        var brokenBytes = File.ReadAllBytes(brokenXml);
        var loaded = SettingsViewModel.FromFile(brokenXml);
        // Reproduce MainViewModel.LoadSettings' actual defaults fallback if loading returns null.
        var recovered = loaded ?? new SettingsViewModel();
        Check(loaded != null && recovered.HandyDeviceId == Key, "Broken settings XML still restores the intact private credential");
        Check(!string.IsNullOrWhiteSpace(Status(recovered)), "Broken settings XML reports blocked saving without a credential leak");
        recovered.Save(brokenXml);
        Check(brokenBytes.SequenceEqual(File.ReadAllBytes(brokenXml)), "Broken XML is not automatically overwritten by defaults");
        Check(File.Exists(Secret(brokenXml)) && intactSecret.SequenceEqual(File.ReadAllBytes(Secret(brokenXml))), "Broken XML fallback cannot overwrite the intact private credential");
        recovered.Duplicate().Save(brokenXml);
        Check(File.Exists(Secret(brokenXml)) && intactSecret.SequenceEqual(File.ReadAllBytes(Secret(brokenXml))), "Editor duplication retains the broken-XML protection");
        var corrupt = NewFile(root, "corrupt"); new SettingsViewModel().Save(corrupt);
        Directory.CreateDirectory(Path.GetDirectoryName(Secret(corrupt))); File.WriteAllBytes(Secret(corrupt), new byte[] { 1, 3, 5, 7 });
        var corruptBytes = File.ReadAllBytes(Secret(corrupt)); var configBytes = File.ReadAllBytes(corrupt);
        var unreadable = SettingsViewModel.FromFile(corrupt);
        Check(!string.IsNullOrWhiteSpace(Status(unreadable)), "Unreadable encrypted file is surfaced rather than silently discarded");
        unreadable.Save(corrupt);
        Check(corruptBytes.SequenceEqual(File.ReadAllBytes(Secret(corrupt))) && configBytes.SequenceEqual(File.ReadAllBytes(corrupt)), "Unedited unreadable key cannot overwrite the private file or settings");
        unreadable.Duplicate().Save(corrupt);
        Check(corruptBytes.SequenceEqual(File.ReadAllBytes(Secret(corrupt))), "Editor duplication retains the credential failure guard");
        unreadable.HandyDeviceId = Key; unreadable.Save(corrupt);
        Check(SettingsViewModel.FromFile(corrupt).HandyDeviceId == Key && Status(unreadable) == null, "Explicit replacement of an unreadable key succeeds and clears the status");
        var invalid = NewFile(root, "serialization"); Legacy(invalid); before = File.ReadAllBytes(invalid);
        // The base serializer rejects an unregistered derived type deterministically.
        // XML serializers may escape control characters, so those are not a reliable fault.
        var bad = new UnsupportedSettings { HandyDeviceId = Key }; bad.Save(invalid);
        Check(before.SequenceEqual(File.ReadAllBytes(invalid)), "Serialization failure leaves the original settings intact");
        Check(!Directory.GetFiles(Path.GetDirectoryName(invalid), "*.new-*").Any(), "Failed settings submission removes its temporary file");
        var locked = NewFile(root, "locked"); Legacy(locked); before = File.ReadAllBytes(locked);
        var lockedVm = SettingsViewModel.FromFile(locked);
        using (var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read)) lockedVm.Save(locked);
        Check(before.SequenceEqual(File.ReadAllBytes(locked)), "Sharing violation leaves original settings intact");
        Check(!string.IsNullOrWhiteSpace(Status(lockedVm)), "Sharing violation reports failure without revealing a key");
        var ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"); File.WriteAllBytes(ffmpeg, new byte[0]);
        var defaults = new SettingsViewModel();
        Check(defaults.FfmpegPath == ffmpeg, "Playback still resolves the bundled executable using the runtime absolute path");
        var display = (string)typeof(SettingsViewModel).GetProperty("FfmpegDisplayPath")?.GetValue(defaults);
        Check(display != null && display.Contains("ffmpeg.exe") && !display.Contains(AppDomain.CurrentDomain.BaseDirectory), "Bundled FFmpeg display omits the installation directory");
        xml = new StringWriter(); new XmlSerializer(typeof(SettingsViewModel)).Serialize(xml, defaults);
        Check(!xml.ToString().Contains(AppDomain.CurrentDomain.BaseDirectory), "Default XML contains no bundled installation address");
        defaults.FfmpegPath = ffmpeg;
        xml = new StringWriter(); new XmlSerializer(typeof(SettingsViewModel)).Serialize(xml, defaults.Duplicate());
        Check(!xml.ToString().Contains(AppDomain.CurrentDomain.BaseDirectory), "Editor clone cannot materialize the resolved bundled path in exported XML");
        var custom = Path.Combine(root, "custom", "ffmpeg.exe"); defaults.FfmpegPath = custom; File.Delete(ffmpeg);
        Check(defaults.FfmpegPath == custom && (string)typeof(SettingsViewModel).GetProperty("FfmpegDisplayPath")?.GetValue(defaults) == custom, "User-selected override remains available when bundled FFmpeg is absent");
        xml = new StringWriter(); new XmlSerializer(typeof(SettingsViewModel)).Serialize(xml, defaults);
        Check(xml.ToString().Contains(custom), "Explicit custom override keeps its existing XML compatibility");
        var restored = (SettingsViewModel)new XmlSerializer(typeof(SettingsViewModel)).Deserialize(new StringReader(xml.ToString()));
        Check(restored.FfmpegPath == custom, "Existing custom FFmpeg paths survive deserialization");
        Console.WriteLine("Failures: " + failures); return failures == 0 ? 0 : 1;
    }
}
