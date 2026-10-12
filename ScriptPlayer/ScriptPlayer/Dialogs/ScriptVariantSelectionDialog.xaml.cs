using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ScriptPlayer.Dialogs
{
    public partial class ScriptVariantSelectionDialog : Window
    {
        public string SelectedScript { get; private set; }

        public ScriptVariantSelectionDialog(IEnumerable<string> scripts, string previousSelection = null)
        {
            InitializeComponent();
            List<ScriptVariantItem> items = scripts.Select(path => new ScriptVariantItem(path)).ToList();
            lstVariants.ItemsSource = items;
            lstVariants.SelectedItem = items.FirstOrDefault(item =>
                string.Equals(item.FullPath, previousSelection, System.StringComparison.InvariantCultureIgnoreCase)) ?? items.FirstOrDefault();
        }

        private void BtnSelect_OnClick(object sender, RoutedEventArgs e)
        {
            SelectCurrent();
        }

        private void LstVariants_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SelectCurrent();
        }

        private void SelectCurrent()
        {
            ScriptVariantItem item = lstVariants.SelectedItem as ScriptVariantItem;
            if (item == null)
                return;
            SelectedScript = item.FullPath;
            DialogResult = true;
        }
    }

    public class ScriptVariantItem
    {
        public ScriptVariantItem(string fullPath)
        {
            FullPath = fullPath;
            FileName = Path.GetFileName(fullPath);
        }

        public string FileName { get; private set; }
        public string FullPath { get; private set; }
    }
}
