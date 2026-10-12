using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ScriptPlayer.Localization;
using ScriptPlayer.Shared;
using ScriptPlayer.Shared.Controls;
using ScriptPlayer.ViewModels;

namespace ScriptPlayer.Dialogs
{
    public partial class SettingsDialog
    {
        private bool unified;
        private DependencyPropertyDescriptor audioSelectionDescriptor;
        private FrameworkElement[] unifiedLanguagePages = new FrameworkElement[0];
        private readonly HashSet<DataGrid> pendingLanguageTables = new HashSet<DataGrid>();

        /// <summary>Refreshes display projections without replacing mappings or applying settings.</summary>
        public void RefreshLanguagePresentation()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(RefreshLanguagePresentation));
                return;
            }
            if (InputMappings != null)
                foreach (var mapping in InputMappings)
                {
                    ScriptplayerCommand command;
                    if (mapping.CommandId != null && GlobalCommandManager.Commands.TryGetValue(mapping.CommandId, out command))
                        mapping.DisplayText = ChineseLocalization.Translate(command.DisplayText);
                }
            RefreshPageNames(Pages);
            SettingsTitle = BuildSettingsPath();
            var visited = new HashSet<DependencyObject>();
            RefreshLanguageItems(this, visited);
            foreach (var page in unifiedLanguagePages) RefreshLanguageItems(page, visited);
        }

        private static void RefreshPageNames(SettingsPageViewModelCollection pages)
        {
            if (pages == null) return;
            foreach (var page in pages)
            {
                string id = page.SettingsId;
                if (!string.IsNullOrEmpty(id))
                    page.Name = ChineseLocalization.Translate(id.Substring(id.LastIndexOf('/') + 1));
                RefreshPageNames(page.SubSettings);
            }
        }

        private void RefreshLanguageItems(DependencyObject root, ISet<DependencyObject> visited)
        {
            if (root == null || !visited.Add(root)) return;
            var table = root as DataGrid;
            if (table != null && InputMappings != null && ReferenceEquals(table.ItemsSource, InputMappings))
                RefreshLanguageTable(table);
            var selector = root as ListBox;
            if (selector != null && ((Pages != null && ReferenceEquals(selector.ItemsSource, Pages))
                || (FilteredPages != null && ReferenceEquals(selector.ItemsSource, FilteredPages))))
                selector.Items.Refresh();
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray())
                RefreshLanguageItems(child, visited);
        }

        private void RefreshLanguageTable(DataGrid table)
        {
            // DisplayText is an auto-property despite the row implementing INotifyPropertyChanged.
            // Refresh after a user edit completes; language changes must not commit/cancel that edit.
            var editable = table.Items as IEditableCollectionView;
            if (editable != null && (editable.IsEditingItem || editable.IsAddingNew))
            {
                if (pendingLanguageTables.Add(table)) table.RowEditEnding += LanguageTable_RowEditEnding;
                return;
            }
            if (pendingLanguageTables.Remove(table)) table.RowEditEnding -= LanguageTable_RowEditEnding;
            table.Items.Refresh();
        }

        private void LanguageTable_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.Cancel) return;
            var table = (DataGrid)sender;
            table.Dispatcher.BeginInvoke(new Action(() => RefreshLanguageTable(table)));
        }

        // Reuse the existing editors and their validators, not the old dialog layout or draft model.
        public Dictionary<string, FrameworkElement> CreateUnifiedPages(Window owner, SettingsViewModel liveSettings)
        {
            Owner = owner;
            unified = true;
            Settings = liveSettings;
            LoadAdditionalPaths();
            var pages = PageSelector.Elements.Cast<FrameworkElement>().ToArray();
            unifiedLanguagePages = pages;
            PageSelector.Elements.Clear();
            Content = null;
            txtPasswordVlcPassword.Password = Settings.VlcPassword;
            txtPasswordKodiPassword.Password = Settings.KodiPassword;
            var result = new Dictionary<string, FrameworkElement>();
            foreach (var page in pages)
            {
                string id = PageSelector.GetContentIdentifier(page);
                page.DataContext = this;
                foreach (object key in Resources.Keys) page.Resources[key] = Resources[key];
                foreach (var dictionary in Resources.MergedDictionaries)
                    page.Resources.MergedDictionaries.Add(dictionary);
                page.SetResourceReference(TextElement.FontFamilyProperty, "MediaFontFamily");
                page.SetResourceReference(TextElement.FontSizeProperty, "MediaBodyFontSize");
                page.Margin = new Thickness(0);
                NormalizeEditor(page, owner);
                page.Height = double.NaN;
                page.HorizontalAlignment = HorizontalAlignment.Stretch;
                if (id == "Input Mappings") page.Height = 440;
                if (id == "Paths" || id == "Favourite Folders") RepairCollectionPage((Grid)page);
                if (id == "Players") GroupPlayerConnections((Grid)page);
                page.LostKeyboardFocus += (s,e) => CommitUnifiedCollections();
                ChineseLocalization.TranslateTree(page);
                NameSettingsSwitches(page);
                result[id] = page;
            }
            AdditionalPaths.CollectionChanged += (s,e) => CommitUnifiedCollections();
            InputMappings.CollectionChanged += (s,e) => CommitUnifiedCollections();
            audioSelectionDescriptor = DependencyPropertyDescriptor.FromProperty(SelectedAudioDeviceProperty, typeof(SettingsDialog));
            audioSelectionDescriptor.AddValueChanged(this, UnifiedAudioChanged);
            return result;
        }

        private void UnifiedAudioChanged(object sender, EventArgs e)
        {
            Settings.EstimAudioDevice = SelectedAudioDevice?.DeviceId ?? "";
        }

        public void CommitUnifiedCollections()
        {
            if (!unified) return;
            var paths = AdditionalPaths.Select(p => p.ToPath()).ToArray();
            if (!Settings.AdditionalPaths.SequenceEqual(paths))
                Settings.AdditionalPaths = new ObservableCollection<string>(paths);
            ApplyInputMappings();
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }

        public void ReleaseUnifiedPages()
        {
            if (!unified) return;
            CommitUnifiedCollections();
            unified = false;
            audioSelectionDescriptor?.RemoveValueChanged(this, UnifiedAudioChanged);
        }

        private static void RepairCollectionPage(Grid page)
        {
            page.RowDefinitions.Clear();
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            page.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            foreach (FrameworkElement child in page.Children.Cast<FrameworkElement>().ToArray())
            {
                child.Margin = new Thickness(0, 0, 0, 12);
                if (child is TextBlock) Grid.SetRow(child, 0);
                else if (child is Button)
                {
                    page.Children.Remove(child);
                    child.Margin = new Thickness(8, 0, 0, 0);
                    actions.Children.Add(child);
                }
                else { Grid.SetRow(child, 1); child.VerticalAlignment = VerticalAlignment.Stretch; }
            }
            Grid.SetRow(actions, 2);
            page.Children.Add(actions);
        }

        private static void GroupPlayerConnections(Grid page)
        {
            var sections = page.Children.OfType<StackPanel>().FirstOrDefault();
            if (sections == null) return;
            foreach (var panel in sections.Children.OfType<StackPanel>().ToArray())
            {
                var heading = panel.Children.OfType<TextBlock>().FirstOrDefault();
                if (heading == null) continue;
                int index = sections.Children.IndexOf(panel);
                sections.Children.Remove(panel);
                panel.Children.Remove(heading);
                panel.Margin = new Thickness(14, 0, 14, 16);
                sections.Children.Insert(index, new Expander { Header = heading.Text, Content = panel, Margin = new Thickness(0,0,0,8) });
            }
        }

        private static void NormalizeEditor(DependencyObject root, Window owner)
        {
            if (root is Control control && !UsesSymbolFont(control.FontFamily))
                control.SetResourceReference(Control.FontFamilyProperty, "MediaFontFamily");
            if (root is Button button)
            {
                button.Height = double.NaN;
                button.MinHeight = 36;
                button.MinWidth = 36;
                button.Width = double.NaN;
                if (button.Content is string caption) button.Content = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap };
                button.Padding = new Thickness(12,7,12,7);
            }
            if (root is TextBox text)
            {
                text.Height = double.NaN;
                text.MinHeight = 36;
                if (!double.IsNaN(text.Width) && text.Width < 60) text.Width = 72;
            }
            if (root is PasswordBox password) { password.Height = double.NaN; password.MinHeight = 36; }
            if (root is TextBlock label)
            {
                if (!UsesSymbolFont(label.FontFamily))
                    label.SetResourceReference(TextBlock.FontFamilyProperty, "MediaFontFamily");
                label.VerticalAlignment = VerticalAlignment.Center;
                label.TextWrapping = TextWrapping.Wrap;
                if (label.Text == "attempts with a") label.Text = "次尝试，间隔";
                if (label.Text == "second delay") label.Text = "秒";
            }
            if (root is CheckBox check)
            {
                bool hasLabel = check.Content != null;
                check.Style = (Style)owner.FindResource(hasLabel ? "SettingsToggleSwitch" : "ToggleSwitch");
                check.Width = double.NaN;
                check.Height = double.NaN;
                check.HorizontalAlignment = HorizontalAlignment.Stretch;
                check.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                check.Margin = hasLabel ? new Thickness(0,0,0,8) : new Thickness(8,0,0,0);
                if (check.Content is string captionText)
                    check.Content = new TextBlock { Text = captionText, TextWrapping = TextWrapping.Wrap };
            }
            if (root is DataGrid table)
            {
                foreach (var column in table.Columns.OfType<DataGridTextColumn>())
                {
                    var cellText = new Style(typeof(TextBlock));
                    cellText.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
                    cellText.Setters.Add(new Setter(TextBlock.FontFamilyProperty, owner.FindResource("MediaFontFamily")));
                    cellText.Setters.Add(new Setter(TextBlock.FontSizeProperty, owner.FindResource("MediaBodyFontSize")));
                    column.ElementStyle = cellText;
                }
            }
            if (root is Grid grid)
                foreach (var row in grid.RowDefinitions)
                    if (row.Height.IsAbsolute && row.Height.Value < 40) row.Height = GridLength.Auto;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray())
                NormalizeEditor(child, owner);
        }

        private static bool UsesSymbolFont(FontFamily font)
        {
            string name = font?.Source ?? "";
            return name.IndexOf("MDL2", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Webdings", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Wingdings", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Awesome", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void NameSettingsSwitches(DependencyObject root)
        {
            if (root is CheckBox check)
            {
                string name = check.Content is TextBlock label ? label.Text : check.Content as string;
                if (!string.IsNullOrWhiteSpace(name))
                    System.Windows.Automation.AutomationProperties.SetName(check, name);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().ToArray())
                NameSettingsSwitches(child);
        }
    }
}
