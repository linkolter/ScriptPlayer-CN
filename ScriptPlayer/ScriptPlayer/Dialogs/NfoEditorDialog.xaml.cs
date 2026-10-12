using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using ScriptPlayer.Metadata;
using ScriptPlayer.Localization;

namespace ScriptPlayer.Dialogs
{
    public partial class NfoEditorDialog : Window
    {
        private readonly string _mediaOrScriptPath;
        private readonly NfoMetadata _loadedMetadata;
        public NfoMetadata SavedMetadata { get; private set; }

        public NfoEditorDialog(string mediaOrScriptPath) : this(mediaOrScriptPath, null) { }

        public NfoEditorDialog(string mediaOrScriptPath, NfoMetadata loadedSnapshot)
        {
            _mediaOrScriptPath = mediaOrScriptPath;
            InitializeComponent();

            _loadedMetadata = loadedSnapshot?.CopyForEdit() ?? NfoMetadata.Load(mediaOrScriptPath) ?? new NfoMetadata
            {
                Title = Path.GetFileNameWithoutExtension(mediaOrScriptPath)
            };
            txtNfoPath.Text = _loadedMetadata.SidecarPath ?? NfoMetadata.GetSidecarPath(mediaOrScriptPath);
            Apply(_loadedMetadata);
        }

        private void Apply(NfoMetadata metadata)
        {
            txtTitle.Text = metadata.Title ?? string.Empty;
            txtSource.Text = metadata.SourceUrl ?? string.Empty;
            txtType.Text = metadata.Type ?? string.Empty;
            txtTags.Text = string.Join(", ", metadata.Tags ?? Enumerable.Empty<string>());
            txtPlot.Text = metadata.Plot ?? string.Empty;
        }

        private async void BtnFetch_OnClick(object sender, RoutedEventArgs e)
        {
            btnFetch.IsEnabled = false;
            txtStatus.Text = "正在读取 EroScripts 主题……";
            try
            {
                NfoMetadata remote = await EroScriptsMetadataClient.FetchAsync(txtSource.Text.Trim());
                string existingPlot = txtPlot.Text;
                Apply(remote);
                if (string.IsNullOrWhiteSpace(remote.Plot))
                    txtPlot.Text = existingPlot;
                txtStatus.Text = "读取成功：已导入 " + remote.Tags.Count + " 个标签。";
            }
            catch (ArgumentException ex)
            {
                txtStatus.Text = ex.Message;
            }
            catch (HttpRequestException ex)
            {
                txtStatus.Text = "无法连接 EroScripts：" + ex.Message;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "读取失败：" + ex.Message;
            }
            finally
            {
                btnFetch.IsEnabled = true;
            }
        }

        private void BtnSave_OnClick(object sender, RoutedEventArgs e)
        {
            if (SaveChanges()) DialogResult = true;
        }

        public bool SaveChanges()
        {
            try
            {
                NfoMetadata metadata = _loadedMetadata.CopyForEdit();
                metadata.Title = txtTitle.Text.Trim();
                metadata.SourceUrl = txtSource.Text.Trim();
                metadata.Type = txtType.Text.Trim();
                metadata.Plot = txtPlot.Text.Trim();
                metadata.Tags = txtTags.Text
                        .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(tag => tag.Trim().TrimStart('#'))
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(StringComparer.InvariantCultureIgnoreCase)
                        .ToList();

                txtNfoPath.Text = metadata.Save(_mediaOrScriptPath);
                SavedMetadata = metadata;
                txtStatus.Text = string.IsNullOrWhiteSpace(metadata.LastSaveWarning)
                    ? ChineseLocalization.TranslateFormat("NFO 已保存：{0}", metadata.SidecarPath)
                    : ChineseLocalization.TranslateFormat("NFO 已保存，但有提示：{0}", metadata.SidecarPath + Environment.NewLine + metadata.LastSaveWarning);
                return true;
            }
            catch (Exception ex)
            {
                txtStatus.Text = ChineseLocalization.TranslateFormat("保存 NFO 失败：{0}", ex.Message);
                return false;
            }
        }
    }
}
