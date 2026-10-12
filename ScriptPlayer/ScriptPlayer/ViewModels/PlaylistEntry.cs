using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using JetBrains.Annotations;
using ScriptPlayer.Localization;
using ScriptPlayer.Metadata;

namespace ScriptPlayer.ViewModels
{
    public class PlaylistEntry : INotifyPropertyChanged
    {
        private TimeSpan? _duration;
        private bool _hasMedia;
        private bool _hasScript;
        private PlaylistEntryStatus _status;
        private string _shortname;
        private string _fullname;
        private bool _removed;
        private ImageSource _heatMap;
        private DateTime _mediaCreationTime;
        private DateTime _scriptCreationTime;
        private bool _playing;
        private ImageSource _preview;
        private string _displayTitle;
        private string _tagsDisplay;
        private List<string> _tagItems = new List<string> { "未设置" };
        private List<string> _rawTagItems = new List<string>();
        private bool _isFavorite;
        private string _sourceUrl;
        private string _nfoPath;
        private string _typeDisplay;
        private int _scriptVariantCount;
        private string _selectedScriptName;
        private string _scriptDurationDisplay = string.Empty;
        private string _scriptActionCountDisplay = string.Empty;
        private string _scriptMaxSpeedDisplay = string.Empty;
        private string _scriptAverageSpeedDisplay = string.Empty;
        private NfoMetadata _localMetadata;
        private string _metadataReferenceFile;
        private string _metadataMediaFile;
        private string _metadataScriptFile;
        private string[] _metadataScriptVariants = new string[0];

        public PlaylistEntry()
        {
            Status = PlaylistEntryStatus.Loading;
        }

        public PlaylistEntry(string filename)
        {
            Fullname = filename;
        }

        public string Shortname
        {
            get => _shortname;
            set
            {
                if (value == _shortname) return;
                _shortname = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }

        public string DisplayTitle
        {
            get => string.IsNullOrWhiteSpace(_displayTitle) ? Shortname : _displayTitle;
            set
            {
                if (value == _displayTitle) return;
                _displayTitle = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan? _pausedExitPosition;
        public TimeSpan? PausedExitPosition
        {
            get => _pausedExitPosition;
            set
            {
                if (_pausedExitPosition == value) return;
                _pausedExitPosition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PausedExitDisplay));
            }
        }

        public string PausedExitDisplay => PausedExitPosition.HasValue
            ? Localization.ChineseLocalization.Translate("上次退出 ") + ((int)PausedExitPosition.Value.TotalHours).ToString("00") + PausedExitPosition.Value.ToString(@"\:mm\:ss")
            : Localization.ChineseLocalization.Translate("暂无退出记录");
        public void RefreshInterfaceLanguage() => OnPropertyChanged(nameof(PausedExitDisplay));

        public string TagsDisplay
        {
            get => _tagsDisplay;
            private set
            {
                if (value == _tagsDisplay) return;
                _tagsDisplay = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TagsDisplayText));
            }
        }

        public string TagsDisplayText => string.IsNullOrWhiteSpace(TagsDisplay)
            ? "标签：未设置"
            : "标签：" + TagsDisplay;

        public List<string> TagItems
        {
            get => _tagItems;
            private set
            {
                _tagItems = value ?? new List<string> { "未设置" };
                OnPropertyChanged();
            }
        }

        public List<string> RawTagItems
        {
            get => _rawTagItems;
            private set
            {
                _rawTagItems = value ?? new List<string>();
                OnPropertyChanged();
                OnPropertyChanged(nameof(RawTagsDisplay));
            }
        }

