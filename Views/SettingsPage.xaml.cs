using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private readonly MainViewModel _viewModel;

        public SettingsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            // Theme settings
            UpdateThemeButtonText();
            
            // Speech settings
            IntervalSlider.Value = _viewModel.ReadInterval;
            IntervalText.Text = $"{_viewModel.ReadInterval}秒";
            SpeechEngineComboBox.SelectedIndex = _viewModel.Settings.Settings.SpeechEngine;
            RandomOrderCheckBox.IsChecked = _viewModel.Settings.Settings.IsRandomOrder;
            
            // Security settings
            EncryptionCheckBox.IsChecked = _viewModel.Settings.Settings.EncryptionEnabled;
            
            // Privacy settings
            DataCollectionCheckBox.IsChecked = _viewModel.Settings.Settings.AllowDataCollection;
            CloudSyncCheckBox.IsChecked = _viewModel.Settings.Settings.AllowCloudSync;
            AnalyticsCheckBox.IsChecked = _viewModel.Settings.Settings.AllowAnalytics;
            ShareStatsCheckBox.IsChecked = _viewModel.Settings.Settings.ShareLearningStats;
            
            // Version info
            VersionLabel.Text = $"当前版本: v2.7.0";
        }

        private void UpdateThemeButtonText()
        {
            if (_viewModel == null) return;
            ThemeToggleButton.Content = _viewModel.IsDarkTheme ? "切换到浅色主题" : "切换到深色主题";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame == null) return;
            Frame?.Navigate(typeof(HomePage));
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            await _viewModel.Settings.SaveSettingsAsync();
            Frame?.Navigate(typeof(HomePage));
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.IsDarkTheme = !_viewModel.IsDarkTheme;
            _viewModel.Settings.Settings.IsDarkTheme = _viewModel.IsDarkTheme;
            UpdateThemeButtonText();
            
            // Apply theme to application
            var window = this.XamlRoot.Content as FrameworkElement;
            if (window != null)
            {
                if (_viewModel.IsDarkTheme)
                {
                    window.RequestedTheme = ElementTheme.Dark;
                }
                else
                {
                    window.RequestedTheme = ElementTheme.Light;
                }
            }
            
            // Save settings
            _ = _viewModel.Settings.SaveSettingsAsync();
        }

        private void IntervalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_viewModel == null) return;
            
            var value = (int)e.NewValue;
            IntervalText.Text = $"{value}秒";
            _viewModel.ReadInterval = value;
        }

        private void SpeechEngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel == null || SpeechEngineComboBox.SelectedItem == null) return;
            
            if (SpeechEngineComboBox.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (int.TryParse(tag, out var engine))
                {
                    _viewModel.Settings.Settings.SpeechEngine = engine;
                }
            }
        }

        private void RandomOrderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.IsRandomOrder = true;
        }

        private void RandomOrderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.IsRandomOrder = false;
        }

        private void EncryptionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.EncryptionEnabled = true;
        }

        private void EncryptionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.EncryptionEnabled = false;
        }

        private void DataCollectionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowDataCollection = true;
        }

        private void DataCollectionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowDataCollection = false;
        }

        private void CloudSyncCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowCloudSync = true;
        }

        private void CloudSyncCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowCloudSync = false;
        }

        private void AnalyticsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowAnalytics = true;
        }

        private void AnalyticsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.AllowAnalytics = false;
        }

        private void ShareStatsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.ShareLearningStats = true;
        }

        private void ShareStatsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            _viewModel.Settings.Settings.ShareLearningStats = false;
        }

        private async void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "初始化程序",
                Content = "程序将初始化，所有数据将被删除！",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // Clear all data
                var appDataPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EnglishListen");
                if (System.IO.Directory.Exists(appDataPath))
                {
                    System.IO.Directory.Delete(appDataPath, true);
                }
                Frame?.Navigate(typeof(HomePage));
            }
        }

        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var updateService = new Services.UpdateService();
            await updateService.CheckForUpdatesAsync(new ContentDialog { XamlRoot = this.XamlRoot });
        }

        private async void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement voice preview logic
            var dialog = new ContentDialog
            {
                Title = "试听",
                Content = "语音预览功能将在未来版本中实现",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
