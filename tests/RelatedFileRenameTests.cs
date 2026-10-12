using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
#if !LEGACY_RED
using ScriptPlayer.Library;
#endif

internal static class RelatedFileRenameTests
{
    private static int failures;
    private static void Check(bool value, string label)
    {
        Console.WriteLine((value ? "PASS: " : "FAIL: ") + label);
        if (!value) failures++;
    }

    public static int Main()
    {
#if LEGACY_RED
        // Exact legacy GetNewName body, copied from PlaylistViewModel.cs:863-868.
        var originals = new[] { @"C:\private-fixture\Movie.funscript", @"C:\private-fixture\Movie-soft.funscript", @"C:\private-fixture\Movie.twist.funscript" };
        var targets = originals.Select(file => LegacyGetNewName(file, "Renamed")).ToArray();
        foreach (var target in targets) Console.WriteLine("LEGACY TARGET: " + target);
        Check(Path.GetFileName(targets[1]) == "Renamed-soft.funscript", "Legacy preserves author suffix");
        Check(Path.GetFileName(targets[2]) == "Renamed.twist.funscript", "Legacy preserves multi-axis suffix");
        Check(targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() == originals.Length, "Legacy destinations are unique");
#else
        Run("suffix and directories", SuffixAndDirectories);
        Run("validation and conflicts", ValidationAndConflicts);
        Run("no-op and case-only", NoopAndCase);
        Run("rollback and races", RollbackAndRaces);
        Run("recovery and serialization", RecoveryAndSerialization);
        Run("multi-file recovery preflight", MultiFileRecoveryPreflight);
        Run("case-only incomplete recovery", CaseOnlyRecovery);
        Run("selected author anchor", SelectedAuthorAnchor);
        Run("loaded same-directory families", LoadedFamilies);
#endif
        Console.WriteLine("Failures: " + failures);
        return failures == 0 ? 0 : 1;
    }

#if LEGACY_RED
    private static string LegacyGetNewName(string filePath, string newFileNameWithoutExtension)
    {
        string directory = Path.GetDirectoryName(filePath);
        string extension = Path.GetExtension(filePath);
        return Path.Combine(directory, newFileNameWithoutExtension + extension);
    }
#else
    private static void Run(string name, Action<string> test)
    {
        string root = Path.Combine(Path.GetTempPath(), "ScriptPlayer-R03-" + Guid.NewGuid().ToString("N"));
        root = Path.GetFullPath(root);
        Directory.CreateDirectory(root);
        try { test(root); }
        catch (Exception ex) { Check(false, name + " unexpected exception " + ex); }
        finally
        {
            string tempParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (!string.Equals(Path.GetDirectoryName(root).TrimEnd(Path.DirectorySeparatorChar), tempParent, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(root).StartsWith("ScriptPlayer-R03-", StringComparison.Ordinal))
                throw new InvalidOperationException("Private fixture cleanup target is outside its intended temporary parent.");
            Directory.Delete(root, true);
        }
    }

    private static string Put(string directory, string name)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "private R03 fixture: " + name + "\n" + Guid.NewGuid().ToString("N"));
        return path;
    }

    private static string Hash(string path)
    {
        using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path))
            return Convert.ToBase64String(algorithm.ComputeHash(stream));
    }

    private static Dictionary<string, string> Snapshot(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path.Substring(root.Length), Hash, StringComparer.Ordinal);
    }

    private static bool Matches(string root, Dictionary<string, string> before)
    {
        var after = Snapshot(root);
        return before.Count == after.Count && before.All(pair => after.ContainsKey(pair.Key) && after[pair.Key] == pair.Value);
    }

    private static void SuffixAndDirectories(string root)
    {
        var names = new[] { "Movie.mp4", "Movie.funscript", "Movie-soft.funscript", "Movie_Handy.funscript", "Movie-soft.twist.funscript", "Movie.pitch.funscript", "Movie.mp4.funscript", "Movie.mp4.thumbs", "Movie.nfo", "Movie.gif", "Movie.png" };
        var files = names.Select(name => Put(root, name)).ToArray();
        var untouched = new[] { Put(root, "Movie2.funscript"), Put(root, "MovieExtra.nfo") };
        string extra = Path.Combine(root, "extra");
        string duplicateName = Put(extra, "Movie-soft.funscript");
        var hashes = files.Concat(new[] { duplicateName }).ToDictionary(path => path, Hash);
        var ignoredHashes = untouched.ToDictionary(path => path, Hash);
        var result = RelatedFileRename.Rename(files[0], "Renamed", new[] { root + Path.DirectorySeparatorChar, extra, extra + Path.DirectorySeparatorChar }, new string[0]);
        Check(result.Status == RelatedFileRenameStatus.Success, "Suffix family commits successfully");
        Check(result.Plan.Count == files.Length + 1, "Same-name files in separate directories are retained and duplicate directories coalesced");
        Check(result.Plan.All(item => Path.GetFileName(item.TargetPath) == "Renamed" + Path.GetFileName(item.OriginalPath).Substring("Movie".Length)), "Complete suffixes including compound extensions survive");
        Check(result.Plan.Select(item => item.TargetPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == result.Plan.Count, "All destinations unique");
        Check(result.Plan.All(item => File.Exists(item.TargetPath) && item.CurrentPath == item.TargetPath && Hash(item.TargetPath) == hashes[item.OriginalPath] && !File.Exists(item.OriginalPath)), "Every committed mapping has unchanged bytes and correct current location");
        Check(ignoredHashes.All(pair => File.Exists(pair.Key) && Hash(pair.Key) == pair.Value), "Similar prefixes without a delimiter are untouched");
        Check(result.Errors.Count == 0 && result.Steps.Count == result.Plan.Count, "Successful ordinary family records every physical step");
    }

    private static void ValidationAndConflicts(string root)
    {
        string original = Put(root, "Movie.mp4");
        Put(root, "Movie.funscript");
        Put(root, "Movie-soft.funscript");
        var before = Snapshot(root);
        foreach (var name in new[] { "", " ", ".", "..", "../escape", "A\\B", "A:B", "A?B", "A*B", "A\"B", "A<B", "A>B", "A|B", "A\0B", "Name.", "Name ", "CON", "con.txt", "CON .txt", "PRN", "AUX", "NUL", "COM1", "LPT9", "COM\u00b9", "CONIN$", "CONOUT$" })
        {
            var rejected = RelatedFileRename.Rename(original, name, new string[0], new string[0]);
            Check(rejected.Status == RelatedFileRenameStatus.Rejected && rejected.Steps.Count == 0 && Matches(root, before), "Invalid name rejected without mutation: " + name.Replace('\0', '?'));
        }
        var missing = RelatedFileRename.Rename(Path.Combine(root, "Missing.mp4"), "New", new string[0], new string[0]);
        Check(missing.Status == RelatedFileRenameStatus.Rejected && missing.Steps.Count == 0, "Missing anchor rejected before moves");
        var missingDirectory = RelatedFileRename.Rename(original, "New", new[] { Path.Combine(root, "absent") }, new string[0]);
        Check(missingDirectory.Status == RelatedFileRenameStatus.Rejected && Matches(root, before), "Invalid search directory fails closed without mutation");
        var loaded = RelatedFileRename.Rename(original, "New", new string[0], new[] { Path.Combine(root, "MOVIE-SOFT.FUNSCRIPT") });
        Check(loaded.Status == RelatedFileRenameStatus.Rejected && loaded.Steps.Count == 0 && Matches(root, before), "Loaded author script blocks the entire family case-insensitively");
        string collision = Put(root, "New.mp4"); var collisionBefore = Snapshot(root);
        Check(RelatedFileRename.Rename(original, "New", new string[0], new string[0]).Status == RelatedFileRenameStatus.Rejected && Matches(root, collisionBefore), "Existing file destination is never overwritten");
        File.Delete(collision); Directory.CreateDirectory(collision);
        var directoryBefore = Snapshot(root);
        Check(RelatedFileRename.Rename(original, "New", new string[0], new string[0]).Status == RelatedFileRenameStatus.Rejected && Matches(root, directoryBefore) && Directory.Exists(collision), "Directory destination rejected before moving any family file");
        Directory.Delete(collision);
        var overlapBefore = Snapshot(root);
        Check(RelatedFileRename.Rename(original, "Movie-soft", new string[0], new string[0]).Status == RelatedFileRenameStatus.Rejected && Matches(root, overlapBefore), "Destination occupied by another family member is rejected without staging cycles");
        var adapter = new FaultFiles(); adapter.BeforeEnumerate = directory => { throw new UnauthorizedAccessException("private denied fixture"); };
        var denied = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        Check(denied.Status == RelatedFileRenameStatus.Rejected && denied.Errors.Count > 0 && adapter.MoveCalls == 0, "Enumeration error becomes structured preflight rejection");
        adapter = new FaultFiles(); adapter.AfterEnumerate = directory => { File.Delete(Path.Combine(root, "Movie-soft.funscript")); };
        var vanished = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        Check(vanished.Status == RelatedFileRenameStatus.Rejected && adapter.MoveCalls == 0 && File.Exists(original), "Discovered source vanishing before preflight causes zero moves");
    }

    private static void NoopAndCase(string root)
    {
        string original = Put(root, "Movie.mp4"); Put(root, "Movie-soft.funscript");
        var before = Snapshot(root); var adapter = new FaultFiles();
        var noop = RelatedFileRename.Rename(original, "Movie", new string[0], new[] { original }, adapter);
        Check(noop.Status == RelatedFileRenameStatus.NoChange && adapter.MoveCalls == 0 && Matches(root, before), "Exact no-op performs no moves even for a loaded anchor");
        var renamed = RelatedFileRename.Rename(original, "movie", new string[0], new string[0], adapter);
        Check(renamed.Status == RelatedFileRenameStatus.Success && renamed.Steps.Count == renamed.Plan.Count * 2, "Case-only uses two physical moves for each source");
        Check(Directory.GetFiles(root).All(path => Path.GetFileName(path).StartsWith("movie", StringComparison.Ordinal)), "Case-only changes actual directory-entry spelling");
        Check(renamed.Plan.All(item => item.CurrentPath == item.TargetPath), "Case-only returns complete final locations");
    }

    private static void RollbackAndRaces(string root)
    {
        string original = Put(root, "Movie.mp4"); Put(root, "Movie-soft.funscript"); Put(root, "Movie.nfo");
        var before = Snapshot(root); var adapter = new FaultFiles(); adapter.FailMoves.Add(2);
        var result = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        Check(result.Status == RelatedFileRenameStatus.FailedRolledBack && Matches(root, before), "Second move failure restores all original names and bytes");
        Check(result.Plan.All(item => item.CurrentPath == item.OriginalPath) && result.Errors.Any(item => item.Phase == "Execute"), "Rolled-back failure retains plan and explicit execution error");
        adapter = new FaultFiles(); adapter.BeforeMove = (call, source, target) => { if (call == 2) File.WriteAllText(target, "concurrent private target"); };
        result = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        var failed = result.Errors.First(item => item.Phase == "Execute");
        Check(result.Status == RelatedFileRenameStatus.FailedRolledBack && File.ReadAllText(failed.ToPath) == "concurrent private target", "Destination appearing after preflight is never overwritten");
        Check(before.All(pair => File.Exists(root + pair.Key) && Hash(root + pair.Key) == pair.Value), "Concurrent collision restores original family");
        File.Delete(failed.ToPath);
        adapter = new FaultFiles(); adapter.FailMoves.Add(2);
        result = RelatedFileRename.Rename(original, "movie", new string[0], new string[0], adapter);
        Check(result.Status == RelatedFileRenameStatus.FailedRolledBack && Matches(root, before), "Case-only second-step failure restores original casing and bytes");
    }

    private static void RecoveryAndSerialization(string root)
    {
        string original = Put(root, "Movie.mp4"); Put(root, "Movie-soft.funscript"); Put(root, "Movie.nfo"); Put(root, "Movie.gif");
        var before = Snapshot(root); var adapter = new FaultFiles();
        adapter.FailMoves.UnionWith(new[] { 4, 5 });
        var result = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        Check(result.Status == RelatedFileRenameStatus.FailedNeedsRecovery, "Rollback failure is distinguished from complete restoration");
        Check(result.Errors.Any(item => item.Phase == "Rollback") && result.Plan.Count(item => item.CurrentPath != item.OriginalPath) == 1, "Rollback continues restoring other independent files after an error");
        var remaining = result.Plan.Single(item => item.CurrentPath != item.OriginalPath);
        Check(File.Exists(remaining.CurrentPath) && !File.Exists(remaining.OriginalPath), "Diagnostic current location matches the remaining real file");
        var serializer = new JavaScriptSerializer(); string json = serializer.Serialize(result);
        result = serializer.Deserialize<RelatedFileRenameResult>(json);
        Check(result.Plan.Count == 4 && result.Steps.Count >= 3 && result.Errors.Count >= 2, "Manifest JSON round-trip preserves full plan, steps, and errors");
        remaining = result.Plan.Single(item => item.CurrentPath != item.OriginalPath);
        File.WriteAllText(remaining.OriginalPath, "occupied recovery destination");
        var recoveryFiles = new FaultFiles();
        result = RelatedFileRename.Recover(result, recoveryFiles);
        Check(result.Status == RelatedFileRenameStatus.RecoveryRejected && recoveryFiles.MoveCalls == 0 && File.ReadAllText(remaining.OriginalPath) == "occupied recovery destination", "Recovery preflights every destination and never overwrites concurrent files");
        File.Delete(remaining.OriginalPath);
        result = RelatedFileRename.Recover(result);
        Check(result.Status == RelatedFileRenameStatus.Recovered && Matches(root, before), "Serialized incomplete result can be safely retried to complete recovery");
        result = RelatedFileRename.Recover(result);
        Check(result.Status == RelatedFileRenameStatus.Recovered && Matches(root, before), "Recovery is idempotent after restoration");
        // A permanent second failure remains recoverable and accurately located.
        adapter = new FaultFiles(); adapter.FailMoves.UnionWith(new[] { 4, 5 });
        result = RelatedFileRename.Rename(original, "New", new string[0], new string[0], adapter);
        recoveryFiles = new FaultFiles(); recoveryFiles.FailMoves.Add(1);
        result = RelatedFileRename.Recover(result, recoveryFiles);
        Check(result.Status == RelatedFileRenameStatus.FailedNeedsRecovery && result.Plan.All(item => File.Exists(item.CurrentPath)), "Recovery move failure preserves accurate current locations for another retry");
        result = RelatedFileRename.Recover(result);
        Check(result.Status == RelatedFileRenameStatus.Recovered && Matches(root, before), "Recovery retries after another move failure");
    }

    private static void SelectedAuthorAnchor(string root)
    {
        string anchor = Put(root, "Movie-soft.funscript"); string companion = Put(root, "Movie-soft.twist.funscript"); string video = Put(root, "Movie.mp4");
        string videoHash = Hash(video);
        var result = RelatedFileRename.Rename(anchor, "OtherAuthor", new string[0], new string[0]);
        Check(result.Status == RelatedFileRenameStatus.Success && result.Plan.Count == 2, "Script anchor retains its exact common stem without guessing author separators");
        Check(File.Exists(Path.Combine(root, "OtherAuthor.twist.funscript")) && Hash(video) == videoHash, "Selected author companion renamed while parent video remains unchanged");
        Check(result.Plan.Single(item => item.OriginalPath == companion).TargetPath == Path.Combine(root, "OtherAuthor.twist.funscript"), "Complete mapping enables caller to preserve selected author filename");
    }

    private static void LoadedFamilies(string root)
    {
        string main = Put(root, "Movie-soft.funscript");
        string axis = Put(root, "Movie-soft.twist.funscript");
        var before = Snapshot(root); var files = new FaultFiles();
        var result = RelatedFileRename.Rename(axis, "OtherAxis", new string[0], new[] { main.ToUpperInvariant() }, files);
        Check(result.Status == RelatedFileRenameStatus.Rejected && files.MoveCalls == 0 && Matches(root, before), "Loaded main script protects a separately selected same-directory multi-axis companion");
        string video = Put(root, "Movie.mp4");
        before = Snapshot(root); files = new FaultFiles();
        result = RelatedFileRename.Rename(main, "OtherAuthor", new string[0], new[] { video }, files);
        Check(result.Status == RelatedFileRenameStatus.Rejected && files.MoveCalls == 0 && Matches(root, before), "Loaded video protects separately selected same-directory author variants");
        string underscore = Put(root, "Movie_Handy.funscript");
        before = Snapshot(root); files = new FaultFiles();
        result = RelatedFileRename.Rename(underscore, "OtherHandy", new string[0], new[] { video }, files);
        Check(result.Status == RelatedFileRenameStatus.Rejected && files.MoveCalls == 0 && Matches(root, before), "Loaded family protection includes underscore author boundaries");
        string additional = Path.Combine(root, "other-directory");
        string otherAxis = Put(additional, "Movie-soft.twist.funscript");
        result = RelatedFileRename.Rename(otherAxis, "IndependentAxis", new string[0], new[] { main });
        Check(result.Status == RelatedFileRenameStatus.Success && File.Exists(Path.Combine(additional, "IndependentAxis.funscript")) && File.Exists(main) && File.Exists(axis), "Same-name companion in another directory is not falsely protected");
        string unrelated = Put(root, "Movie2.funscript");
        result = RelatedFileRename.Rename(unrelated, "IndependentMovie", new string[0], new[] { video });
        Check(result.Status == RelatedFileRenameStatus.Success && File.Exists(Path.Combine(root, "IndependentMovie.funscript")), "Loaded family matching requires a complete delimiter boundary");
    }

    private static void MultiFileRecoveryPreflight(string root)
    {
        string original = Put(root, "Movie.mp4"); Put(root, "Movie.funscript"); Put(root, "Movie.nfo"); Put(root, "Movie.gif");
        var before = Snapshot(root); var files = new FaultFiles(); files.FailMoves.UnionWith(new[] { 4, 5, 6 });
        var result = RelatedFileRename.Rename(original, "New", new string[0], new string[0], files);
        var pending = result.Plan.Where(item => item.CurrentPath != item.OriginalPath).ToList();
        Check(result.Status == RelatedFileRenameStatus.FailedNeedsRecovery && pending.Count == 2, "Multiple failed rollback moves remain independently tracked");
        // Occupy a later restoration destination; an earlier free destination must not be moved.
        var blocked = pending.First(); File.WriteAllText(blocked.OriginalPath, "private recovery collision");
        var observed = Snapshot(root); files = new FaultFiles();
        result = RelatedFileRename.Recover(result, files);
        Check(result.Status == RelatedFileRenameStatus.RecoveryRejected && files.MoveCalls == 0 && Matches(root, observed), "Recovery inspects the complete plan before its first move");
        File.Delete(blocked.OriginalPath);
        string missingPath = pending.Last().CurrentPath; string missingBytes = File.ReadAllText(missingPath); File.Delete(missingPath);
        files = new FaultFiles(); result = RelatedFileRename.Recover(result, files);
        Check(result.Status == RelatedFileRenameStatus.RecoveryRejected && files.MoveCalls == 0, "Missing recovery source prevents all restoration moves");
        File.WriteAllText(missingPath, missingBytes);
        files = new FaultFiles(); files.BeforeMove = (call, source, target) => { if (call == 1) File.WriteAllText(target, "raced restoration destination"); };
        result = RelatedFileRename.Recover(result, files);
        var raced = result.Errors.Last(item => item.Phase == "Recovery");
        Check(result.Status == RelatedFileRenameStatus.FailedNeedsRecovery && File.ReadAllText(raced.ToPath) == "raced restoration destination", "Recovery never overwrites a destination created after preflight");
        Check(result.Plan.Count(item => item.CurrentPath != item.OriginalPath) == 1, "Recovery continues other independent files after a concurrent collision");
        File.Delete(raced.ToPath); result = RelatedFileRename.Recover(result);
        Check(result.Status == RelatedFileRenameStatus.Recovered && Matches(root, before), "Multi-file recovery eventually restores original content and names");
        // A successful result is not an implicit undo command.
        var success = RelatedFileRename.Rename(original, "New", new string[0], new string[0]);
        var successfulTree = Snapshot(root); files = new FaultFiles();
        var rejected = RelatedFileRename.Recover(success, files);
        Check(rejected.Status == RelatedFileRenameStatus.RecoveryRejected && files.MoveCalls == 0 && Matches(root, successfulTree), "Recovery rejects a successful forward operation");
        rejected = RelatedFileRename.Recover(rejected, files);
        Check(rejected.Status == RelatedFileRenameStatus.RecoveryRejected && files.MoveCalls == 0 && Matches(root, successfulTree), "Repeated invalid recovery cannot turn success into an undo");
    }

    private static void CaseOnlyRecovery(string root)
    {
        string original = Put(root, "Movie.mp4"); Put(root, "Movie.funscript");
        var before = Snapshot(root); var files = new FaultFiles(); files.FailMoves.UnionWith(new[] { 4, 5, 6 });
        var result = RelatedFileRename.Rename(original, "movie", new string[0], new string[0], files);
        Check(result.Status == RelatedFileRenameStatus.FailedNeedsRecovery && result.Plan.Count(item => item.CurrentPath != item.OriginalPath) == 2, "Case-only failure retains both final and temporary remaining locations");
        Check(result.Plan.All(item => File.Exists(item.CurrentPath)), "Case-only rollback errors do not hide temporary files from diagnostics");
        result = new JavaScriptSerializer().Deserialize<RelatedFileRenameResult>(new JavaScriptSerializer().Serialize(result));
        result = RelatedFileRename.Recover(result);
        Check(result.Status == RelatedFileRenameStatus.Recovered && Matches(root, before), "Serialized case-only recovery restores original spelling and bytes");
        Check(!Directory.GetFiles(root).Any(path => Path.GetFileName(path).StartsWith(".scriptplayer-rename-", StringComparison.Ordinal)), "Completed case-only recovery leaves no temporary files");
    }

    private sealed class FaultFiles : IRelatedFileRenameFileSystem
    {
        public int MoveCalls;
        public readonly HashSet<int> FailMoves = new HashSet<int>();
        public Action<int, string, string> BeforeMove;
        public Action<string> BeforeEnumerate;
        public Action<string> AfterEnumerate;
        public bool FileExists(string path) { return File.Exists(path); }
        public bool DirectoryExists(string path) { return Directory.Exists(path); }
        public IEnumerable<string> EnumerateFiles(string directory)
        {
            if (BeforeEnumerate != null) BeforeEnumerate(directory);
            var files = Directory.GetFiles(directory);
            if (AfterEnumerate != null) AfterEnumerate(directory);
            return files;
        }
        public void Move(string source, string target)
        {
            MoveCalls++;
            if (BeforeMove != null) BeforeMove(MoveCalls, source, target);
            if (FailMoves.Contains(MoveCalls)) throw new IOException("Injected private move failure " + MoveCalls);
            File.Move(source, target);
        }
    }
#endif
}
