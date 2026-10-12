using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using ScriptPlayer.Metadata;

// All file I/O stays under the runner-created sibling fixtures directory. No APPDATA, WPF or media loading.
internal static class NfoMetadataTests
{
    private static string fixtures;
    private static int passed, failed, sequence;
    private const string RichXml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
        + "<?fixture preserve?>\n<!--document comment--><movie owner=\"private\" xmlns:x=\"urn:r02-test\">"
        + "<title>Original title</title><originaltitle>Original source title</originaltitle>"
        + "<source>https://example.invalid/topic</source><type>video</type><plot>Original plot</plot>"
        + "<favorite>false</favorite><tag>Alpha</tag><tag>Beta</tag>"
        + "<!--root comment--><rating source=\"fixture\">7.5</rating>"
        + "<x:data x:revision=\"4\"><x:value>private extension</x:value></x:data>"
        + "<scripts custom=\"keep-container\"><script file=\"Movie-soft.twist.funscript\" author=\"private-author\">"
        + "<duration>00:01:00</duration><durationms>60000</durationms><actions>12</actions>"
        + "<maxspeed>80</maxspeed><avgspeed>40</avgspeed><unknown unit=\"private\">retain</unknown>"
        + "<!--script comment--><x:axis channel=\"R0\"/></script></scripts></movie>";

