using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ScriptPlayer.Metadata
{
    /// <summary>
    /// Local, Kodi-compatible sidecar metadata shared by a video and all of its script variants.
    /// </summary>
    public class NfoMetadata
    {
        public string Title { get; set; }
        public string OriginalTitle { get; set; }
        public string SourceUrl { get; set; }
        public string Type { get; set; }
        public string Plot { get; set; }
        public bool Favorite { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public List<NfoScriptStatistics> Scripts { get; set; } = new List<NfoScriptStatistics>();

        private XmlDocument _document;
        private byte[] _sourceBytes;
        private Encoding _encoding = new UTF8Encoding(false);
        private NfoMetadata _originalValues;
        private string _writeBlockReason;
        // These infrequent metadata commits serialize only with one another, never with playback/output.
        private static readonly object CommitGate = new object();
        public string SidecarPath { get; private set; }
        public string LastSaveWarning { get; private set; }

        public NfoMetadata CopyForEdit()
        {
            var copy = (NfoMetadata)MemberwiseClone();
            copy.Tags = Tags == null ? null : new List<string>(Tags);
            copy.Scripts = CloneScripts(Scripts);
            copy.LastSaveWarning = null;
            return copy;
        }

        public static string GetSidecarPath(string mediaOrScriptPath)
        {
            if (string.IsNullOrWhiteSpace(mediaOrScriptPath))
                return null;

            string directory = Path.GetDirectoryName(mediaOrScriptPath);
            string name = Path.GetFileNameWithoutExtension(mediaOrScriptPath);

            // Variant scripts such as Movie-soft.funscript share Movie.nfo when it exists.
            string exact = Path.Combine(directory ?? string.Empty, name + ".nfo");
            if (File.Exists(exact))
                return exact;

            int separator = name.LastIndexOf('-');
            while (separator > 0)
            {
                string shared = Path.Combine(directory ?? string.Empty, name.Substring(0, separator) + ".nfo");
                if (File.Exists(shared))
                    return shared;
                separator = name.LastIndexOf('-', separator - 1);
            }

            return exact;
        }

        public static NfoMetadata Load(string mediaOrScriptPath)
        {
            string path = GetSidecarPath(mediaOrScriptPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            path = Path.GetFullPath(path);
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var result = new NfoMetadata { SidecarPath = path, _sourceBytes = bytes };
                try
                {
                    result._document = ParseDocument(bytes);
                    if (result._document.DocumentElement == null) return null;
                    result._encoding = SourceEncoding(bytes, result._document);
                    ReadKnownValues(result._document, result);
                }
                catch (XmlException ex)
                {
                    // Legacy text remains readable, but conversion is never an implicit write operation.
                    ReadPlainText(bytes, result);
                    result._writeBlockReason = "现有 NFO 不是可安全编辑的 XML，已保留原文件：" + ex.Message;
                }
                result._originalValues = CopyValues(result);
                return result;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static XmlDocument ParseDocument(byte[] bytes)
        {
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                document.Load(reader);
            return document;
        }

        private static Encoding SourceEncoding(byte[] bytes, XmlDocument document)
        {
            if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191) return new UTF8Encoding(true);
            if (bytes.Length >= 2 && bytes[0] == 255 && bytes[1] == 254) return Encoding.Unicode;
            if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == 255) return Encoding.BigEndianUnicode;
            var declaration = document.FirstChild as XmlDeclaration;
            if (declaration != null && !string.IsNullOrWhiteSpace(declaration.Encoding)
                && !declaration.Encoding.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
                return Encoding.GetEncoding(declaration.Encoding);
            return new UTF8Encoding(false);
        }

        private static List<XmlElement> OwnElements(XmlElement parent, params string[] names)
        {
            return parent.ChildNodes.OfType<XmlElement>().Where(element => element.NamespaceURI == parent.NamespaceURI
                && names.Contains(element.LocalName, StringComparer.Ordinal)).ToList();
        }

        private static string OwnText(XmlElement element)
        {
            return string.Concat(element.ChildNodes.Cast<XmlNode>().Where(node => node is XmlText || node is XmlCDataSection)
                .Select(node => node.Value)).Trim();
        }

        private static string ReadFirst(XmlElement root, params string[] names)
        {
            foreach (string name in names)
                foreach (var element in OwnElements(root, name))
                {
                    string value = OwnText(element);
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            return null;
        }
        private static long? ReadLong(XmlElement root, params string[] names)
        {
            long value; return long.TryParse(ReadFirst(root, names), out value) ? value : (long?)null;
        }
        private static int? ReadInt(XmlElement root, params string[] names)
        {
            int value; return int.TryParse(ReadFirst(root, names), out value) ? value : (int?)null;
        }
        private static bool ReadBool(XmlElement root, params string[] names)
        {
            string raw = ReadFirst(root, names); bool value;
            return bool.TryParse(raw, out value) ? value : raw == "1" || string.Equals(raw, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadKnownValues(XmlDocument document, NfoMetadata result)
        {
            var root = document.DocumentElement;
            result.Title = ReadFirst(root, "title", "name");
            result.OriginalTitle = ReadFirst(root, "originaltitle", "original-title");
            result.SourceUrl = ReadFirst(root, "source", "url", "website");
            result.Type = ReadFirst(root, "type", "mediatype");
            result.Plot = ReadFirst(root, "plot", "description");
            result.Favorite = ReadBool(root, "favorite", "favourite");
            result.Tags = new List<string>();
            foreach (var tag in OwnElements(root, "tag", "genre")) AddTags(result.Tags, OwnText(tag));
            result.Tags = result.Tags.Distinct(StringComparer.InvariantCultureIgnoreCase)
                .OrderBy(tag => tag, StringComparer.InvariantCultureIgnoreCase).ToList();
            result.Scripts = new List<NfoScriptStatistics>();
            foreach (var scripts in OwnElements(root, "scripts"))
                foreach (var element in OwnElements(scripts, "script"))
                {
                    string fileName = element.GetAttribute("file");
                    if (string.IsNullOrWhiteSpace(fileName)) fileName = ReadFirst(element, "file", "filename");
                    if (string.IsNullOrWhiteSpace(fileName)) continue;
                    result.Scripts.Add(new NfoScriptStatistics { FileName = fileName.Trim(),
                        Duration = ReadFirst(element, "duration"), DurationMilliseconds = ReadLong(element, "durationms"),
                        ActionCount = ReadInt(element, "actions", "actioncount"), MaxSpeed = ReadInt(element, "maxspeed"),
                        AverageSpeed = ReadInt(element, "avgspeed", "averagespeed") });
                }
        }

        private static void ReadPlainText(byte[] bytes, NfoMetadata result)
        {
            using (var reader = new StreamReader(new MemoryStream(bytes, false), Encoding.UTF8, true))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string line = rawLine.Trim(); int separator = line.IndexOf(':');
                    if (separator <= 0) continue;
                    string key = line.Substring(0, separator).Trim().ToLowerInvariant();
                    string value = line.Substring(separator + 1).Trim();
                    switch (key)
                    {
                        case "title": result.Title = value; break;
                        case "source": case "url": result.SourceUrl = value; break;
                        case "type": result.Type = value; break;
                        case "plot": case "description": result.Plot = value; break;
                        case "tag": case "tags": case "genre": AddTags(result.Tags, value); break;
                    }
                }
            }
        }

        public string Save(string mediaOrScriptPath)
        {
            return Save(mediaOrScriptPath, new NfoFileOperations());
        }

        /// <summary>Real file-operation seam for deterministic commit failures; never substitutes metadata policy.</summary>
        public string Save(string mediaOrScriptPath, NfoFileOperations files)
        {
            lock (CommitGate) return SaveCore(mediaOrScriptPath, files);
        }

        private string SaveCore(string mediaOrScriptPath, NfoFileOperations files)
        {
            if (files == null) throw new ArgumentNullException(nameof(files));
            string path = SidecarPath ?? GetSidecarPath(mediaOrScriptPath);
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("无法确定 NFO 文件路径。", nameof(mediaOrScriptPath));
            path = Path.GetFullPath(path);
            LastSaveWarning = null;
            if (_writeBlockReason != null) throw new NfoSaveException(path, _writeBlockReason, null, null, false);
            VerifyVersion(files, path);
            if (_document != null && !HasChanges()) return path;
            XmlDocument updated = BuildDocument();
            byte[] desired = Serialize(updated, _encoding, _document == null);
            string token = Guid.NewGuid().ToString("N");
            string temporary = path + ".r02-" + token + ".tmp";
            string backup = path + ".r02-" + token + ".bak";
            byte[] reservation = Encoding.UTF8.GetBytes("ScriptPlayer R02 backup reservation " + token);
            bool temporaryOwned = false, backupOwned = false, operationStarted = false, committed = false;
            try
            {
                using (var stream = files.CreateTemporaryFile(temporary))
                {
                    temporaryOwned = true;
                    stream.Write(desired, 0, desired.Length);
                    files.FlushTemporaryFile(stream);
                }
                if (_sourceBytes != null)
                    using (var stream = files.CreateTemporaryFile(backup))
                    {
                        backupOwned = true;
                        stream.Write(reservation, 0, reservation.Length);
                        files.FlushTemporaryFile(stream);
                    }
                // This final content check plus Replace is not a filesystem CAS. Inspect the displaced backup too.
                VerifyVersion(files, path);
                if (_sourceBytes == null)
                {
                    operationStarted = true;
                    files.MoveNoOverwrite(temporary, path);
                }
                else
                {
                    if (!BytesMatch(files, backup, reservation)) throw new IOException("提交备份占位文件已被修改。");
                    operationStarted = true;
                    files.Replace(temporary, path, backup);
                }
                if (!BytesMatch(files, path, desired)) throw new IOException("提交后的 NFO 内容与本次写入不一致。");
                if (_sourceBytes != null && !BytesMatch(files, backup, _sourceBytes))
                    throw new NfoSaveException(path, "提交期间发现外部修订；原数据保存在备份，请先检查恢复。", temporary, backup, true);
                committed = true;
            }
            catch (Exception ex)
            {
                if (committed) throw;
                if (operationStarted && !SafeFileExists(files, temporary) && BytesMatch(files, path, desired)
                    && (_sourceBytes == null || BytesMatch(files, backup, _sourceBytes)))
                {
                    committed = true;
                    LastSaveWarning = "文件已保存，但提交操作报告异常：" + ex.Message;
                }
                else
                {
                    bool recoveryRequired = false;
                    string detail = ex.Message;
                    if (operationStarted && _sourceBytes != null)
                    {
                        if (!SafeFileExists(files, path) && BytesMatch(files, backup, _sourceBytes))
                        {
                            try
                            {
                                files.MoveNoOverwrite(backup, path);
                                if (!BytesMatch(files, path, _sourceBytes)) throw new IOException("恢复后的文件校验失败。");
                                backupOwned = false;
                                detail += " 原 NFO 已恢复。";
                            }
                            catch (Exception recovery) { recoveryRequired = true; detail += " 恢复失败：" + recovery.Message; }
                        }
                        else if (!BytesMatch(files, path, _sourceBytes)) recoveryRequired = true;
                    }
                    if (!recoveryRequired)
                    {
                        if (temporaryOwned) CleanupOwned(files, temporary, desired, ref detail);
                        if (backupOwned && BytesMatch(files, backup, reservation)) CleanupOwned(files, backup, reservation, ref detail);
                    }
                    throw new NfoSaveException(path, detail, ArtifactPath(files, temporary),
                        ArtifactPath(files, backup), recoveryRequired, ex);
                }
            }
            // A successful commit remains successful if cleanup fails; callers must not retry a toggle.
            string cleanupWarning = LastSaveWarning;
            if (temporaryOwned) CleanupOwned(files, temporary, desired, ref cleanupWarning);
            if (backupOwned && BytesMatch(files, backup, _sourceBytes)) CleanupOwned(files, backup, _sourceBytes, ref cleanupWarning);
            LastSaveWarning = cleanupWarning;
            SidecarPath = path;
            _sourceBytes = desired;
            _document = updated;
            ReadKnownValues(updated, this);
            _originalValues = CopyValues(this);
            return path;
        }

        private void VerifyVersion(NfoFileOperations files, string path)
        {
            if (files.DirectoryExists(path)) throw new NfoSaveException(path, "目标是目录，不能保存 NFO。", null, null, false);
            if (_sourceBytes == null)
            {
                if (files.FileExists(path)) throw new NfoSaveException(path, "目标已存在；请重新读取后编辑，不能覆盖未读取的文件。", null, null, false);
            }
            else if (!BytesMatch(files, path, _sourceBytes))
                throw new NfoSaveException(path, "NFO 已被外部修改、删除或无法读取；请重新读取后编辑。", null, null, false);
            if (files.FileExists(path) && (files.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new NfoSaveException(path, "NFO 为只读文件，未修改原文件。", null, null, false);
        }

        private static bool BytesMatch(NfoFileOperations files, string path, byte[] expected)
        {
            if (expected == null) return false;
            try
            {
                if (!files.FileExists(path)) return false;
                byte[] actual = files.ReadAllBytes(path);
                if (actual.Length != expected.Length) return false;
                using (var hash = SHA256.Create()) return hash.ComputeHash(actual).SequenceEqual(hash.ComputeHash(expected));
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        private static bool SafeFileExists(NfoFileOperations files, string path)
        {
            try { return files.FileExists(path); }
            catch (Exception) { return true; } // Unknown location is conservatively treated as occupied.
        }
        private static string ArtifactPath(NfoFileOperations files, string path)
        {
            return SafeFileExists(files, path) ? path : null;
        }
        private static void CleanupOwned(NfoFileOperations files, string path, byte[] expected, ref string warning)
        {
            try
            {
                if (!files.FileExists(path)) return;
                if (!BytesMatch(files, path, expected))
                {
                    warning = (warning ?? string.Empty) + " 暂存资源内容无法确认，已保留：" + path;
                    return;
                }
                files.DeleteOwnedFile(path);
            }
            catch (Exception ex) { warning = (warning ?? string.Empty) + " 临时资源清理失败：" + path + "：" + ex.Message; }
        }

        private bool HasChanges()
        {
            if (_originalValues == null) return true;
            return Title != _originalValues.Title || OriginalTitle != _originalValues.OriginalTitle || SourceUrl != _originalValues.SourceUrl
                || Type != _originalValues.Type || Plot != _originalValues.Plot || Favorite != _originalValues.Favorite
                || !SameTags(Tags, _originalValues.Tags) || !SameScripts(Scripts, _originalValues.Scripts);
        }
        private XmlDocument BuildDocument()
        {
            bool fresh = _document == null;
            var document = fresh ? new XmlDocument { PreserveWhitespace = true, XmlResolver = null } : (XmlDocument)_document.CloneNode(true);
            if (fresh)
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", _encoding.WebName, null));
                document.AppendChild(document.CreateElement("movie"));
            }
            var root = document.DocumentElement;
            if (fresh || Title != _originalValues.Title) PatchScalar(root, Title, "title", "name");
            if (fresh || OriginalTitle != _originalValues.OriginalTitle) PatchScalar(root, OriginalTitle, "originaltitle", "original-title");
            if (fresh || SourceUrl != _originalValues.SourceUrl) PatchScalar(root, SourceUrl, "source", "url", "website");
            if (fresh || Type != _originalValues.Type) PatchScalar(root, Type, "type", "mediatype");
            if (fresh || Plot != _originalValues.Plot) PatchScalar(root, Plot, "plot", "description");
            if (fresh || Favorite != _originalValues.Favorite) PatchScalar(root, Favorite ? "true" : "false", "favorite", "favourite");
            if (fresh || !SameTags(Tags, _originalValues.Tags)) PatchTags(root, Tags);
            if (fresh) WriteNewScripts(root, Scripts);
            else if (!SameScripts(Scripts, _originalValues.Scripts))
                throw new InvalidOperationException("修改现有脚本统计需要专用的无损编辑；本次保存已拒绝，原文件保持不变。");
            return document;
        }
        private static XmlElement NewOwnElement(XmlElement root, string name)
        {
            return root.OwnerDocument.CreateElement(root.Prefix, name, root.NamespaceURI);
        }
        private static void SetOwnText(XmlElement element, string value)
        {
            if (element.ChildNodes.OfType<XmlElement>().Any())
                throw new InvalidOperationException("字段包含复杂子元素，不能安全编辑：" + element.LocalName);
            foreach (var text in element.ChildNodes.Cast<XmlNode>().Where(node => node is XmlText || node is XmlCDataSection).ToArray())
                element.RemoveChild(text);
            element.PrependChild(element.OwnerDocument.CreateTextNode((value ?? string.Empty).Trim()));
        }
        private static void PatchScalar(XmlElement root, string value, params string[] aliases)
        {
            var fields = OwnElements(root, aliases);
            if (fields.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var field = NewOwnElement(root, aliases[0]); root.AppendChild(field); fields.Add(field);
            }
            foreach (var field in fields) SetOwnText(field, value);
        }
        private static void PatchTags(XmlElement root, List<string> tags)
        {
            var fields = OwnElements(root, "tag", "genre");
            var values = (tags ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();
            var remaining = new List<string>(values);
            foreach (var field in fields)
            {
                string existing = OwnText(field);
                string retained = remaining.FirstOrDefault(value => string.Equals(value, existing, StringComparison.InvariantCultureIgnoreCase));
                if (retained != null)
                {
                    // Attributes belong to this tag's value, not its position in a UI-sorted list.
                    remaining.Remove(retained);
                    continue;
                }
                if (field.Attributes.Count > 0 || field.ChildNodes.Cast<XmlNode>().Any(node => !(node is XmlText || node is XmlCDataSection)))
                    throw new InvalidOperationException("待移除的标签包含扩展属性或内容，不能安全编辑。");
                root.RemoveChild(field);
            }
            foreach (string value in remaining)
            {
                var field = NewOwnElement(root, "tag"); root.AppendChild(field); SetOwnText(field, value);
            }
        }
        private static void WriteNewScripts(XmlElement root, List<NfoScriptStatistics> scripts)
        {
            var values = (scripts ?? new List<NfoScriptStatistics>()).Where(value => value != null && !string.IsNullOrWhiteSpace(value.FileName)).ToList();
            if (values.Count == 0) return;
            var container = NewOwnElement(root, "scripts"); root.AppendChild(container);
            foreach (var value in values)
            {
                var script = NewOwnElement(container, "script"); container.AppendChild(script); script.SetAttribute("file", value.FileName.Trim());
                PatchScalar(script, value.Duration, "duration");
                if (value.DurationMilliseconds.HasValue) PatchScalar(script, value.DurationMilliseconds.Value.ToString(), "durationms");
                if (value.ActionCount.HasValue) PatchScalar(script, value.ActionCount.Value.ToString(), "actions");
                if (value.MaxSpeed.HasValue) PatchScalar(script, value.MaxSpeed.Value.ToString(), "maxspeed");
                if (value.AverageSpeed.HasValue) PatchScalar(script, value.AverageSpeed.Value.ToString(), "avgspeed");
            }
        }
        private static byte[] Serialize(XmlDocument document, Encoding encoding, bool indent)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = encoding, Indent = indent,
                    NewLineChars = Environment.NewLine, NewLineHandling = NewLineHandling.None, CloseOutput = false })) document.Save(writer);
                return stream.ToArray();
            }
        }
        private static NfoMetadata CopyValues(NfoMetadata value)
        {
            return new NfoMetadata { Title = value.Title, OriginalTitle = value.OriginalTitle, SourceUrl = value.SourceUrl,
                Type = value.Type, Plot = value.Plot, Favorite = value.Favorite,
                Tags = value.Tags == null ? null : new List<string>(value.Tags), Scripts = CloneScripts(value.Scripts) };
        }
        private static List<NfoScriptStatistics> CloneScripts(List<NfoScriptStatistics> scripts)
        {
            return scripts?.Select(value => value == null ? null : new NfoScriptStatistics { FileName = value.FileName,
                Duration = value.Duration, DurationMilliseconds = value.DurationMilliseconds, ActionCount = value.ActionCount,
                MaxSpeed = value.MaxSpeed, AverageSpeed = value.AverageSpeed }).ToList();
        }
        private static bool SameTags(List<string> left, List<string> right)
        {
            return (left ?? new List<string>()).SequenceEqual(right ?? new List<string>(), StringComparer.Ordinal);
        }
        private static bool SameScripts(List<NfoScriptStatistics> left, List<NfoScriptStatistics> right)
        {
            var a = left ?? new List<NfoScriptStatistics>(); var b = right ?? new List<NfoScriptStatistics>();
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] == null || b[i] == null) { if (a[i] != b[i]) return false; continue; }
                if (a[i].FileName != b[i].FileName || a[i].Duration != b[i].Duration || a[i].DurationMilliseconds != b[i].DurationMilliseconds
                    || a[i].ActionCount != b[i].ActionCount || a[i].MaxSpeed != b[i].MaxSpeed || a[i].AverageSpeed != b[i].AverageSpeed) return false;
            }
            return true;
        }
        public NfoScriptStatistics FindScript(string scriptPathOrName)
        {
            if (string.IsNullOrWhiteSpace(scriptPathOrName) || Scripts == null)
                return null;

            string fileName = Path.GetFileName(scriptPathOrName);
            NfoScriptStatistics exact = Scripts.FirstOrDefault(script => script != null &&
                string.Equals(script.FileName, fileName, StringComparison.InvariantCultureIgnoreCase));
            if (exact != null)
                return exact;

            string normalized = NormalizeFileName(fileName);
            NfoScriptStatistics normalizedMatch = Scripts.FirstOrDefault(script => script != null &&
                string.Equals(NormalizeFileName(script.FileName), normalized, StringComparison.Ordinal));
            if (normalizedMatch != null)
                return normalizedMatch;

            return Scripts.Count == 1 ? Scripts[0] : null;
        }

        private static string NormalizeFileName(string value)
        {
            string name = Path.GetFileNameWithoutExtension(value ?? string.Empty).ToLowerInvariant();
            return Regex.Replace(name, "[^\\p{L}\\p{Nd}]", string.Empty);
        }

        private static void AddTags(ICollection<string> target, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            foreach (string value in raw.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string tag = value.Trim().TrimStart('#');
                if (!string.IsNullOrWhiteSpace(tag))
                    target.Add(tag);
            }
        }

        private static void WriteElement(XmlWriter writer, string name, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                writer.WriteElementString(name, value.Trim());
        }
    }

    public class NfoScriptStatistics
    {
        public string FileName { get; set; }
        public string Duration { get; set; }
        public long? DurationMilliseconds { get; set; }
        public int? ActionCount { get; set; }
        public int? MaxSpeed { get; set; }
        public int? AverageSpeed { get; set; }
    }
    /// <summary>Filesystem adapter; tests decorate real operations rather than bypassing the commit policy.</summary>
    public class NfoFileOperations
    {
        public virtual bool FileExists(string path) { return File.Exists(path); }
        public virtual bool DirectoryExists(string path) { return Directory.Exists(path); }
        public virtual byte[] ReadAllBytes(string path) { return File.ReadAllBytes(path); }
        public virtual FileAttributes GetAttributes(string path) { return File.GetAttributes(path); }
        public virtual Stream CreateTemporaryFile(string path) { return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); }
        public virtual void FlushTemporaryFile(Stream stream)
        {
            stream.Flush();
            var file = stream as FileStream;
            if (file != null) file.Flush(true);
        }
        public virtual void Replace(string temporary, string target, string backup) { File.Replace(temporary, target, backup, false); }
        public virtual void MoveNoOverwrite(string source, string target) { File.Move(source, target); }
        public virtual void DeleteOwnedFile(string path) { File.Delete(path); }
    }

    public sealed class NfoSaveException : IOException
    {
        public string SidecarPath { get; private set; }
        public string TemporaryPath { get; private set; }
        public string BackupPath { get; private set; }
        public bool RecoveryRequired { get; private set; }
        public NfoSaveException(string path, string reason, string temporary, string backup, bool recoveryRequired, Exception inner = null)
            : base(reason + "\nNFO: " + path + (backup == null ? "" : "\nBackup: " + backup)
                + (temporary == null ? "" : "\nTemporary: " + temporary), inner)
        {
            SidecarPath = path; TemporaryPath = temporary; BackupPath = backup; RecoveryRequired = recoveryRequired;
        }
    }
}
