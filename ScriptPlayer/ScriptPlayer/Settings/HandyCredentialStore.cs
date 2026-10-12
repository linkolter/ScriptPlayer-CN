using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ScriptPlayer.Settings
{
    // User credentials are separate from exportable settings and protected by this Windows user.
    public static class HandyCredentialStore
    {
        public static string FilePath(string settingsFile) => Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(settingsFile)), "Private", "HandyConnection.dpapi");

        public static string Load(string settingsFile)
        {
            var path = FilePath(settingsFile);
            if (!File.Exists(path)) return null;
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }

        public static void Save(string settingsFile, string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key ?? string.Empty);
            try
            {
                var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
                WriteAtomically(FilePath(settingsFile), stream => stream.Write(encrypted, 0, encrypted.Length));
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }

        internal static void WriteAtomically(string filename, Action<FileStream> write)
        {
            var path = Path.GetFullPath(filename);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + ".new-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { write(stream); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
