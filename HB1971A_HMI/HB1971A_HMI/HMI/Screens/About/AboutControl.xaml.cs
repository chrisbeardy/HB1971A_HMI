using HBControls.Enums;
using HBControls.Themes;
using System.Reflection;
using System.Diagnostics;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Messaging;
using HBMessages.HBMessages.HBLanguage;
using HBMessages.HBHelperMethods;
using HBCommon;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace HB1971A_HMI.Screens.About
{
    /// <summary>
    /// Interaction logic for AboutControl.xaml
    /// </summary>
    public partial class AboutControl : UserControl, IRecipient<LangaugesLoaded>
    {
        private string _currentLanguage = "English";
        private bool _suppressLanguageChange;
        private readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true };

        /// <summary>
        /// Initializes a new instance of the AboutControl class.
        /// </summary>
        public AboutControl()
        {
            InitializeComponent();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Content = version != null ? version.ToString() : "1.0.0.0";

            _suppressLanguageChange = true;
            LanguageComboBox.Items.Add(_currentLanguage);
            LanguageComboBox.SelectedItem = _currentLanguage;
            _suppressLanguageChange = false;
            StrongReferenceMessenger.Default.RegisterAll(this);
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item &&
                Enum.TryParse<Theme>(item.Content?.ToString(), out var theme))
                ThemeManager.ApplyTheme(theme);
        }

        /// <summary>
        /// Repopulates the language list when the available languages are loaded,
        /// keeping the current language selected.
        /// </summary>
        public void Receive(LangaugesLoaded message)
        {
            Dispatcher.Invoke(() =>
            {
                _suppressLanguageChange = true;
                LanguageComboBox.Items.Clear();
                foreach (string language in message.Value.Languages)
                    LanguageComboBox.Items.Add(language);
                if (LanguageComboBox.Items.Contains(_currentLanguage))
                    LanguageComboBox.SelectedItem = _currentLanguage;
                else if (LanguageComboBox.Items.Count > 0)
                    LanguageComboBox.SelectedIndex = 0;
                _suppressLanguageChange = false;
            });
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLanguageChange) return;
            if (LanguageComboBox.SelectedItem is string language)
            {
                _currentLanguage = language;
                HBLang.SetLanguage(language);
            }
        }

        private void Website_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SavePersistentsButton_Click(object sender, RoutedEventArgs e)
        {
            const string plcReference = "PLC1";
            List<string> paths = HBPLC.GetPersistent(plcReference);
            if (paths.Count == 0)
            {
                MessageBox.Show("No persistent variables found.", "Save Persistent Data", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            Dictionary<string, HBObject> data = HBPLC.Read(plcReference, paths);
            string json = JsonSerializer.Serialize(data.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value), jsonOptions);
            SaveFileDialog dlg = new()
            {
                FileName = $"Persistents_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".json",
                Filter = "JSON files (*.json)|*.json"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show("Persistent data saved.", "Save Persistent Data", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
