using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class TestPage : Page
    {
        private readonly MainViewModel _viewModel;
        private bool _isTestCompleted = false;

        public TestPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += TestPage_Loaded;
        }

        private void TestPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            if (_viewModel.WordsCount > 0)
            {
                _viewModel.StartTestCommand.Execute(null);
            }
            
            IntervalSlider.Value = _viewModel.ReadInterval;
            IntervalText.Text = $"{_viewModel.ReadInterval}秒";
            UpdateDisplay();
            
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsTesting) && !_viewModel.IsTesting && _isTestCompleted)
                {
                    _isTestCompleted = false;
                    ShowTestCompleteDialog();
                }
            };
            
            // Set focus to enable keyboard shortcuts
            this.Focus(FocusState.Programmatic);
        }

        private async void ShowTestCompleteDialog()
        {
            // Calculate test results
            int totalWords = _viewModel.WordsCount;
            int correctCount = 0; // For now, assume all correct in paper mode
            double accuracy = 100.0; // For paper mode, we can't track actual accuracy
            
            // For online dictation mode, we would calculate actual accuracy
            if (_viewModel.DictationMode == 1)
            {
                // TODO: Implement actual accuracy calculation for online mode
                // This would require tracking user input and comparing with correct answers
            }
            
            // Create test result
            var testResult = new Models.TestResult
            {
                Timestamp = DateTime.Now,
                TotalWords = totalWords,
                CorrectCount = correctCount,
                Accuracy = accuracy,
                WordListName = _viewModel.CurrentWordListName
            };
            
            // Add to history
            _viewModel.TestHistory.Add(testResult);
            
            // Save test history
            await _viewModel.Settings.SaveTestHistoryAsync(_viewModel.TestHistory);
            
            // Update view model
            _viewModel.TestHistoryViewModels.Clear();
            foreach (var historyItem in _viewModel.TestHistory.OrderByDescending(h => h.Timestamp))
            {
                _viewModel.TestHistoryViewModels.Add(new TestResultViewModel { Result = historyItem });
            }
            
            var dialog = new ContentDialog
            {
                Title = "听写测试结束",
                Content = $"本次测试共 {totalWords} 个单词\n正确率: {accuracy:F1}%",
                PrimaryButtonText = "直接返回",
                SecondaryButtonText = "显示答案"
            };

            var dialogResult = await dialog.ShowAsync();
            
            if (dialogResult == ContentDialogResult.Secondary)
            {
                // 用户选择"显示答案"，显示答案界面
                Frame?.Navigate(typeof(AnswersPage));
            }
            else
            {
                // 用户选择"直接返回"，返回主界面
                Frame?.Navigate(typeof(HomePage));
            }
        }

        private async void ExitTestButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "确认退出",
                Content = "好学生是不会放弃的！",
                PrimaryButtonText = "返回",
                CloseButtonText = "去意已决"
            };

            dialog.PrimaryButtonClick += (s, args) =>
            {
                // 用户选择返回，继续测试
            };

            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.None)
            {
                // 用户选择"去意已决"，停止测试并返回主界面
                _viewModel?.StopTestCommand.Execute(null);
                Frame?.Navigate(typeof(HomePage));
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.PreviousWordCommand.Execute(null);
            UpdateDisplay();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.NextWordCommand.Execute(null);
            UpdateDisplay();
            
            // 检查是否到达最后一个单词
            if (_viewModel.CurrentIndex >= _viewModel.WordsCount - 1)
            {
                _isTestCompleted = true;
            }
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.RepeatWordCommand.Execute(null);
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.PauseResumeCommand.Execute(null);
            PauseButton.Content = _viewModel?.IsPaused == true ? "继续" : "暂停";
        }

        private void IntervalSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_viewModel == null) return;
            
            var value = (int)e.NewValue;
            IntervalText.Text = $"{value}秒";
            _viewModel.ReadInterval = value;
        }

        private void UpdateDisplay()
        {
            if (_viewModel == null) return;
            
            CurrentWordText.Text = _viewModel.CurrentWord;
            var totalWords = _viewModel.WordsCount;
            ProgressText.Text = $"{_viewModel.CurrentIndex + 1}/{totalWords}";
            CountdownLabel.Text = _viewModel.Countdown.ToString();
        }
        
        private void Page_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (_viewModel == null || !_viewModel.IsTesting) return;
            
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Space:
                    RepeatButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Left:
                    PreviousButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Right:
                    NextButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.Escape:
                    PauseButton_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
    }
}
