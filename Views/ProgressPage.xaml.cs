using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class ProgressPage : Page
    {
        private readonly MainViewModel _viewModel;

        public ProgressPage()
        {
            this.InitializeComponent();
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;
            Loaded += ProgressPage_Loaded;
        }

        private void ProgressPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStats();
        }

        private void LoadStats()
        {
            var history = _viewModel.TestHistory;
            TotalTestsText.Text = history.Count.ToString();

            if (history.Count > 0)
            {
                AvgAccuracyText.Text = $"{history.Average(h => h.Accuracy):F1}%";
                var totalWords = history.Sum(h => h.TotalWords);
                TotalWordsText.Text = totalWords.ToString();

                var streak = CalculateStreak(history);
                StreakDaysText.Text = streak.ToString();
            }

            HistoryListView.ItemsSource = _viewModel.TestHistoryViewModels;
        }

        private int CalculateStreak(System.Collections.Generic.List<English_Listen_WinUI.Models.TestResult> history)
        {
            if (history.Count == 0) return 0;

            var dates = history
                .Select(h => h.Timestamp.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToList();

            int streak = 1;
            var today = DateTime.Today;

            if (dates[0] != today && dates[0] != today.AddDays(-1))
                return 0;

            for (int i = 1; i < dates.Count; i++)
            {
                if (dates[i - 1].AddDays(-1) == dates[i])
                {
                    streak++;
                }
                else
                {
                    break;
                }
            }

            return streak;
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"test_history_{DateTime.Now:yyyyMMdd}"
            };
            picker.FileTypeChoices.Add("CSV", new[] { ".csv" });

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var lines = new System.Collections.Generic.List<string>
                {
                    "时间,词库名称,总单词数,正确数,正确率"
                };

                foreach (var result in _viewModel.TestHistory)
                {
                    lines.Add($"{result.Timestamp:yyyy-MM-dd HH:mm},{result.WordListName},{result.TotalWords},{result.CorrectCount},{result.Accuracy:F1}%");
                }

                await Windows.Storage.FileIO.WriteTextAsync(file, string.Join(Environment.NewLine, lines));
            }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "确认清空",
                Content = "确定要清空所有学习记录吗？此操作不可恢复。",
                PrimaryButtonText = "清空",
                CloseButtonText = "取消"
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _viewModel.TestHistory.Clear();
                await _viewModel.Settings.SaveTestHistoryAsync(_viewModel.TestHistory);
                LoadStats();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }
    }
}
