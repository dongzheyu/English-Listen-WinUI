using System;
using System.Linq;
using System.Threading.Tasks;
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
        private bool _isOnlineMode = false;
        private int _testCountdown = 0;
        private System.Timers.Timer? _dictationTimer;
        private int _correctAnswers = 0;
        private int _wrongAnswers = 0;

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
                // Determine dictation mode
                _isOnlineMode = _viewModel.DictationMode == 1;
                
                if (_isOnlineMode)
                {
                    SetupOnlineDictation();
                }
                else
                {
                    SetupPaperDictation();
                }
                
                // Apply settings
                IntervalSlider.Value = _viewModel.ReadInterval;
                IntervalText.Text = $"{_viewModel.ReadInterval}秒";
                
                // Start test
                _viewModel.StartTestCommand.Execute(null);
                
                if (!_isOnlineMode)
                {
                    StartDictationTimer();
                }
            }
            
            _viewModel.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(_viewModel.IsTesting) && !_viewModel.IsTesting && _isTestCompleted)
                {
                    _isTestCompleted = false;
                    ShowTestCompleteDialog();
                }
                else if (args.PropertyName == nameof(_viewModel.CurrentWord))
                {
                    UpdateDisplay();
                }
                else if (args.PropertyName == nameof(_viewModel.Countdown))
                {
                    CountdownLabel.Text = _viewModel.Countdown.ToString();
                }
            };
            
            // Set focus to enable keyboard shortcuts
            this.Focus(FocusState.Programmatic);
        }

        private void SetupPaperDictation()
        {
            // Show paper dictation UI
            OnlineInputPanel.Visibility = Visibility.Collapsed;
            OnlineModeButtons.Visibility = Visibility.Collapsed;
            PaperModeButtons.Visibility = Visibility.Visible;
            SettingsPanel.Visibility = Visibility.Visible;
            CountdownLabel.Visibility = Visibility.Visible;
            WordDisplayBorder.Visibility = Visibility.Visible;
        }

        private void SetupOnlineDictation()
        {
            // Show online dictation UI
            OnlineInputPanel.Visibility = Visibility.Visible;
            OnlineModeButtons.Visibility = Visibility.Visible;
            PaperModeButtons.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Collapsed;
            CountdownLabel.Visibility = Visibility.Collapsed;
            WordDisplayBorder.Visibility = Visibility.Collapsed;
            
            OnlineInputTextBox.Text = "";
            OnlineStatusLabel.Text = "";
            OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
            
            // Reset counters for new test
            _correctAnswers = 0;
            _wrongAnswers = 0;
        }

        private void StartDictationTimer()
        {
            StopDictationTimer();
            _testCountdown = 0;
            _dictationTimer = new System.Timers.Timer(1000);
            _dictationTimer.Elapsed += async (s, e) =>
            {
                if (!_viewModel.IsPaused)
                {
                    _testCountdown++;
                    var countdown = _viewModel.ReadInterval - _testCountdown;
                    var currentIdx = _viewModel.CurrentIndex;
                    var totalWords = _viewModel.WordsCount;
                    var shouldSpeak = _testCountdown >= _viewModel.ReadInterval;

                    try
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                _viewModel.Countdown = countdown;
                                if (shouldSpeak && currentIdx < totalWords - 1)
                                {
                                    _testCountdown = 0;
                                    _viewModel.NextWordCommand.Execute(null);
                                }
                                else if (shouldSpeak && currentIdx >= totalWords - 1)
                                {
                                    _isTestCompleted = true;
                                    StopDictationTimer();
                                    _viewModel.StopTestCommand.Execute(null);
                                }
                            });
                    }
                    catch { }
                }
            };
            _dictationTimer.Start();
        }

        private void StopDictationTimer()
        {
            _dictationTimer?.Stop();
            _dictationTimer?.Dispose();
            _dictationTimer = null;
        }

        private async void ShowTestCompleteDialog()
        {
            StopDictationTimer();
            
            // Calculate test results
            int totalWords = _viewModel.WordsCount;
            int correctCount = 0;
            double accuracy = 100.0;
            
            // For online dictation mode, calculate actual accuracy
            if (_isOnlineMode && _viewModel.UserInputs != null && _viewModel.UserInputs.Count > 0)
            {
                for (int i = 0; i < Math.Min(_viewModel.CurrentWords.Count, _viewModel.UserInputs.Count); i++)
                {
                    var correct = _viewModel.CurrentWords[i].ToLower();
                    var userInput = _viewModel.UserInputs[i].ToLower();
                    if (correct == userInput)
                    {
                        correctCount++;
                    }
                }
                accuracy = totalWords > 0 ? (double)correctCount / totalWords * 100.0 : 0.0;
            }
            else
            {
                // For paper mode, show self-assessment dialog
                var assessmentDialog = new ContentDialog
                {
                    Title = "测试完成",
                    Content = $"本次测试共 {totalWords} 个单词，请评估您答对了多少个？",
                    PrimaryButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                // Create a number box for user input
                var numberBox = new NumberBox
                {
                    Minimum = 0,
                    Maximum = totalWords,
                    Value = totalWords,
                    Width = 120,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                
                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock { Text = $"本次测试共 {totalWords} 个单词，请评估您答对了多少个？" });
                stackPanel.Children.Add(numberBox);
                assessmentDialog.Content = stackPanel;

                var result = await assessmentDialog.ShowAsync();
                correctCount = (int)numberBox.Value;
                accuracy = totalWords > 0 ? (double)correctCount / totalWords * 100.0 : 0.0;
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
            var currentUser = _viewModel.Settings.Settings.CurrentUser;
            await _viewModel.Settings.SaveTestHistoryAsync(currentUser ?? "", _viewModel.TestHistory);
            
            // Update view model
            _viewModel.TestHistoryViewModels.Clear();
            foreach (var historyItem in _viewModel.TestHistory.OrderByDescending(h => h.Timestamp))
            {
                _viewModel.TestHistoryViewModels.Add(new TestResultViewModel { Result = historyItem });
            }
            
            var dialog = new ContentDialog
            {
                Title = "听写测试结束",
                Content = $"本次测试共 {totalWords} 个单词\n正确: {correctCount} 个\n正确率: {accuracy:F1}%",
                PrimaryButtonText = "直接返回",
                SecondaryButtonText = "显示答案",
                XamlRoot = this.XamlRoot
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
                CloseButtonText = "去意已决",
                XamlRoot = this.XamlRoot
            };

            dialog.PrimaryButtonClick += (s, args) =>
            {
                // 用户选择返回，继续测试
            };

            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.None)
            {
                // 用户选择"去意已决"，停止测试并返回主界面
                StopDictationTimer();
                _viewModel?.StopTestCommand.Execute(null);
                Frame?.Navigate(typeof(HomePage));
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.PreviousWordCommand.Execute(null);
            UpdateDisplay();
            
            // Reset countdown for paper mode
            if (!_isOnlineMode)
            {
                _testCountdown = 0;
                _viewModel.Countdown = _viewModel.ReadInterval;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            // 在线听写模式下，这个函数不需要执行
            if (_isOnlineMode) {
                return;
            }
            
            if (_viewModel == null) return;
            
            _viewModel.NextWordCommand.Execute(null);
            UpdateDisplay();
            
            // Reset countdown for paper mode
            if (!_isOnlineMode)
            {
                _testCountdown = 0;
                _viewModel.Countdown = _viewModel.ReadInterval;
            }
            
            // 检查是否到达最后一个单词
            if (_viewModel.CurrentIndex >= _viewModel.WordsCount - 1)
            {
                _isTestCompleted = true;
                StopDictationTimer();
                _viewModel.StopTestCommand.Execute(null);
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

        private async void OnlineSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || !_isOnlineMode) return;
            
            var userInput = OnlineInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                OnlineStatusLabel.Text = "请输入单词!";
                OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                return;
            }
            
            // Save user input
            if (_viewModel.CurrentIndex < _viewModel.UserInputs.Count)
            {
                _viewModel.UserInputs[_viewModel.CurrentIndex] = userInput;
            }
            
            // Check answer and count correct/wrong
            var currentWord = _viewModel.CurrentWord;
            bool isCorrect = userInput.ToLower() == currentWord.ToLower();
            
            if (isCorrect)
            {
                _correctAnswers++;
                OnlineStatusLabel.Text = "正确！";
                OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
            }
            else
            {
                _wrongAnswers++;
                OnlineStatusLabel.Text = $"错误！正确答案: {currentWord}";
                OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
            }
            
            // Clear input and move to next word after delay
            OnlineInputTextBox.Text = "";
            
            // Wait a moment for user to see the result, then move to next word
            await Task.Delay(1500);
            MoveToNextOnlineWord();
        }



        private async void MoveToNextOnlineWord()
        {
            if (_viewModel == null || !_isOnlineMode) return;
            
            _viewModel.CurrentIndex++;
            
            if (_viewModel.CurrentIndex < _viewModel.WordsCount)
            {
                // Update progress
                OnlineStatusLabel.Text = $"第 {_viewModel.CurrentIndex + 1} / {_viewModel.WordsCount} 个单词";
                OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                
                // Speak the word
                await _viewModel.SpeechService.SpeakAsync(_viewModel.CurrentWord, _viewModel.Settings.Settings.FliteVoiceModel);
                
                // Clear input
                OnlineInputTextBox.Text = "";
                
                // Focus on input
                OnlineInputTextBox.Focus(FocusState.Programmatic);
            }
            else
            {
                // Test completed - show statistics and save user data
                _isTestCompleted = true;
                _viewModel.StopTestCommand.Execute(null);
                await ShowOnlineTestResultsAsync();
            }
        }

        private async Task ShowOnlineTestResultsAsync()
        {
            var totalWords = _viewModel?.WordsCount ?? 0;
            var accuracy = totalWords > 0 ? (double)_correctAnswers / totalWords * 100 : 0;
            
            var resultDialog = new ContentDialog
            {
                Title = "在线听写测试完成",
                Content = $"测试结果:\n\n" +
                         $"总单词数: {totalWords}\n" +
                         $"正确: {_correctAnswers}\n" +
                         $"错误: {_wrongAnswers}\n" +
                         $"正确率: {accuracy:F1}%",
                PrimaryButtonText = "查看答案",
                SecondaryButtonText = "返回主界面",
                CloseButtonText = "重新测试",
                XamlRoot = this.XamlRoot
            };

            var result = await resultDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 用户选择"查看答案"，显示答案界面
                Frame?.Navigate(typeof(AnswersPage));
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // 用户选择"返回主界面"
                Frame?.Navigate(typeof(HomePage));
            }
            else
            {
                // 用户选择"重新测试"，重置计数并重新开始
                _correctAnswers = 0;
                _wrongAnswers = 0;
                _viewModel?.StartTestCommand.Execute(null);
            }
            
            // 保存用户数据到配置文件
            await SaveUserTestResultAsync(totalWords, _correctAnswers, accuracy);
        }

        private async Task SaveUserTestResultAsync(int totalWords, int correctCount, double accuracy)
        {
            try
            {
                var currentUser = _viewModel?.Settings?.Settings?.CurrentUser;
                if (!string.IsNullOrEmpty(currentUser) && _viewModel?.Settings != null)
                {
                    // 加载用户数据
                    var users = await _viewModel.Settings.LoadUsersAsync();
                    var user = users.FirstOrDefault(u => u.Username == currentUser);
                    
                    if (user != null)
                    {
                        // 添加测试结果
                        user.TestHistory.Add(new Models.TestResult
                        {
                            Timestamp = DateTime.Now,
                            TotalWords = totalWords,
                            CorrectCount = correctCount,
                            Accuracy = accuracy,
                            WordListName = "临时词库"
                        });
                        
                        // 更新统计
                        user.CompletedTests++;
                        
                        // 保存用户数据
                        await _viewModel.Settings.SaveUsersAsync(users);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果保存失败，记录错误但不中断用户操作
                System.Diagnostics.Debug.WriteLine($"Failed to save user test result: {ex.Message}");
            }
        }

        private void OnlineInputTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var userInput = OnlineInputTextBox.Text.Trim();
                if (string.IsNullOrEmpty(userInput))
                {
                    OnlineStatusLabel.Text = "请输入单词!";
                    OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                    return;
                }
                
                // Save input
                if (_viewModel != null && _viewModel.CurrentIndex < _viewModel.UserInputs.Count)
                {
                    _viewModel.UserInputs[_viewModel.CurrentIndex] = userInput;
                }
                
                // Check answer and count correct/wrong
                var currentWord = _viewModel?.CurrentWord ?? "";
                bool isCorrect = userInput.ToLower() == currentWord.ToLower();
                
                if (isCorrect)
                {
                    _correctAnswers++;
                    OnlineStatusLabel.Text = "正确！进入下一个单词...";
                    OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                }
                else
                {
                    _wrongAnswers++;
                    OnlineStatusLabel.Text = $"错误！正确答案: {currentWord}，进入下一个单词...";
                    OnlineStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }
                
                // Move to next word after delay
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    MoveToNextOnlineWord();
                };
                timer.Start();
                
                e.Handled = true;
            }
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

            if (_isOnlineMode)
            {
                // Online mode: only handle Enter for input submission
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    OnlineInputTextBox_KeyDown(sender, e);
                    e.Handled = true;
                }
                return;
            }
            
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
                    if (_isOnlineMode)
                    {
                        // In online mode, right arrow should move to next word
                        MoveToNextOnlineWord();
                    }
                    else
                    {
                        NextButton_Click(sender, e);
                    }
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
