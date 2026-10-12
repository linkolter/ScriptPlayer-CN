using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using ScriptPlayer.Library;
using ScriptPlayer.ViewModels;

internal static class RelatedFileRenameIntegrationTests
{
    private static int failures;
    [STAThread] public static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "ScriptPlayer-R03-Caller-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string profile=Environment.GetEnvironmentVariable("APPDATA");
            if(string.IsNullOrWhiteSpace(profile) || profile.IndexOf("caller-profile-",StringComparison.OrdinalIgnoreCase)<0)
                throw new InvalidOperationException("Run through the private-profile test runner; direct use of real APPDATA is forbidden.");
            ScriptPlayer.Localization.ChineseLocalization.Initialize();
            PublishAfterCommit(Path.Combine(root, "commit"));
            FailureState(Path.Combine(root, "failures"));
            ScriptOnlySelection(Path.Combine(root, "selection"));
            ReportFailure(Path.Combine(root, "report"));
            MainViewModelWiring(Path.Combine(root, "wiring"));
        }
        catch (Exception ex) { failures++; Console.WriteLine("ERROR: " + ex); }
        finally
        {
            string full = Path.GetFullPath(root);
            string temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("ScriptPlayer-R03-Caller-", StringComparison.Ordinal)) Directory.Delete(full, true);
            else { failures++; Console.WriteLine("ERROR: Unsafe fixture cleanup path refused"); }
        }
        Console.WriteLine("Failures: " + failures); return failures == 0 ? 0 : 1;
    }
    private static string Put(string root, string name, string bytes = "fixture")
    {
        Directory.CreateDirectory(root); string p = Path.Combine(root, name); File.WriteAllText(p, bytes); return p;
    }
    private static void Check(bool value, string label) { Console.WriteLine((value ? "PASS: " : "FAIL: ") + label); if (!value) failures++; }
    private static string Hash(string path) { using (var sha = SHA256.Create()) using (var stream = File.OpenRead(path)) return Convert.ToBase64String(sha.ComputeHash(stream)); }
    private static void PublishAfterCommit(string root)
    {
        string video = Put(root, "Movie.mp4"), script = Put(root, "Movie-soft.funscript", "{\"actions\":[]}"), axis = Put(root, "Movie-soft.twist.funscript");
        Put(root, "Movie.nfo", "<movie><title>保留</title><custom flag=\"x\">未知内容</custom></movie>");
        var before = Directory.GetFiles(root).ToDictionary(p => p, Hash);
        using (var playlist = new PlaylistViewModel())
        {
            var entry = new PlaylistEntry(video) { SelectedScriptName = Path.GetFileName(script), Duration = TimeSpan.FromSeconds(42) };
            var other = new PlaylistEntry(script) { SelectedScriptName = Path.GetFileName(script) };
            playlist.DeferLoading = true; playlist.Entries.Add(entry); playlist.Entries.Add(other);
            playlist.RequestScriptFileNames += (s,e) => { e.ValueOut = new[] { script }; e.Handled = true; };
            var files = new FaultFiles();
            files.BeforeMove = (n,s,t) => Check(entry.Fullname == video && other.Fullname == script && entry.SelectedScriptName == Path.GetFileName(script), "Entries retain old paths/selection during physical move " + n);
            playlist.RenameRelatedFiles = (p,n) => RelatedFileRename.Rename(p,n,new string[0],new string[0],files);
            playlist.RequestRenameFile += (s,e) => { e.Value = "New"; e.Handled = true; };
            playlist.RenameCommand.Execute(entry);
            var result = playlist.LastRenameResult;
            Check(result.Status == RelatedFileRenameStatus.Success, "Real RenameCommand uses shared transactional rename seam");
            Check(entry.Fullname == Path.Combine(root,"New.mp4") && other.Fullname == Path.Combine(root,"New-soft.funscript"), "All mapped entries publish paths after commit");
            Check(entry.SelectedScriptName == "New-soft.funscript" && other.SelectedScriptName == "New-soft.funscript", "Selected author survives Reset and reflects complete mapping");
            Check(result.Plan.All(m => File.Exists(m.TargetPath) && Hash(m.TargetPath) == before[m.OriginalPath]), "Caller preserves all video/script/axis/NFO bytes");
            var saved = JsonConvert.DeserializeObject<RelatedFileRenameResult>(File.ReadAllText(playlist.LastRenameReportPath));
            Check(saved.Status == result.Status && saved.Plan.Count == result.Plan.Count && saved.Plan.All(m => m.CurrentPath == m.TargetPath), "Actual Newtonsoft report retains full committed old/new/current map");
            Check(saved.Steps.Count == result.Steps.Count && saved.Errors.Count == 0, "Actual report retains physical steps and diagnostics");
            string selected = entry.SelectedScriptName; entry.Duration = TimeSpan.FromSeconds(42); int moves = files.MoveCalls;
            result = playlist.RenameEntry(entry, "New");
            Check(result.Status == RelatedFileRenameStatus.NoChange && files.MoveCalls == moves && entry.Duration == TimeSpan.FromSeconds(42) && entry.SelectedScriptName == selected, "No-op does not reset Entry or enqueue physical moves");
        }
    }
    private static void FailureState(string root)
    {
        foreach (string kind in new[] { "rejected", "rollback", "recovery" })
        {
            string directory = Path.Combine(root,kind); string video = Put(directory,"Movie.mp4"), script=Put(directory,"Movie.funscript");
            string videoHash=Hash(video), scriptHash=Hash(script);
            if(kind=="rejected") Put(directory,"New.mp4","occupied");
            using(var playlist=new PlaylistViewModel())
            {
                var entry=new PlaylistEntry(video) { SelectedScriptName="Movie.funscript", Duration=TimeSpan.FromSeconds(42) };
                var files=new FaultFiles();
                if(kind!="rejected") files.FailMoves.Add(2);
                if(kind=="recovery") files.FailMoves.Add(3);
                files.BeforeMove=(n,s,t)=>Check(entry.Fullname==video && entry.SelectedScriptName=="Movie.funscript",kind+" state remains old at move "+n);
                playlist.RenameRelatedFiles=(p,n)=>RelatedFileRename.Rename(p,n,new string[0],new string[0],files);
                var result=playlist.RenameEntry(entry,"New");
                var expected=kind=="rejected"?RelatedFileRenameStatus.Rejected:kind=="rollback"?RelatedFileRenameStatus.FailedRolledBack:RelatedFileRenameStatus.FailedNeedsRecovery;
                Check(result.Status==expected && entry.Fullname==video && entry.SelectedScriptName=="Movie.funscript" && entry.Duration==TimeSpan.FromSeconds(42),kind+" never publishes or resets Entry");
                var saved=JsonConvert.DeserializeObject<RelatedFileRenameResult>(File.ReadAllText(playlist.LastRenameReportPath));
                Check(saved.Status==result.Status && saved.Errors.Count==result.Errors.Count && saved.Plan.All(m=>File.Exists(m.CurrentPath)),kind+" saved diagnostics contain actual locations");
                if(kind=="recovery")
                {
                    saved=RelatedFileRename.Recover(saved);
                    Check(saved.Status==RelatedFileRenameStatus.Recovered && Hash(video)==videoHash && Hash(script)==scriptHash,"Caller-saved Newtonsoft manifest recovers failed rollback");
                    Check(entry.Fullname==video && entry.SelectedScriptName=="Movie.funscript","Recovery keeps originally unpublished Entry valid");
                }
                else Check(Hash(video)==videoHash && Hash(script)==scriptHash,kind+" original bytes remain intact");
            }
        }
    }
    private static void ScriptOnlySelection(string root)
    {
        string a=Path.Combine(root,"a"), b=Path.Combine(root,"b"), c=Path.Combine(root,"c");
        string script=Put(a,"Movie-soft.funscript","{\"actions\":[]}");
        string video=Put(a,"Movie.mp4"), outsideVideo=Put(b,"Movie.mp4"), outsideScript=Put(b,"Movie-soft.funscript","{\"actions\":[]}"), sharedVideo=Put(c,"Movie.mp4");
        using(var playlist=new PlaylistViewModel())
        {
            var anchor=new PlaylistEntry(script);
            var local=new PlaylistEntry(video) { SelectedScriptName="Movie-soft.funscript" };
            var outside=new PlaylistEntry(outsideVideo) { SelectedScriptName="Movie-soft.funscript" };
            var shared=new PlaylistEntry(sharedVideo) { SelectedScriptName="Movie-soft.funscript" };
            playlist.DeferLoading=true; playlist.Entries.Add(local); playlist.Entries.Add(outside); playlist.Entries.Add(shared);
            playlist.RequestScriptFileNames+=(s,e)=>{ e.ValueOut=e.ValueIn==outsideVideo?new[]{outsideScript,script}:new[]{script}; e.Handled=true; };
            var result=playlist.RenameEntry(anchor,"Other");
            Check(result.Status==RelatedFileRenameStatus.Success && local.Fullname==video && local.SelectedScriptName=="Other.funscript","Script-anchor rename updates same-directory video selection without renaming video");
            Check(shared.Fullname==sharedVideo && shared.SelectedScriptName=="Other.funscript","Script-anchor rename updates a resolved shared-directory script selection");
            Check(outside.Fullname==outsideVideo && outside.SelectedScriptName=="Movie-soft.funscript" && File.Exists(outsideScript),"Same-named unrenamed local script selection is not guessed from a global basename");
            Check(File.Exists(Path.Combine(a,"Other.funscript")) && !File.Exists(script),"Script anchor commits its own exact stem family only");
        }
    }
    private static void ReportFailure(string root)
    {
        string video=Put(root,"Movie.mp4"), script=Put(root,"Movie.funscript");
        string profile=Environment.GetEnvironmentVariable("APPDATA");
        string directory=Path.Combine(profile,"ScriptPlayer","RenameOperations");
        string held=directory+"-held";
        if(Directory.Exists(directory)) Directory.Move(directory,held);
        File.WriteAllText(directory,"private report blocker");
        try
        {
            using(var playlist=new PlaylistViewModel())
            {
                var entry=new PlaylistEntry(video);
                var result=playlist.RenameEntry(entry,"New");
                Check(result.Status==RelatedFileRenameStatus.Success && entry.Fullname==Path.Combine(root,"New.mp4"),"Report I/O failure does not falsify disk commit or leave Entry at an obsolete path");
                Check(result.Errors.Any(e=>e.Phase=="Report") && playlist.LastRenameReportPath==null && playlist.LastRenameResult.Plan.Count==2,"Report failure is explicit and complete mapping remains in memory");
                Check(File.ReadAllText(directory)=="private report blocker","Report writer never overwrites occupied data");
            }
        }
        finally { File.Delete(directory); if(Directory.Exists(held)) Directory.Move(held,directory); }
    }
    private static void MainViewModelWiring(string root)
    {
        string local=Path.Combine(root,"local"), additional=Path.Combine(root,"additional");
        string video=Put(local,"Movie.mp4"), script=Put(local,"Movie.funscript","{\"actions\":[]}"), remote=Put(additional,"Movie.funscript","{\"actions\":[]}");
        using(var vm=new MainViewModel())
        {
            vm.Settings.AdditionalPaths.Add(additional);
            var entry=new PlaylistEntry(video);
            string companion=Put(additional,"Movie.twist.funscript","{\"actions\":[]}");
            typeof(MainViewModel).GetField("_loadedScript",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(vm,script);
            var protectedAxis=vm.Playlist.RenameEntry(new PlaylistEntry(companion),"AxisNew");
            Check(protectedAxis.Status==RelatedFileRenameStatus.Rejected && File.Exists(companion),"Actual rename wiring protects loaded script companion candidates in additional directories");
            typeof(MainViewModel).GetField("_loadedScript",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(vm,null);
            vm.LoadedAudio=video; // A loaded path blocks rename even without an active playback engine.
            var result=vm.Playlist.RenameEntry(entry,"New");
            Check(result.Status==RelatedFileRenameStatus.Rejected && File.Exists(video) && File.Exists(remote),"Actual MainViewModel wiring supplies loaded paths and rejects paused/idle loaded file rename");
            vm.LoadedAudio=null;
            result=vm.Playlist.RenameEntry(entry,"New");
            Check(result.Status==RelatedFileRenameStatus.Success && result.Plan.Count==4 && File.Exists(Path.Combine(additional,"New.funscript")),"Actual MainViewModel wiring preserves same-named files across additional directories");
            string invalid=Path.Combine(root,"missing"); vm.Settings.AdditionalPaths.Add(invalid);
            result=vm.Playlist.RenameEntry(entry,"Again");
            Check(result.Status==RelatedFileRenameStatus.Rejected && entry.Fullname==Path.Combine(local,"New.mp4") && File.Exists(entry.Fullname),"Invalid configured discovery directory is reported before moves and state change");
        }
    }
    private sealed class FaultFiles:IRelatedFileRenameFileSystem
    {
        public int MoveCalls;
        public HashSet<int> FailMoves=new HashSet<int>();
        public Action<int,string,string> BeforeMove;
        public bool FileExists(string p)=>File.Exists(p);
        public bool DirectoryExists(string p)=>Directory.Exists(p);
        public IEnumerable<string> EnumerateFiles(string p)=>Directory.GetFiles(p);
        public void Move(string s,string t) { MoveCalls++; BeforeMove?.Invoke(MoveCalls,s,t); if(FailMoves.Contains(MoveCalls)) throw new IOException("Injected caller failure "+MoveCalls); File.Move(s,t); }
    }
}
