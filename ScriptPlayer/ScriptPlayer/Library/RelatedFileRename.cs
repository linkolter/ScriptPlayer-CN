using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScriptPlayer.Library
{
    public enum RelatedFileRenameStatus
    {
        NoChange, Rejected, Success, FailedRolledBack, FailedNeedsRecovery, RecoveryRejected, Recovered
    }

    public sealed class RelatedFileRenameMapping
    {
        public string OriginalPath { get; set; }
        public string TargetPath { get; set; }
        public string CurrentPath { get; set; }
    }

    public sealed class RelatedFileRenameStep
    {
        public string OriginalPath { get; set; }
        public string FromPath { get; set; }
        public string ToPath { get; set; }
        public string Phase { get; set; }
    }

    public sealed class RelatedFileRenameError
    {
        public string Phase { get; set; }
        public string FromPath { get; set; }
        public string ToPath { get; set; }
        public string Message { get; set; }
    }

    // Plain public data so a caller can save a local diagnostic manifest without WPF dependencies.
    // The module itself does not durably journal moves: a process crash may precede manifest saving.
    public sealed class RelatedFileRenameResult
    {
        public RelatedFileRenameResult()
        {
            Plan = new List<RelatedFileRenameMapping>();
            Steps = new List<RelatedFileRenameStep>();
            Errors = new List<RelatedFileRenameError>();
        }
        public RelatedFileRenameStatus Status { get; set; }
        public string OriginalPath { get; set; }
        public string NewStem { get; set; }
        public bool RecoveryAllowed { get; set; }
        public List<RelatedFileRenameMapping> Plan { get; set; }
        public List<RelatedFileRenameStep> Steps { get; set; }
        public List<RelatedFileRenameError> Errors { get; set; }
    }

    public interface IRelatedFileRenameFileSystem
    {
        bool FileExists(string path);
        bool DirectoryExists(string path);
        IEnumerable<string> EnumerateFiles(string directory);
        // Must fail if the destination exists. An adapter must never replace a file.
        void Move(string source, string target);
    }

    public static class RelatedFileRename
    {
        private static readonly StringComparer Paths = StringComparer.OrdinalIgnoreCase;
        private sealed class DiskFiles : IRelatedFileRenameFileSystem
        {
            public bool FileExists(string path) { return File.Exists(path); }
            public bool DirectoryExists(string path) { return Directory.Exists(path); }
            public IEnumerable<string> EnumerateFiles(string directory) { return Directory.EnumerateFiles(directory); }
            public void Move(string source, string target) { File.Move(source, target); }
        }

        public static RelatedFileRenameResult Rename(string originalPath, string newStem,
            IEnumerable<string> searchDirectories, IEnumerable<string> loadedPaths)
        {
            return Rename(originalPath, newStem, searchDirectories, loadedPaths, new DiskFiles());
        }

        public static RelatedFileRenameResult Rename(string originalPath, string newStem,
            IEnumerable<string> searchDirectories, IEnumerable<string> loadedPaths, IRelatedFileRenameFileSystem files)
        {
            var result = new RelatedFileRenameResult { Status = RelatedFileRenameStatus.Rejected, OriginalPath = originalPath, NewStem = newStem };
            try
            {
                if (files == null) throw new ArgumentNullException("files");
                ValidateStem(newStem);
                string anchor = FullPath(originalPath);
                result.OriginalPath = anchor;
                if (!files.FileExists(anchor)) throw new IOException("The original file does not exist: " + anchor);
                string oldStem = Path.GetFileNameWithoutExtension(anchor);
                if (string.IsNullOrEmpty(oldStem)) throw new ArgumentException("The original file has no common stem.");
                var directories = new List<string> { Path.GetDirectoryName(anchor) };
                foreach (string directory in searchDirectories ?? Enumerable.Empty<string>()) directories.Add(DirectoryPath(directory));
                var discovered = new HashSet<string>(Paths);
                foreach (string directory in directories.Distinct(Paths))
                {
                    if (!files.DirectoryExists(directory)) throw new DirectoryNotFoundException("Search directory does not exist: " + directory);
                    foreach (string candidate in files.EnumerateFiles(directory))
                    {
                        string source = FullPath(candidate);
                        if (!Paths.Equals(Path.GetDirectoryName(source), directory)) throw new IOException("Enumeration returned a file outside its directory: " + source);
                        string filename = Path.GetFileName(source);
                        if (filename.Length <= oldStem.Length || !filename.StartsWith(oldStem, StringComparison.OrdinalIgnoreCase)) continue;
                        char delimiter = filename[oldStem.Length];
                        if (delimiter != '.' && delimiter != '-' && delimiter != '_') continue;
                        if (!discovered.Add(source)) continue;
                        string target = FullPath(Path.Combine(directory, newStem + filename.Substring(oldStem.Length)));
                        ValidateStem(Path.GetFileName(target));
                        result.Plan.Add(new RelatedFileRenameMapping { OriginalPath = source, TargetPath = target, CurrentPath = source });
                    }
                }
                if (!discovered.Contains(anchor)) throw new IOException("The original file was not present in the discovered family: " + anchor);
                result.Plan = result.Plan.OrderBy(item => item.OriginalPath, Paths).ToList();
                ValidatePlan(result, files, false);
                if (result.Plan.All(item => string.Equals(item.OriginalPath, item.TargetPath, StringComparison.Ordinal)))
                {
                    result.Status = RelatedFileRenameStatus.NoChange;
                    return result;
                }
                var loaded = new HashSet<string>((loadedPaths ?? Enumerable.Empty<string>())
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Select(FullPath), Paths);
                foreach (var item in result.Plan)
                    if (loaded.Any(path => ProtectsLoadedFamily(path, item.OriginalPath)))
                        throw new IOException("A related file or its same-directory family is currently loaded; switch away before renaming: " + item.OriginalPath);
            }
            catch (Exception ex)
            {
                Error(result, "Preflight", originalPath, null, ex);
                return result;
            }

            try
            {
                foreach (var item in result.Plan) MoveTo(result, item, item.TargetPath, "Execute", files);
                result.Status = RelatedFileRenameStatus.Success;
            }
            catch (Exception ex)
            {
                // MoveTo reports the exact failed physical step before propagating it.
                if (!result.Errors.Any(item => item.Phase == "Execute")) Error(result, "Execute", null, null, ex);
                Rollback(result, files);
                bool restored = AreAtOriginalLocations(result, files);
                result.Status = restored ? RelatedFileRenameStatus.FailedRolledBack : RelatedFileRenameStatus.FailedNeedsRecovery;
                result.RecoveryAllowed = !restored;
            }
            return result;
        }

        public static RelatedFileRenameResult Recover(RelatedFileRenameResult result)
        {
            return Recover(result, new DiskFiles());
        }

        // Only restores locations from an incomplete operation; it never retries forward renaming.
        // Callers must use a trusted saved result. This is not an external file-identity protocol.
        public static RelatedFileRenameResult Recover(RelatedFileRenameResult result, IRelatedFileRenameFileSystem files)
        {
            if (result == null) throw new ArgumentNullException("result");
            if (result.Errors == null) result.Errors = new List<RelatedFileRenameError>();
            if (result.Steps == null) result.Steps = new List<RelatedFileRenameStep>();
            try
            {
                if (files == null) throw new ArgumentNullException("files");
                if (!result.RecoveryAllowed) throw new InvalidOperationException("This result is not an incomplete recovery operation.");
                ValidatePlan(result, files, true);
            }
            catch (Exception ex)
            {
                Error(result, "RecoveryPreflight", null, null, ex);
                result.Status = RelatedFileRenameStatus.RecoveryRejected;
                return result;
            }

            bool failed = false;
            foreach (var item in result.Plan.AsEnumerable().Reverse())
            {
                try { MoveTo(result, item, item.OriginalPath, "Recovery", files); }
                catch { failed = true; } // Continue independently recoverable family members.
            }
            result.Status = failed || !AreAtOriginalLocations(result, files) ? RelatedFileRenameStatus.FailedNeedsRecovery : RelatedFileRenameStatus.Recovered;
            return result;
        }

        private static void ValidatePlan(RelatedFileRenameResult result, IRelatedFileRenameFileSystem files, bool recovery)
        {
            if (result.Plan == null || result.Plan.Count == 0) throw new InvalidOperationException("The file plan is empty.");
            var originals = new HashSet<string>(Paths);
            var targets = new HashSet<string>(Paths);
            var current = new HashSet<string>(Paths);
            foreach (var item in result.Plan)
            {
                if (item == null) throw new InvalidOperationException("The file plan contains an empty mapping.");
                RequireFullPath(item.OriginalPath); RequireFullPath(item.TargetPath); RequireFullPath(item.CurrentPath);
                string directory = Path.GetDirectoryName(item.OriginalPath);
                if (!Paths.Equals(directory, Path.GetDirectoryName(item.TargetPath)) || !Paths.Equals(directory, Path.GetDirectoryName(item.CurrentPath)))
                    throw new InvalidOperationException("Renaming and recovery must remain in the original directory.");
                if (!originals.Add(item.OriginalPath) || !targets.Add(item.TargetPath) || !current.Add(item.CurrentPath))
                    throw new InvalidOperationException("The plan contains duplicate file locations.");
                if (!files.DirectoryExists(directory)) throw new DirectoryNotFoundException("Parent directory does not exist: " + directory);
                string source = recovery ? item.CurrentPath : item.OriginalPath;
                string destination = recovery ? item.OriginalPath : item.TargetPath;
                if (!files.FileExists(source)) throw new IOException("Source file does not exist: " + source);
                if (files.DirectoryExists(destination)) throw new IOException("A directory occupies the destination: " + destination);
                if (!Paths.Equals(source, destination) && files.FileExists(destination))
                    throw new IOException("A file occupies the destination: " + destination);
            }
        }

        private static void MoveTo(RelatedFileRenameResult result, RelatedFileRenameMapping item, string target, string phase, IRelatedFileRenameFileSystem files)
        {
            if (string.Equals(item.CurrentPath, target, StringComparison.Ordinal)) return;
            // Case-insensitive filesystems require a distinct temporary directory entry.
            if (Paths.Equals(item.CurrentPath, target))
                MoveOne(result, item, TemporaryPath(item.CurrentPath, files), phase, files);
            MoveOne(result, item, target, phase, files);
        }

        private static void MoveOne(RelatedFileRenameResult result, RelatedFileRenameMapping item, string target, string phase, IRelatedFileRenameFileSystem files)
        {
            string source = item.CurrentPath;
            try
            {
                files.Move(source, target);
                item.CurrentPath = target;
                result.Steps.Add(new RelatedFileRenameStep { OriginalPath = item.OriginalPath, FromPath = source, ToPath = target, Phase = phase });
            }
            catch (Exception ex)
            {
                Error(result, phase, source, target, ex);
                throw;
            }
        }

        private static void Rollback(RelatedFileRenameResult result, IRelatedFileRenameFileSystem files)
        {
            var forward = result.Steps.Where(step => step.Phase == "Execute").Reverse().ToList();
            foreach (var step in forward)
            {
                var item = result.Plan.Single(mapping => Paths.Equals(mapping.OriginalPath, step.OriginalPath));
                if (!string.Equals(item.CurrentPath, step.ToPath, StringComparison.Ordinal)) continue;
                try { MoveOne(result, item, step.FromPath, "Rollback", files); }
                catch { } // Retain exact locations and continue other independent moves.
            }
        }

        private static string TemporaryPath(string source, IRelatedFileRenameFileSystem files)
        {
            string directory = Path.GetDirectoryName(source);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string path = Path.Combine(directory, ".scriptplayer-rename-" + Guid.NewGuid().ToString("N") + ".tmp");
                if (!files.FileExists(path) && !files.DirectoryExists(path)) return path;
            }
            throw new IOException("Could not obtain an unused temporary rename path.");
        }

        private static string FullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("The file or directory path is empty.");
            return Path.GetFullPath(path);
        }

        private static string DirectoryPath(string path)
        {
            string full = FullPath(path);
            string root = Path.GetPathRoot(full);
            return full.Length > root.Length ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : full;
        }

        private static bool ProtectsLoadedFamily(string loadedPath, string candidate)
        {
            if (Paths.Equals(loadedPath, candidate)) return true;
            if (!Paths.Equals(Path.GetDirectoryName(loadedPath), Path.GetDirectoryName(candidate))) return false;
            string stem = Path.GetFileNameWithoutExtension(loadedPath);
            string name = Path.GetFileName(candidate);
            if (string.IsNullOrEmpty(stem) || name.Length <= stem.Length || !name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) return false;
            char boundary = name[stem.Length];
            return boundary == '.' || boundary == '-' || boundary == '_';
        }

        private static bool AreAtOriginalLocations(RelatedFileRenameResult result, IRelatedFileRenameFileSystem files)
        {
            bool restored = true;
            foreach (var item in result.Plan)
            {
                if (!string.Equals(item.CurrentPath, item.OriginalPath, StringComparison.Ordinal)) { restored = false; continue; }
                try
                {
                    if (!files.FileExists(item.OriginalPath)) throw new IOException("The original location is missing after restoration.");
                }
                catch (Exception ex) { restored = false; Error(result, "Verification", item.CurrentPath, item.OriginalPath, ex); }
            }
            return restored;
        }

        private static void RequireFullPath(string path)
        {
            if (!Path.IsPathRooted(path) || !string.Equals(FullPath(path), path, StringComparison.Ordinal))
                throw new InvalidOperationException("The manifest must contain normalized absolute paths.");
        }

        private static void ValidateStem(string stem)
        {
            if (string.IsNullOrWhiteSpace(stem) || stem == "." || stem == ".." || stem.EndsWith(".", StringComparison.Ordinal) || stem.EndsWith(" ", StringComparison.Ordinal)
                || stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("The new name is not a valid Windows filename stem.");
            string device = stem.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
            if (device == "CON" || device == "PRN" || device == "AUX" || device == "NUL" || device == "CONIN$" || device == "CONOUT$"
                || ((device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal))
                    && device.Length == 4 && "123456789\u00b9\u00b2\u00b3".IndexOf(device[3]) >= 0))
                throw new ArgumentException("The new name uses a reserved Windows device name.");
        }

        private static void Error(RelatedFileRenameResult result, string phase, string source, string target, Exception error)
        {
            result.Errors.Add(new RelatedFileRenameError { Phase = phase, FromPath = source, ToPath = target, Message = error.GetType().Name + ": " + error.Message });
        }
    }
}
