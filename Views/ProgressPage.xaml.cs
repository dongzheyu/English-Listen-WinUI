using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;
using English_Listen_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WinRT.Interop;
using Colors = Microsoft.UI.Colors;

namespace English_Listen_WinUI.Views
{
    public sealed partial class ProgressPage : Page
    {
        private readonly MainViewModel _viewModel;

        public ProgressPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += ProgressPage_Loaded;
        }

        private void ProgressPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadStats();
        }

        private void LoadStats()
        {
            if (_viewModel == null) return;

            var history = _viewModel.TestHistory;
            if (history == null)
            {
                TotalTestsText.Text = "0";
                return;
            }

            TotalTestsText.Text = history.Count.ToString();

            // ponytail: exclude learning records from accuracy stats/chart
            var dictationHistory = history.Where(h => h.RecordType != "learning").ToList();
            if (dictationHistory.Count > 0)
            {
                AvgAccuracyText.Text = $"{dictationHistory.Average(h => h.Accuracy):F1}%";
                var totalWords = history.Sum(h => h.TotalWords);
                TotalWordsText.Text = totalWords.ToString();

                var streak = CalculateStreak(history);
                StreakDaysText.Text = streak.ToString();

                DrawAccuracyChart(dictationHistory);
            }
            else
            {
                var totalWords = history.Sum(h => h.TotalWords);
                TotalWordsText.Text = totalWords.ToString();
                AvgAccuracyText.Text = "-";
                var streak = CalculateStreak(history);
                StreakDaysText.Text = streak.ToString();
            }

            ApplySort();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplySort();
        }

        private void ApplySort()
        {
            if (_viewModel?.TestHistory == null) return;

            var tag = (SortComboBox?.SelectedItem as ComboBoxItem)?.Tag as string;
            IEnumerable<TestResult> sorted = tag switch
            {
                "Oldest" => _viewModel.TestHistory.OrderBy(h => h.Timestamp),
                "Accuracy" => _viewModel.TestHistory.OrderByDescending(h => h.Accuracy),
                _ => _viewModel.TestHistory.OrderByDescending(h => h.Timestamp),
            };

            _viewModel.TestHistoryViewModels.Clear();
            foreach (var r in sorted)
                _viewModel.TestHistoryViewModels.Add(new TestResultViewModel { Result = r });

            HistoryListView.ItemsSource = _viewModel.TestHistoryViewModels;
        }

        private void DrawAccuracyChart(List<TestResult> history)
        {
            AccuracyChartCanvas.Children.Clear();

            var chartData = ChartService.GenerateAccuracyTrendData(history, 8);
            if (chartData.Count == 0) return;

            double canvasWidth = AccuracyChartCanvas.ActualWidth;
            double canvasHeight = AccuracyChartCanvas.ActualHeight;
            if (canvasWidth <= 0 || canvasHeight <= 0)
            {
                canvasWidth = 400;
                canvasHeight = 200;
            }

            double padding = 20;
            double chartWidth = canvasWidth - 2 * padding;
            double chartHeight = canvasHeight - 2 * padding;

            // Find max value for scaling
            double maxValue = chartData.Max(d => d.Value);
            if (maxValue == 0) maxValue = 100;

            // Draw grid lines and labels
            var gridBrush = new SolidColorBrush(Colors.LightGray);
            var textBrush = new SolidColorBrush(Colors.Gray);

            // Horizontal grid lines (0%, 25%, 50%, 75%, 100%)
            for (int i = 0; i <= 4; i++)
            {
                double y = padding + chartHeight - (i * chartHeight / 4);
                var line = new Line
                {
                    X1 = padding,
                    Y1 = y,
                    X2 = padding + chartWidth,
                    Y2 = y,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5
                };
                AccuracyChartCanvas.Children.Add(line);

                var label = new TextBlock
                {
                    Text = $"{i * 25}%",
                    FontSize = 10,
                    Foreground = textBrush,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                Canvas.SetLeft(label, 0);
                Canvas.SetTop(label, y - 8);
                AccuracyChartCanvas.Children.Add(label);
            }

            // Draw data points and lines
            var pointRadius = 4.0;
            var lineStroke = new SolidColorBrush(Colors.Blue);
            var pointStroke = new SolidColorBrush(Colors.White);

            List<Point> points = new List<Point>();
            for (int i = 0; i < chartData.Count; i++)
            {
                double x = padding + (i * chartWidth / Math.Max(1, chartData.Count - 1));
                double y = padding + chartHeight - (chartData[i].Value * chartHeight / maxValue);

                points.Add(new Point(x, y));

                // Draw point
                var ellipse = new Ellipse
                {
                    Width = pointRadius * 2,
                    Height = pointRadius * 2,
                    Fill = new SolidColorBrush(chartData[i].Color),
                    Stroke = pointStroke,
                    StrokeThickness = 1
                };
                Canvas.SetLeft(ellipse, x - pointRadius);
                Canvas.SetTop(ellipse, y - pointRadius);
                AccuracyChartCanvas.Children.Add(ellipse);

                // Draw label
                var label = new TextBlock
                {
                    Text = chartData[i].Label,
                    FontSize = 10,
                    Foreground = textBrush,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, x - 20);
                Canvas.SetTop(label, padding + chartHeight + 5);
                AccuracyChartCanvas.Children.Add(label);
            }

            // Draw connecting lines
            if (points.Count > 1)
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var line = new Line
                    {
                        X1 = points[i].X,
                        Y1 = points[i].Y,
                        X2 = points[i + 1].X,
                        Y2 = points[i + 1].Y,
                        Stroke = lineStroke,
                        StrokeThickness = 2
                    };
                    AccuracyChartCanvas.Children.Add(line);
                }
            }
        }

        private int CalculateStreak(List<TestResult> history)
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
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"test_history_{DateTime.Now:yyyyMMdd}"
            };
            picker.FileTypeChoices.Add("CSV", new[] { ".csv" });

            // Set XamlRoot for WinUI3 compatibility
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var lines = new List<string>
                {
                    "时间,词库名称,总单词数,正确数,正确率"
                };

                foreach (var result in _viewModel.TestHistory)
                {
                    lines.Add(
                        $"{result.Timestamp:yyyy-MM-dd HH:mm},{result.WordListName},{result.TotalWords},{result.CorrectCount},{result.Accuracy:F1}%");
                }

                await FileIO.WriteTextAsync(file, string.Join(Environment.NewLine, lines));
            }
        }

        private async void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "确认清空",
                    Content = "确定要清空所有学习记录吗？此操作不可恢复。",
                    PrimaryButtonText = "清空",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _viewModel.TestHistory.Clear();
                    var currentUser = _viewModel?.Settings?.Settings?.CurrentUser;
                    if (_viewModel?.Settings != null)
                    {
                        await _viewModel.Settings.SaveTestHistoryAsync(currentUser ?? "", _viewModel.TestHistory);
                    }

                    LoadStats();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ClearButton_Click error: {ex.Message}");
            }
        }

        private async void RedictateButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not TestResultViewModel vm) return;
            if (vm.Result.Words is not { Count: > 0 } words) return;

            var wordList = words
                .Select(w => new DictationTestPage.WordTranslationPair { Word = w.Word, Translation = w.Translation })
                .ToList();

            var settingsDialog = new ContentDialog
            {
                Title = "听考选项",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 10 };

            var modeStack = new StackPanel { Spacing = 5 };
            var onlineModeRadio = new RadioButton { Content = "在线听写", IsChecked = true, GroupName = "RedictateMode" };
            var paperModeRadio = new RadioButton { Content = "纸笔听写", GroupName = "RedictateMode" };
            modeStack.Children.Add(onlineModeRadio);
            modeStack.Children.Add(paperModeRadio);
            stackPanel.Children.Add(modeStack);

            var randomOrderSwitch = new ToggleSwitch { Header = "随机顺序", IsOn = false };
            stackPanel.Children.Add(randomOrderSwitch);

            var readTranslationSwitch = new ToggleSwitch { Header = "朗读翻译", IsOn = false };
            stackPanel.Children.Add(readTranslationSwitch);

            settingsDialog.Content = stackPanel;

            var result = await settingsDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            var testParams = new WordsPage.DictationTestParamsWithTranslations(
                wordList,
                randomOrderSwitch.IsOn,
                readTranslationSwitch.IsOn,
                paperModeRadio.IsChecked ?? false);

            Frame?.Navigate(typeof(DictationTestPage), testParams);
        }

        private void RelearnButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not TestResultViewModel vm) return;
            if (vm.Result.Words is not { Count: > 0 } words) return;

            var wordList = words
                .Select(w => new DictationTestPage.WordTranslationPair { Word = w.Word, Translation = w.Translation })
                .ToList();

            Frame?.Navigate(typeof(MemorizePage), wordList);
        }
    }
}