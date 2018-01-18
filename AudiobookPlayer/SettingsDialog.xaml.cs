// File: AudiobookPlayer/AudiobookPlayer/SettingsDialog.xaml.cs
// User: Adrian Hum/
// 
// Created:  2018-01-18 10:37 AM
// Modified: 2018-01-18 11:25 AM

using System.Windows;
using System.Windows.Controls;

namespace AudiobookPlayer
{
    /// <summary>
    ///     Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public static readonly DependencyProperty BackgroundThreadsProperty =
            DependencyProperty.Register("BackgroundThreads", typeof(int), typeof(SettingsDialog),
                new UIPropertyMetadata(2));

        public SettingsDialog(Config currentConfig)
        {
            InitializeComponent();
            Config = currentConfig;
            DataContext = Config;
        }

        public Config Config { get; }

        private bool ValidateTextFields()
        {
            var hasErrors = false;
            hasErrors = Validation.GetHasError(txtAudiobookPath);
            hasErrors = hasErrors || Validation.GetHasError(txtSmallSkipSeconds);
            hasErrors = hasErrors || Validation.GetHasError(txtLargeSkipSeconds);
            hasErrors = hasErrors || Validation.GetHasError(txtUpdateIntervallSeconds);
            hasErrors = hasErrors || Validation.GetHasError(txtBackgroundThreads);
            return hasErrors;
        }

        private void cmdOk_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateTextFields())
                MessageBox.Show("At least one text fields contains errors.");
            else
                DialogResult = true;
        }

        private void cmdAbort_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void BrowseForFolderClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
            }
        }
    }
}