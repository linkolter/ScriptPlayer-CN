using System.Windows;
using System.Windows.Controls;
using ScriptPlayer.Generators;

namespace ScriptPlayer.Dialogs
{
    /// <summary>
    /// Interaction logic for ThumbnailBannerGeneratorSettingsDialog.xaml
    /// </summary>
    public partial class ThumbnailBannerGeneratorSettingsDialog : Window
    {
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            "Settings", typeof(ThumbnailBannerGeneratorSettings), typeof(ThumbnailBannerGeneratorSettingsDialog), new PropertyMetadata(default(ThumbnailBannerGeneratorSettings)));

        public ThumbnailBannerGeneratorSettings Settings
        {
            get => (ThumbnailBannerGeneratorSettings) GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        public ThumbnailBannerGeneratorSettingsDialog(ThumbnailBannerGeneratorSettings initialSettings)
        {
            Settings = initialSettings?.Duplicate() ?? new ThumbnailBannerGeneratorSettings();
            InitializeComponent();
        }

        private void btnPreview_Click(object sender, RoutedEventArgs e)
        {
            PreviewThumbnailBanner(Settings);
        }

        private async void PreviewThumbnailBanner(ThumbnailBannerGeneratorSettings settings)
        {
            await InlineDialogService.ShowAsync(new ThumbnailBannerGeneratorSettingsPreviewDialog(settings));
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            InlineDialogService.Complete(this, false);
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            ((Button) sender).Focus();
            InlineDialogService.Complete(this, true);
        }
    }
}