        public string RawTagsDisplay => string.Join(" ", RawTagItems);

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (value == _isFavorite) return;
                _isFavorite = value;
                OnPropertyChanged();
            }
        }

        public string SourceUrl
        {
            get => _sourceUrl;
            private set
            {
                if (value == _sourceUrl) return;
                _sourceUrl = value;
                OnPropertyChanged();
            }
        }

        public string NfoPath
        {
            get => _nfoPath;
            private set
            {
                if (value == _nfoPath) return;
                _nfoPath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNfo));
            }
        }

        public bool HasNfo => !string.IsNullOrWhiteSpace(NfoPath) && File.Exists(NfoPath);

        public string TypeDisplay
        {
            get => _typeDisplay;
            private set
            {
                if (value == _typeDisplay) return;
                _typeDisplay = value;
                OnPropertyChanged();
            }
        }

        public int ScriptVariantCount
        {
            get => _scriptVariantCount;
            set
            {
                if (value == _scriptVariantCount) return;
                _scriptVariantCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScriptVariantDisplay));
            }
        }

        public string SelectedScriptName
        {
            get => _selectedScriptName;
            set
            {
                if (value == _selectedScriptName) return;
                _selectedScriptName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScriptVariantDisplay));
                OnPropertyChanged(nameof(ScriptFileDisplay));
                ApplyScriptStatistics();
            }
        }

        public string ScriptFileDisplay => string.IsNullOrWhiteSpace(SelectedScriptName)
            ? "未找到脚本"
            : SelectedScriptName;

        // Per-funscript values loaded from the video's local NFO sidecar.
        public string ScriptDurationDisplay
        {
            get => _scriptDurationDisplay;
            set { if (value == _scriptDurationDisplay) return; _scriptDurationDisplay = value; OnPropertyChanged(); }
        }

        public string ScriptActionCountDisplay
        {
            get => _scriptActionCountDisplay;
            set { if (value == _scriptActionCountDisplay) return; _scriptActionCountDisplay = value; OnPropertyChanged(); }
        }

        public string ScriptMaxSpeedDisplay
        {
            get => _scriptMaxSpeedDisplay;
            set { if (value == _scriptMaxSpeedDisplay) return; _scriptMaxSpeedDisplay = value; OnPropertyChanged(); }
        }

        public string ScriptAverageSpeedDisplay
        {
            get => _scriptAverageSpeedDisplay;
            set { if (value == _scriptAverageSpeedDisplay) return; _scriptAverageSpeedDisplay = value; OnPropertyChanged(); }
        }

        public string ScriptVariantDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SelectedScriptName))
                    return "脚本：" + SelectedScriptName;
                if (ScriptVariantCount > 1)
                    return "可选脚本 × " + ScriptVariantCount;
                return ScriptVariantCount == 1 ? "脚本 × 1" : "无脚本";
            }
        }

        public string Fullname
        {
            get => _fullname;
            set
            {
                if (value == _fullname) return;
                _fullname = value;
                Shortname = System.IO.Path.GetFileNameWithoutExtension(_fullname);
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged();
            }
        }

        public bool Playing
        {
            get => _playing;
            set
            {
                if (value == _playing) return;
                _playing = value;
                OnPropertyChanged();
            }
        }

        public bool Removed
        {
            get => _removed;
            set
            {
                if (value == _removed) return;
                _removed = value;
                OnPropertyChanged();
            }
        }

        public PlaylistEntryStatus Status
        {
            get => _status;
            protected set
            {
                if (value == _status) return;
                _status = value;
                OnPropertyChanged();
            }
        }

        public ImageSource HeatMap
        {
            get => _heatMap;
            set
            {
                if (Equals(value, _heatMap)) return;
                _heatMap = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan? Duration
        {
            get => _duration;
            set
            {
                if (value == _duration) return;
                _duration = value;
                OnPropertyChanged();
            }
        }

        public bool HasMedia
        {
            get => _hasMedia;
            set
            {
                if (value == _hasMedia) return;
                _hasMedia = value;
                OnPropertyChanged();
            }
        }

        public DateTime MediaCreationTime
        {
            get => _mediaCreationTime;
            set
            {
                if (value.Equals(_mediaCreationTime)) return;
                _mediaCreationTime = value;
                OnPropertyChanged();
            }
        }

        public DateTime ScriptCreationTime
        {
            get => _scriptCreationTime;
            set
            {
                if (value.Equals(_scriptCreationTime)) return;
                _scriptCreationTime = value;
                OnPropertyChanged();
            }
        }

        public ImageSource Preview  
        {
            get => _preview;
            set
            {
                if (Equals(value, _preview)) return;
                _preview = value;
                OnPropertyChanged();
            }
        }

        public void UpdateStatus()
        {
            Status = HasMedia && HasScript ? PlaylistEntryStatus.FilesOk : PlaylistEntryStatus.MissingFile;
        }

        public void Reset()
        {
            Status = PlaylistEntryStatus.Loading;
            Duration = null;
            HeatMap = null;
            Preview = null;
            ScriptVariantCount = 0;
            _localMetadata = null;
            _metadataReferenceFile = null;
            SelectedScriptName = null;
            ScriptDurationDisplay = string.Empty;
            ScriptActionCountDisplay = string.Empty;
            ScriptMaxSpeedDisplay = string.Empty;
            ScriptAverageSpeedDisplay = string.Empty;
        }

        public void LoadLocalMetadata(string mediaFile, string scriptFile, IEnumerable<string> scriptVariants)
        {
            string referenceFile = !string.IsNullOrWhiteSpace(mediaFile) ? mediaFile :
                (!string.IsNullOrWhiteSpace(scriptFile) ? scriptFile : Fullname);
            _metadataReferenceFile = referenceFile;
            _metadataMediaFile = mediaFile;
            _metadataScriptFile = scriptFile;
            _metadataScriptVariants = scriptVariants?.ToArray();
            NfoMetadata metadata = NfoMetadata.Load(referenceFile);
            ApplyMetadataProjection(metadata, metadata?.SidecarPath ?? NfoMetadata.GetSidecarPath(referenceFile));
        }

        public NfoMetadata CreateMetadataDraft()
        {
            if (string.IsNullOrWhiteSpace(_metadataReferenceFile))
                throw new InvalidOperationException("媒体元数据尚未加载，请稍后重试。");
            return _localMetadata?.CopyForEdit() ?? new NfoMetadata
            {
                Title = Path.GetFileNameWithoutExtension(_metadataReferenceFile),
                OriginalTitle = Path.GetFileNameWithoutExtension(_metadataReferenceFile),
                Type = "视频"
            };
        }

        public void ApplyCommittedMetadata(NfoMetadata metadata)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.SidecarPath)) throw new ArgumentException("缺少已提交的 NFO。");
            if (string.IsNullOrWhiteSpace(_metadataReferenceFile)) throw new InvalidOperationException("媒体元数据匹配尚未完成。");
            ApplyMetadataProjection(metadata.CopyForEdit(), metadata.SidecarPath);
        }

        private void ApplyMetadataProjection(NfoMetadata metadata, string sidecarPath)
        {
            _localMetadata = metadata;
            NfoPath = sidecarPath;
            DisplayTitle = metadata?.Title;
            SourceUrl = metadata?.SourceUrl;
            RawTagItems = metadata?.Tags == null
                ? new List<string>()
                : metadata.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            List<string> translatedTags = RawTagItems.Select(ChineseTagTranslator.Translate)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            TagsDisplay = string.Join("  ", translatedTags.Select(tag => "#" + tag));
            TagItems = translatedTags.Count == 0 ? new List<string> { "未设置" } : translatedTags;
            IsFavorite = metadata?.Favorite ?? false;

            List<string> parts = new List<string>();
            string mediaExtension = ExtensionLabel(_metadataMediaFile);
            string scriptExtension = ExtensionLabel(_metadataScriptFile);
            if (!string.IsNullOrWhiteSpace(mediaExtension)) parts.Add(mediaExtension);
            if (!string.IsNullOrWhiteSpace(scriptExtension)) parts.Add(scriptExtension);
            TypeDisplay = !string.IsNullOrWhiteSpace(metadata?.Type)
                ? metadata.Type + (parts.Count > 0 ? " · " + string.Join(" + ", parts) : string.Empty)
                : string.Join(" + ", parts);

            ScriptVariantCount = _metadataScriptVariants?.Length ?? (string.IsNullOrWhiteSpace(_metadataScriptFile) ? 0 : 1);
            if (string.IsNullOrWhiteSpace(SelectedScriptName) && !string.IsNullOrWhiteSpace(_metadataScriptFile))
                SelectedScriptName = Path.GetFileName(_metadataScriptFile);

            ApplyScriptStatistics();
        }

        private void ApplyScriptStatistics()
        {
            NfoScriptStatistics statistics = _localMetadata?.FindScript(SelectedScriptName);
            ScriptDurationDisplay = statistics?.Duration ?? string.Empty;
            ScriptActionCountDisplay = statistics?.ActionCount?.ToString() ?? string.Empty;
            ScriptMaxSpeedDisplay = statistics?.MaxSpeed?.ToString() ?? string.Empty;
            ScriptAverageSpeedDisplay = statistics?.AverageSpeed?.ToString() ?? string.Empty;
        }

        private static string ExtensionLabel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            string extension = Path.GetExtension(path);
            return string.IsNullOrWhiteSpace(extension) ? null : extension.TrimStart('.').ToUpperInvariant();
        }

        public bool HasScript
        {
            get => _hasScript;
            set
            {
                if (value == _hasScript) return;
                _hasScript = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public enum PlaylistEntryStatus
    {
        Loading,
        MissingFile,
        FilesOk
    }
}