    public static int Main(string[] args)
    {
        string run = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        fixtures = Path.GetFullPath(Path.Combine(run, "fixtures"));
        if (args.Length != 1 || !string.Equals(Path.GetFullPath(args[0]), fixtures, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(fixtures, ".scriptplayer-r02-private-fixture"))
            || File.ReadAllText(Path.Combine(fixtures, ".scriptplayer-r02-private-fixture")) != "R02 private test fixtures")
        {
            Console.Error.WriteLine("ERROR: Refusing file I/O outside the runner-owned private fixture tree.");
            return 2;
        }
        Console.WriteLine("Fixture tree: " + fixtures);
        Run("Existing known metadata and script statistics remain readable", ReadCompatibility);
        Run("Unknown root and element attributes survive Save", UnknownAttributes);
        Run("Unknown elements survive Save", UnknownElements);
        Run("Comments and processing instructions survive Save", CommentsAndInstructions);
        Run("Namespaced extension elements and attributes survive Save", NamespacedData);
        Run("Favorite-only update leaves all other XML metadata unchanged", FavoriteOnly);
        Run("Favorite-only update preserves known field values", FavoriteKnownFields);
        Run("Title and tag edits save successfully", TitleAndTags);
        Run("Title and tag edits preserve script extension fields", ScriptUnknownFields);
        Run("Title edit preserves original title and unrelated fields", OriginalTitle);
        Run("No-op Save preserves a distinct original title", OriginalTitleNoop);
        Run("Existing document root is retained", RootName);
        Run("Malformed XML cannot be silently replaced", Malformed);
        Run("Legacy plaintext cannot be silently converted by Save", LegacyPlaintext);
        Run("An externally revised sidecar cannot be overwritten by a stale load", ExternalRevision);
        Run("Same-size same-timestamp external revision is detected", SameSizeRevision);
        Run("Externally deleted sidecar cannot be recreated from a stale load", ExternalDeletion);
        Run("New metadata cannot overwrite a newly appeared target", AppearedTarget);
        Run("Locked target rejects Save and retains original bytes", Locked);
        Run("Readonly target rejects Save and retains original bytes", Readonly);
        Run("Serialization failure retains complete original bytes", SerializationFailure);
        Run("Directory at target remains intact and Save rejects", TargetDirectory);
        Run("Missing destination directory is rejected without creating metadata", MissingDirectory);
        Run("Hyphen script variant uses the existing shared NFO", SharedVariant);
        Run("Loaded shared sidecar does not rebind to a newly appeared exact sidecar", LoadedPath);
        Run("Exact variant sidecar still takes priority", ExactVariant);
        Run("UTF16 non-ASCII metadata survives a normal round trip", UnicodeEncoding);
        Run("Successful first Save remains readable", NewDocument);
#if NFO_COMMIT_SEAM
        Run("No-op Save keeps exact bytes and creates no commit artifacts", Noop);
        Run("Favorite aliases retain attributes and vendor names are untouched", Aliases);
        Run("Nested vendor title and tags never become root metadata", NestedVendor);
        Run("CopyForEdit isolates failed drafts from shared metadata", CopyIsolation);
        Run("Cloned equal script statistics do not cause dirty rewriting", ScriptValueSnapshot);
        Run("Unsupported dirty script edits explicitly reject without data loss", DirtyScripts);
        Run("Mixed-content edited fields reject without data loss", MixedContent);
        Run("Temporary stream write failure preserves original bytes and retains unverifiable partial stage", TempWriteFailure);
        Run("Temporary flush failure preserves original bytes", FlushFailure);
        Run("Replace refusal preserves original bytes and cleans reservations", ReplaceRefusal);
        Run("Replace 1177 target-moved failure restores original bytes", ReplaceMovedFailure);
        Run("Recovery failure reports actual backup and temporary locations", RecoveryFailure);
        Run("Replace exception after a verified completed commit stays successful", CompletedReplaceException);
        Run("Backup cleanup failure stays committed and exposes a warning", CleanupFailure);
        Run("Narrow external race preserves displaced bytes and reports recovery required", ReplaceExternalRace);
        Run("External replacement after Replace is never overwritten by rollback", ExternalAfterReplace);
        Run("A target appearing during new-file Move is not overwritten", NewMoveCollision);
        Run("Identical appeared target cannot be mistaken for our successful Move", NewMoveIdenticalCollision);
        Run("Tag insertion and reordering keep attributes attached to their original values", AttributedTags);
        Run("Consumed temporary path recreated externally is retained", ConsumedTemporary);
        Run("Temporary file modified externally on failure is retained", ModifiedTemporary);
        Run("Two stale in-process commits serialize and the second reports conflict", ConcurrentCommits);
#endif
        Console.WriteLine("Passed: " + passed);
        Console.WriteLine("Failures: " + failed);
        Console.WriteLine("Suite boundary: actual WPF shared Entry publication is exercised by NfoMetadataIntegrationTests.");
        return failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL: " + name + " | " + ex.GetType().Name + ": " + ex.Message); }
    }
    private static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static string NewMedia(string xml = RichXml, Encoding encoding = null)
    {
        string directory = Path.Combine(fixtures, (++sequence).ToString("000"));
        Directory.CreateDirectory(directory);
        string media = Path.Combine(directory, "Movie.mp4");
        File.WriteAllText(media, "private fixture; not playable media");
        if (xml != null) File.WriteAllText(Path.ChangeExtension(media, ".nfo"), xml, encoding ?? new UTF8Encoding(false));
        return media;
    }
    private static XmlDocument Document(string path)
    {
        var document = new XmlDocument { PreserveWhitespace = false, XmlResolver = null };
        document.Load(path);
        return document;
    }
    private static NfoMetadata Loaded(string media)
    {
        var metadata = NfoMetadata.Load(media);
        Assert(metadata != null, "Load unexpectedly returned null");
        return metadata;
    }
    private static string Sidecar(string media) { return Path.ChangeExtension(media, ".nfo"); }
    private static void SaveFavorite(string media)
    {
        var metadata = Loaded(media);
        metadata.Favorite = true;
        Assert(metadata.Save(media) == NfoMetadata.GetSidecarPath(media), "Save returned the wrong sidecar path");
    }
    private static bool Blocked(Action action)
    {
        try { action(); return false; }
        catch (Exception) { return true; }
    }
    private static void BytesEqual(byte[] expected, string path, string reason)
    {
        Assert(File.Exists(path) && expected.SequenceEqual(File.ReadAllBytes(path)), reason);
    }
    private static void ReadCompatibility()
    {
        var m = Loaded(NewMedia());
        Assert(m.Title == "Original title" && m.OriginalTitle == "Original source title" && !m.Favorite, "Known fields differ");
        Assert(m.Tags.SequenceEqual(new[] { "Alpha", "Beta" }), "Tags differ");
        var s = m.FindScript("Movie-soft.twist.funscript");
        Assert(s != null && s.ActionCount == 12 && s.DurationMilliseconds == 60000 && s.MaxSpeed == 80 && s.AverageSpeed == 40,
            "Script statistics differ");
    }
    private static void UnknownAttributes()
    {
        string media = NewMedia(); SaveFavorite(media);
        var d = Document(Sidecar(media));
        Assert(d.DocumentElement.GetAttribute("owner") == "private", "Root attribute lost");
        Assert(d.SelectSingleNode("/movie/rating/@source") != null, "Unknown element attribute lost");
    }
    private static void UnknownElements()
    {
        string media = NewMedia(); SaveFavorite(media);
        Assert(Document(Sidecar(media)).SelectSingleNode("/movie/rating") != null, "Unknown rating element lost");
    }
    private static void CommentsAndInstructions()
    {
        string media = NewMedia(); SaveFavorite(media);
        var d = Document(Sidecar(media));
        Assert(d.SelectNodes("//comment()").Count == 3, "Comments lost");
        Assert(d.SelectSingleNode("/processing-instruction('fixture')") != null, "Processing instruction lost");
    }
    private static void NamespacedData()
    {
        string media = NewMedia(); SaveFavorite(media);
        var d = Document(Sidecar(media));
        var ns = new XmlNamespaceManager(d.NameTable); ns.AddNamespace("x", "urn:r02-test");
        Assert(d.SelectSingleNode("/movie/x:data/@x:revision", ns)?.Value == "4", "Namespaced data or attribute lost");
        Assert(d.SelectSingleNode("/movie/scripts/script/x:axis", ns) != null, "Namespaced script data lost");
    }
    private static string WithoutFavorite(XmlDocument document)
    {
        var clone = (XmlDocument)document.CloneNode(true);
        foreach (XmlNode favorite in clone.SelectNodes("/movie/favorite").Cast<XmlNode>().ToArray())
            favorite.ParentNode.RemoveChild(favorite);
        return clone.OuterXml;
    }
    private static void FavoriteOnly()
    {
        string media = NewMedia();
        string before = WithoutFavorite(Document(Sidecar(media)));
        SaveFavorite(media);
        Assert(Loaded(media).Favorite, "Favorite was not saved");
        Assert(before == WithoutFavorite(Document(Sidecar(media))), "Non-favorite XML metadata changed");
    }
    private static void FavoriteKnownFields()
    {
        string media = NewMedia(); SaveFavorite(media);
        var m = Loaded(media);
        Assert(m.Title == "Original title" && m.OriginalTitle == "Original source title" && m.Plot == "Original plot"
            && m.Type == "video" && m.SourceUrl == "https://example.invalid/topic", "Unselected known field changed");
    }
    private static void TitleAndTags()
    {
        string media = NewMedia(); var m = Loaded(media); m.Title = "Edited title"; m.Tags = new List<string> { "Gamma" };
        m.Save(media); var actual = Loaded(media);
        Assert(actual.Title == "Edited title" && actual.Tags.SequenceEqual(new[] { "Gamma" }), "Explicit edits not persisted");
    }
    private static void ScriptUnknownFields()
    {
        string media = NewMedia(); var m = Loaded(media); m.Title = "Edited"; m.Tags = new List<string> { "Gamma" }; m.Save(media);
        var d = Document(Sidecar(media));
        Assert(d.SelectSingleNode("/movie/scripts/@custom")?.Value == "keep-container", "Scripts container extension lost");
        Assert(d.SelectSingleNode("/movie/scripts/script/@author")?.Value == "private-author", "Script attribute lost");
        Assert(d.SelectSingleNode("/movie/scripts/script/unknown")?.InnerText == "retain", "Script element lost");
    }
    private static void OriginalTitle()
    {
        string media = NewMedia(); var m = Loaded(media); m.Title = "Edited"; m.Save(media);
        Assert(Loaded(media).OriginalTitle == "Original source title", "Original title changed without an explicit edit");
    }
    private static void OriginalTitleNoop()
    {
        string media = NewMedia(); Loaded(media).Save(media);
        Assert(Loaded(media).Title == "Original title" && Loaded(media).OriginalTitle == "Original source title",
            "No-op Save changed a distinct original title");
    }
    private static void RootName()
    {
        string media = NewMedia("<musicvideo provider='private'><title>Original</title><favorite>false</favorite><unknown>keep</unknown></musicvideo>");
        SaveFavorite(media);
        Assert(Document(Sidecar(media)).DocumentElement.Name == "musicvideo", "Existing XML root was replaced");
    }
    private static void Malformed()
    {
        string media = NewMedia("<movie><title>broken</movie>");
        byte[] bytes = File.ReadAllBytes(Sidecar(media));
        bool blocked = Blocked(() => { var m = NfoMetadata.Load(media) ?? new NfoMetadata(); m.Favorite = true; m.Save(media); });
        Assert(blocked, "Malformed XML was accepted for overwrite");
        BytesEqual(bytes, Sidecar(media), "Malformed XML original bytes were lost");
    }
    private static void LegacyPlaintext()
    {
        string media = NewMedia("title: Legacy title\nsource: https://example.invalid\ntags: Alpha; Beta\nprivate-field: keep");
        var m = Loaded(media); Assert(m.Title == "Legacy title", "Legacy read compatibility lost");
        byte[] bytes = File.ReadAllBytes(Sidecar(media)); m.Favorite = true;
        Assert(Blocked(() => m.Save(media)), "Legacy plaintext was silently converted to XML");
        BytesEqual(bytes, Sidecar(media), "Legacy plaintext original bytes were lost");
    }
    private static void ExternalRevision()
    {
        string media = NewMedia(); var stale = Loaded(media);
        File.WriteAllText(Sidecar(media), RichXml.Replace("Original title", "External title"), new UTF8Encoding(false));
        byte[] external = File.ReadAllBytes(Sidecar(media)); stale.Favorite = true;
        Assert(Blocked(() => stale.Save(media)), "Stale metadata silently overwrote external revision");
        BytesEqual(external, Sidecar(media), "External revision bytes were lost");
    }
    private static void SameSizeRevision()
    {
        string media = NewMedia("<movie><title>AAAA</title><favorite>false</favorite></movie>");
        var stale = Loaded(media); DateTime timestamp = File.GetLastWriteTimeUtc(Sidecar(media));
        File.WriteAllText(Sidecar(media), "<movie><title>BBBB</title><favorite>false</favorite></movie>", new UTF8Encoding(false));
        File.SetLastWriteTimeUtc(Sidecar(media), timestamp);
        byte[] external = File.ReadAllBytes(Sidecar(media)); stale.Favorite = true;
        Assert(Blocked(() => stale.Save(media)), "Size/time-only revision comparison missed the external edit");
        BytesEqual(external, Sidecar(media), "Same-size external content was overwritten");
    }
    private static void ExternalDeletion()
    {
        string media = NewMedia(); var stale = Loaded(media);
        File.Delete(Sidecar(media)); stale.Favorite = true;
        Assert(Blocked(() => stale.Save(media)), "Stale metadata recreated an externally removed sidecar");
        Assert(!File.Exists(Sidecar(media)), "Externally removed sidecar was recreated");
    }
    private static void AppearedTarget()
    {
        string media = NewMedia(null); var m = new NfoMetadata { Title = "Draft", Favorite = true };
        File.WriteAllText(Sidecar(media), "<movie><title>External creation</title></movie>", new UTF8Encoding(false));
        byte[] external = File.ReadAllBytes(Sidecar(media));
        Assert(Blocked(() => m.Save(media)), "Unloaded metadata overwrote an existing target");
        BytesEqual(external, Sidecar(media), "Newly appeared target bytes changed");
    }
    private static void Locked()
    {
        string media = NewMedia(); var m = Loaded(media); m.Favorite = true;
        byte[] bytes = File.ReadAllBytes(Sidecar(media));
        using (var locked = new FileStream(Sidecar(media), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert(Blocked(() => m.Save(media)), "Locked target did not reject Save");
            BytesEqual(bytes, Sidecar(media), "Locked target original bytes changed");
        }
    }
    private static void Readonly()
    {
        string media = NewMedia(); var m = Loaded(media); m.Favorite = true;
        byte[] bytes = File.ReadAllBytes(Sidecar(media)); FileAttributes before = File.GetAttributes(Sidecar(media));
        try
        {
            File.SetAttributes(Sidecar(media), before | FileAttributes.ReadOnly);
            Assert(Blocked(() => m.Save(media)), "Readonly target did not reject Save");
            BytesEqual(bytes, Sidecar(media), "Readonly target original bytes changed");
        }
        finally { File.SetAttributes(Sidecar(media), before); }
    }
    private static void SerializationFailure()
    {
        string media = NewMedia(); var m = Loaded(media); byte[] bytes = File.ReadAllBytes(Sidecar(media));
        m.Title = "Invalid XML character " + (char)1;
        Assert(Blocked(() => m.Save(media)), "Invalid XML character unexpectedly serialized");
        BytesEqual(bytes, Sidecar(media), "Serialization failed after truncating/replacing the original file");
    }
    private static void TargetDirectory()
    {
        string media = NewMedia(null); string target = Sidecar(media); Directory.CreateDirectory(target);
        string sentinel = Path.Combine(target, "sentinel.txt"); File.WriteAllText(sentinel, "private sentinel");
        Assert(Blocked(() => new NfoMetadata { Title = "Draft" }.Save(media)), "Directory collision did not reject Save");
        Assert(Directory.Exists(target) && File.ReadAllText(sentinel) == "private sentinel", "Directory collision changed fixture data");
    }
    private static void MissingDirectory()
    {
        string media = Path.Combine(Path.GetDirectoryName(NewMedia(null)), "absent", "Movie.mp4");
        Assert(Blocked(() => new NfoMetadata { Title = "Draft" }.Save(media)), "Missing destination was silently created");
        Assert(!File.Exists(Sidecar(media)), "Metadata created at missing destination");
    }
    private static void SharedVariant()
    {
        string media = NewMedia(); string script = Path.Combine(Path.GetDirectoryName(media), "Movie-soft.funscript");
        Assert(NfoMetadata.GetSidecarPath(script) == Sidecar(media), "Existing hyphen shared fallback changed");
        var m = Loaded(script); m.Favorite = true; m.Save(script);
        Assert(Loaded(media).Favorite && !File.Exists(Sidecar(script)), "Shared variant created an unintended separate sidecar");
    }
    private static void ExactVariant()
    {
        string media = NewMedia(); string script = Path.Combine(Path.GetDirectoryName(media), "Movie-soft.funscript");
        File.WriteAllText(Sidecar(script), "<movie><title>Exact variant</title><favorite>false</favorite></movie>");
        byte[] shared = File.ReadAllBytes(Sidecar(media)); var m = Loaded(script); m.Favorite = true; m.Save(script);
        Assert(Loaded(script).Favorite, "Exact variant favorite was not saved");
        BytesEqual(shared, Sidecar(media), "Exact variant Save changed the shared NFO");
    }
    private static void LoadedPath()
    {
        string media = NewMedia(); string script = Path.Combine(Path.GetDirectoryName(media), "Movie-soft.funscript");
        var loadedShared = Loaded(script);
        File.WriteAllText(Sidecar(script), "<movie><title>New exact sidecar</title><favorite>false</favorite></movie>", new UTF8Encoding(false));
        byte[] exact = File.ReadAllBytes(Sidecar(script)); loadedShared.Favorite = true;
        string saved = loadedShared.Save(script);
        Assert(saved == Sidecar(media), "Save rebound a loaded shared document to a different target");
        BytesEqual(exact, Sidecar(script), "New exact sidecar was overwritten by shared loaded metadata");
        Assert(Loaded(media).Favorite, "Loaded shared target was not updated");
    }
    private static void UnicodeEncoding()
    {
        string media = NewMedia("<?xml version='1.0' encoding='utf-16'?><movie><title>中文 Ω</title><favorite>false</favorite></movie>", Encoding.Unicode);
        SaveFavorite(media); Assert(Loaded(media).Title == "中文 Ω", "Non-ASCII content changed");
    }
    private static void NewDocument()
    {
        string media = NewMedia(null); var m = new NfoMetadata { Title = "New title", Favorite = true, Tags = new List<string> { "Alpha" } };
        Assert(m.Save(media) == Sidecar(media), "First Save returned wrong path");
        Assert(Loaded(media).Title == "New title" && Loaded(media).Favorite, "First Save could not be reloaded");
    }
#if NFO_COMMIT_SEAM
    private static void Noop()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var ops = new CountingFiles();
        Loaded(media).Save(media, ops); BytesEqual(before, Sidecar(media), "No-op changed original bytes");
        Assert(ops.Created == 0 && ops.Replaced == 0, "No-op created commit artifacts");
    }
    private static void Aliases()
    {
        string xml = "<movie><name provider='keep'>Original</name><favourite source='keep'>no</favourite>"
            + "<favorite source='also-keep'>false</favorite><vendor><favorite>vendor-only</favorite><title>vendor title</title><tag>vendor tag</tag></vendor></movie>";
        string media = NewMedia(xml); SaveFavorite(media); var d = Document(Sidecar(media));
        Assert(d.SelectSingleNode("/movie/favourite")?.InnerText == "true" && d.SelectSingleNode("/movie/favorite")?.InnerText == "true", "Favorite alias not patched");
        Assert(d.SelectSingleNode("/movie/favourite/@source")?.Value == "keep", "Favorite attribute lost");
        Assert(d.SelectSingleNode("/movie/vendor/favorite")?.InnerText == "vendor-only", "Vendor favorite patched");
        Assert(d.SelectSingleNode("/movie/name/@provider")?.Value == "keep", "Unselected alias changed");
    }
    private static void NestedVendor()
    {
        string media = NewMedia("<movie><vendor><title>Vendor title</title><tag>Vendor tag</tag></vendor><favorite>false</favorite></movie>");
        var m = Loaded(media); Assert(m.Title == null && m.Tags.Count == 0, "Vendor fields leaked into owned metadata");
        m.Title = "Owned title"; m.Tags.Add("Owned tag"); m.Save(media);
        var d = Document(Sidecar(media)); Assert(d.SelectSingleNode("/movie/vendor/title")?.InnerText == "Vendor title"
            && d.SelectSingleNode("/movie/vendor/tag")?.InnerText == "Vendor tag", "Vendor fields overwritten");
    }
    private static void CopyIsolation()
    {
        string media = NewMedia(); var original = Loaded(media); var draft = original.CopyForEdit();
        draft.Favorite = true; draft.Scripts[0].ActionCount = 999;
        Assert(Blocked(() => draft.Save(media)), "Unsupported draft should be rejected");
        Assert(!original.Favorite && original.Scripts[0].ActionCount == 12, "Failed draft mutated original shared metadata");
    }
    private static void ScriptValueSnapshot()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media);
        m.Scripts = m.CopyForEdit().Scripts; m.Save(media);
        BytesEqual(before, Sidecar(media), "Equal script values changed document bytes");
    }
    private static void DirtyScripts()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Scripts[0].ActionCount = 999;
        Assert(Blocked(() => m.Save(media)), "Dirty script statistics were silently rebuilt");
        BytesEqual(before, Sidecar(media), "Rejected dirty script edit changed original bytes");
    }
    private static void MixedContent()
    {
        string media = NewMedia("<movie><title>Title<extension>keep</extension></title><favorite>false</favorite></movie>");
        byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Title = "Edited";
        Assert(Blocked(() => m.Save(media)), "Mixed-content edit was not explicitly rejected");
        BytesEqual(before, Sidecar(media), "Mixed-content extension was lost");
    }
    private static void RejectPreserving(NfoFileOperations ops, bool expectArtifactsClean = true)
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        Assert(Blocked(() => m.Save(media, ops)), "Injected failure unexpectedly saved");
        BytesEqual(before, Sidecar(media), "Injected failure changed original bytes");
        if (expectArtifactsClean) Assert(Directory.GetFiles(Path.GetDirectoryName(media), "*.r02-*").Length == 0, "Owned commit artifacts were not cleaned");
    }
    private static void TempWriteFailure()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        NfoSaveException observed = null;
        try { m.Save(media, new FaultFiles { Failure = "write" }); } catch (NfoSaveException ex) { observed = ex; }
        Assert(observed != null && observed.TemporaryPath != null, "Partial stage location not reported");
        BytesEqual(before, Sidecar(media), "Partial stage failure changed original bytes");
        Assert(File.ReadAllBytes(observed.TemporaryPath).Length == 8, "Unverifiable partial stage was blindly deleted");
    }
    private static void FlushFailure() { RejectPreserving(new FaultFiles { Failure = "flush" }); }
    private static void ReplaceRefusal() { RejectPreserving(new FaultFiles { Failure = "replace" }); }
    private static void ReplaceMovedFailure() { RejectPreserving(new FaultFiles { Failure = "moved" }); }
    private static void RecoveryFailure()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        NfoSaveException observed = null;
        try { m.Save(media, new FaultFiles { Failure = "recover" }); } catch (NfoSaveException ex) { observed = ex; }
        Assert(observed != null && observed.RecoveryRequired, "Failed recovery did not expose recovery state");
        Assert(!File.Exists(Sidecar(media)) && observed.BackupPath != null && observed.TemporaryPath != null, "Actual file locations not exposed");
        BytesEqual(before, observed.BackupPath, "Original backup bytes lost");
        Assert(File.Exists(observed.TemporaryPath), "Recoverable draft was discarded");
    }
    private static void CompletedReplaceException()
    {
        string media = NewMedia(); var m = Loaded(media); m.Favorite = true;
        m.Save(media, new FaultFiles { Failure = "after" });
        Assert(Loaded(media).Favorite && !string.IsNullOrEmpty(m.LastSaveWarning), "Verified commit was reported as a retryable failure");
    }
    private static void CleanupFailure()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        m.Save(media, new FaultFiles { Failure = "cleanup" });
        Assert(Loaded(media).Favorite && !string.IsNullOrEmpty(m.LastSaveWarning), "Cleanup failure hid committed state");
        string backup = Directory.GetFiles(Path.GetDirectoryName(media), "*.r02-*.bak").Single();
        BytesEqual(before, backup, "Retained backup bytes differ");
    }
    private static void ReplaceExternalRace()
    {
        string media = NewMedia(); var m = Loaded(media); m.Favorite = true; var files = new FaultFiles { Failure = "race" };
        NfoSaveException observed = null;
        try { m.Save(media, files); } catch (NfoSaveException ex) { observed = ex; }
        Assert(observed != null && observed.RecoveryRequired && observed.BackupPath != null, "Narrow conflict was reported as success");
        BytesEqual(files.ExternalBytes, observed.BackupPath, "Displaced external revision was lost");
    }
    private static void ExternalAfterReplace()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        var files = new FaultFiles { Failure = "external-after" }; NfoSaveException observed = null;
        try { m.Save(media, files); } catch (NfoSaveException ex) { observed = ex; }
        Assert(observed != null && observed.RecoveryRequired, "Post-commit external edit was hidden");
        BytesEqual(files.ExternalBytes, Sidecar(media), "Rollback overwrote the latest external edit");
        BytesEqual(before, observed.BackupPath, "Original bytes no longer recoverable");
    }
    private static void NewMoveCollision() { NewCollision(false); }
    private static void NewMoveIdenticalCollision() { NewCollision(true); }
    private static void NewCollision(bool identical)
    {
        string media = NewMedia(null); var m = new NfoMetadata { Title = "Draft", Favorite = true };
        var files = new FaultFiles { Failure = identical ? "collision-identical" : "collision" };
        Assert(Blocked(() => m.Save(media, files)), "New-file collision was mistaken for our commit");
        BytesEqual(files.ExternalBytes, Sidecar(media), "New-file collision overwrote external bytes");
    }
    private static void AttributedTags()
    {
        string media = NewMedia("<movie><title>Original</title><tag id='A'>Alpha</tag><tag id='B'>Beta</tag></movie>");
        var m = Loaded(media); m.Tags = new List<string> { "0Inserted", "Beta", "Alpha" }; m.Save(media);
        var d = Document(Sidecar(media));
        Assert(d.SelectSingleNode("/movie/tag[@id='A']")?.InnerText == "Alpha", "Alpha attribute became attached to another tag");
        Assert(d.SelectSingleNode("/movie/tag[@id='B']")?.InnerText == "Beta", "Beta attribute became attached to another tag");
        Assert(d.SelectSingleNode("/movie/tag[not(@id)]")?.InnerText == "0Inserted", "Inserted tag inherited unknown attributes");
    }
    private static void ConsumedTemporary()
    {
        string media = NewMedia(); var m = Loaded(media); m.Favorite = true;
        var files = new FaultFiles { Failure = "recreated-stage" }; m.Save(media, files);
        Assert(Loaded(media).Favorite, "Verified commit did not complete");
        BytesEqual(files.ExternalBytes, files.Temporary, "Cleanup deleted a recreated external stage file");
        Assert(!string.IsNullOrEmpty(m.LastSaveWarning), "Retained stage not exposed as a warning");
    }
    private static void ModifiedTemporary()
    {
        string media = NewMedia(); byte[] before = File.ReadAllBytes(Sidecar(media)); var m = Loaded(media); m.Favorite = true;
        var files = new FaultFiles { Failure = "modified-stage" }; NfoSaveException observed = null;
        try { m.Save(media, files); } catch (NfoSaveException ex) { observed = ex; }
        Assert(observed != null && observed.TemporaryPath != null, "Modified stage location not exposed");
        BytesEqual(before, Sidecar(media), "Modified stage failure changed target");
        BytesEqual(files.ExternalBytes, observed.TemporaryPath, "Cleanup deleted an externally changed stage");
    }
    private static void ConcurrentCommits()
    {
        string media = NewMedia(); var first = Loaded(media); var second = Loaded(media);
        first.Title = "First commit"; second.Title = "Stale second"; var files = new BlockingFiles();
        Task firstTask = Task.Run(() => first.Save(media, files)); Task<bool> secondTask = null;
        try
        {
            Assert(files.Entered.Wait(3000), "First commit did not reach Replace");
            var secondEntered = new ManualResetEventSlim();
            secondTask = Task.Run(() => { secondEntered.Set(); return Blocked(() => second.Save(media)); });
            Assert(secondEntered.Wait(3000), "Second caller did not start");
            Assert(!secondTask.Wait(120), "Second commit was not serialized behind the first");
        }
        finally { files.Continue.Set(); }
        Assert(firstTask.Wait(3000) && secondTask != null && secondTask.Wait(3000), "Commit threads did not finish");
        Assert(secondTask.Result, "Stale second commit did not report conflict");
        Assert(Loaded(media).Title == "First commit", "Second commit overwrote first committed bytes");
    }
    private sealed class BlockingFiles : NfoFileOperations
    {
        internal readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
        internal readonly ManualResetEventSlim Continue = new ManualResetEventSlim();
        public override void Replace(string temporary, string target, string backup)
        {
            Entered.Set(); if (!Continue.Wait(5000)) throw new IOException("Fixture Replace barrier timed out");
            base.Replace(temporary, target, backup);
        }
    }
    private class CountingFiles : NfoFileOperations
    {
        internal int Created, Replaced;
        public override Stream CreateTemporaryFile(string path) { Created++; return base.CreateTemporaryFile(path); }
        public override void Replace(string temporary, string target, string backup) { Replaced++; base.Replace(temporary, target, backup); }
    }
    private sealed class FaultFiles : NfoFileOperations
    {
        internal string Failure;
        internal byte[] ExternalBytes;
        internal string Temporary;
        public override Stream CreateTemporaryFile(string path)
        {
            var stream = base.CreateTemporaryFile(path);
            return Failure == "write" && path.EndsWith(".tmp") ? new FailingWriteStream(stream) : stream;
        }
        public override void FlushTemporaryFile(Stream stream)
        {
            base.FlushTemporaryFile(stream);
            if (Failure == "flush") throw new IOException("Fixture flush failure");
        }
        public override void Replace(string temporary, string target, string backup)
        {
            Temporary = temporary;
            if (Failure == "modified-stage")
            {
                ExternalBytes = Encoding.UTF8.GetBytes("external stage bytes");
                File.WriteAllBytes(temporary, ExternalBytes); throw new IOException("Fixture stage modification");
            }
            if (Failure == "replace") throw new IOException("Fixture replace refusal");
            if (Failure == "moved" || Failure == "recover")
            {
                File.Delete(backup); // Only the module-owned placeholder in this marked fixture tree.
                File.Move(target, backup);
                throw new IOException("Fixture ERROR_UNABLE_TO_MOVE_REPLACEMENT_2 (1177)");
            }
            if (Failure == "race")
            {
                ExternalBytes = Encoding.UTF8.GetBytes("<movie><title>External revision during Replace</title></movie>");
                File.WriteAllBytes(target, ExternalBytes);
            }
            base.Replace(temporary, target, backup);
            if (Failure == "recreated-stage")
            {
                ExternalBytes = Encoding.UTF8.GetBytes("external recreated stage");
                File.WriteAllBytes(temporary, ExternalBytes);
            }
            if (Failure == "after") throw new IOException("Fixture exception after actual Replace");
            if (Failure == "external-after")
            {
                ExternalBytes = Encoding.UTF8.GetBytes("<movie><title>Latest external revision</title></movie>");
                File.WriteAllBytes(target, ExternalBytes);
                throw new IOException("Fixture latest external edit after Replace");
            }
        }
        public override void MoveNoOverwrite(string source, string target)
        {
            if (Failure == "recover" && source.EndsWith(".bak")) throw new IOException("Fixture recovery refusal");
            if ((Failure == "collision" || Failure == "collision-identical") && source.EndsWith(".tmp"))
            {
                ExternalBytes = Failure == "collision-identical" ? File.ReadAllBytes(source)
                    : Encoding.UTF8.GetBytes("<movie><title>External new target</title></movie>");
                File.WriteAllBytes(target, ExternalBytes);
            }
            base.MoveNoOverwrite(source, target);
        }
        public override void DeleteOwnedFile(string path)
        {
            if (Failure == "cleanup" && path.EndsWith(".bak")) throw new IOException("Fixture cleanup refusal");
            base.DeleteOwnedFile(path);
        }
    }
    private sealed class FailingWriteStream : Stream
    {
        private readonly Stream wrapped;
        internal FailingWriteStream(Stream wrapped) { this.wrapped = wrapped; }
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return wrapped.Length; } }
        public override long Position { get { return wrapped.Position; } set { throw new NotSupportedException(); } }
        public override void Flush() { wrapped.Flush(); }
        public override void Write(byte[] buffer, int offset, int count)
        { wrapped.Write(buffer, offset, Math.Min(8, count)); throw new IOException("Fixture temporary stream write failure"); }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        protected override void Dispose(bool disposing) { if (disposing) wrapped.Dispose(); base.Dispose(disposing); }
    }
#endif
}
