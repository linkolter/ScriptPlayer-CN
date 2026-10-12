using System.Diagnostics;
using System.Reflection;

namespace ScriptPlayer.ViewModels
{
    /// <summary>
    /// Local edition information only. The Chinese edition deliberately does not
    /// contact GitHub or perform automatic/manual update checks.
    /// </summary>
    public class VersionViewModel
    {
        public string InstalledVersion { get; private set; }

        public VersionViewModel()
        {
            InstalledVersion = GetVersion();
        }

        private static string GetVersion()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrWhiteSpace(location))
                return "1.0.11";

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(location);
            return string.IsNullOrWhiteSpace(info.FileVersion) ? "1.0.11" : info.FileVersion;
        }
    }
}
