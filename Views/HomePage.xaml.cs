using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly MainViewModel _viewModel;

        public HomePage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                WordListComboBox.ItemsSource = _viewModel.WordListFiles;
                if (_viewModel.WordListFiles.Count > 0)
                {
                    WordListComboBox.SelectedIndex = 0;
                }
                UpdateStats();
            }
        }

        private void UpdateStats()
        {
            if (_viewModel?.TestHistory != null && _viewModel.TestHistory.Count > 0)
            {
                StatsPanel.Visibility = Visibility.Visible;
                TotalTestsText.Text = $"总测试次数: {_viewModel.TestHistory.Count}";
                var avg = _viewModel.TestHistory.Average(t => t.Accuracy);
                AvgAccuracyText.Text = $"平均正确率: {avg:F1}%";
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ToggleThemeCommand.Execute(null);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(SettingsPage));
        }

        private void ViewWordsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(WordsPage));
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(TestPage));
        }

        private void ProgressButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(ProgressPage));
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".csv");

            var file = await picker.PickSingleFileAsync();
            if (file != null && _viewModel != null)
            {
                await _viewModel.ImportWordsFromFileAsync(file.Path);
                WordListComboBox.ItemsSource = _viewModel.WordListFiles;
            }
        }

        private async void WordListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WordListComboBox.SelectedItem != null && _viewModel != null)
            {
                await _viewModel.LoadWordsAsync(WordListComboBox.SelectedItem as string);
            }
        }
    }
}
