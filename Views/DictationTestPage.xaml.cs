using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;
using English_Listen_WinUI.Services;
using English_Listen_WinUI.Models;

namespace English_Listen_WinUI.Views
{
    public sealed partial class DictationTestPage : Page, IDisposable
    {
        private List<WordTranslationPair> wordList = new List<WordTranslationPair>();
        private int currentIndex;
        private int correctCount;
        private int totalWords;
        private int countdownSeconds;
        private int remainingSeconds;
        private DispatcherTimer? countdownTimer;
        private SpeechSynthesizer? synthesizer;
        private TaskCompletionSource<bool>? speakTaskSource;
        private bool isTestActive;
        private bool isSpeaking = false;
        private bool _readTranslation = false;
        private bool _isPaperMode = false;
        private Random random = new Random();
        private string currentFileName = string.Empty;
        private HashSet<int> correctIndices = new HashSet<int>();
        private BaiduTranslateService _translateService;
        private TranslationLibraryService _translationLibraryService;
        private SpeechSynthesizer? chineseSynthesizer;

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }

        public DictationTestPage()
        {
            this.InitializeComponent();
            try
            {
                synthesizer = new SpeechSynthesizer();
                synthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                try
                {
                    synthesizer.SetOutputToDefaultAudioDevice();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"英文 Synthesizer SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"英文 Synthesizer 初始化失败: {ex.Message}");
                synthesizer = null;
            }

            try
            {
                chineseSynthesizer = new SpeechSynthesizer();
                chineseSynthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                try
                {
                    chineseSynthesizer.SetOutputToDefaultAudioDevice();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"中文 Synthesizer SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"中文 Synthesizer 初始化失败: {ex.Message}");
                chineseSynthesizer = null;
            }

            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            _translateService = new BaiduTranslateService();
            _translationLibraryService = new TranslationLibraryService();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var mainWindow = App.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.SetSidebarVisibility(false);
            }

            // 从共享的ViewModel获取设置，而不是创建新的SettingsService实例
            countdownSeconds = App.SharedViewModel?.Settings?.Settings?.ReadInterval ?? 5;

            // 同步设置到UI控件（OnNavigatedTo 在 UI 线程上调用，可以直接设置）
            if (CountdownSetter != null)
            {
                CountdownSetter.Value = countdownSeconds;
            }

            SetVoicesFromSettings();

            if (e.Parameter is DictationTestParams testParams)
            {
                currentFileName = testParams.FileName;
                LoadWordList(currentFileName, testParams.RandomOrder);
            }
            else if (e.Parameter != null && e.Parameter.GetType().Name == "DictationTestParamsWithTranslations")
            {
                var wordListProperty = e.Parameter.GetType().GetProperty("WordList");
                var randomOrderProperty = e.Parameter.GetType().GetProperty("RandomOrder");
                var readTranslationProperty = e.Parameter.GetType().GetProperty("ReadTranslation");
                var isPaperModeProperty = e.Parameter.GetType().GetProperty("IsPaperMode");
                
                if (wordListProperty != null && randomOrderProperty != null)
                {
                    var wordListValue = wordListProperty.GetValue(e.Parameter) as List<WordTranslationPair>;
                    var randomOrderValue = (bool)(randomOrderProperty.GetValue(e.Parameter) ?? false);
                    var readTranslationValue = (bool)(readTranslationProperty?.GetValue(e.Parameter) ?? false);
                    var isPaperModeValue = (bool)(isPaperModeProperty?.GetValue(e.Parameter) ?? false);
                    
                    if (wordListValue != null)
                    {
                        wordList = wordListValue;
                        if (randomOrderValue)
                        {
                            ShuffleWordList();
                        }
                        totalWords = wordList.Count;
                        currentIndex = 0;
                        correctCount = 0;
                        correctIndices.Clear();
                        isTestActive = true;
                        _readTranslation = readTranslationValue;
                        _isPaperMode = isPaperModeValue;
                        UpdateUI();
                        _ = PlayCurrentWordAndStartCountdown();
                    }
                }
            }
            else if (e.Parameter is string fileName)
            {
                currentFileName = fileName;
                LoadWordList(currentFileName, false);
            }
            else
            {
                ShowErrorDialog("未指定词库文件。");
                return;
            }

            // 在UI加载完成后设置焦点
            if (InputTextBox != null)
            {
                InputTextBox.Focus(FocusState.Programmatic);
            }
        }

        private void Synthesizer_SpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        {
            speakTaskSource?.TrySetResult(true);
        }



        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            var mainWindow = App.MainWindow as MainWindow;
            mainWindow?.SetSidebarVisibility(true);

            // 清理资源
            Dispose();
        }

        private void LoadWordList(string filePath, bool randomOrder)
        {
            // 判断是否为完整路径，如果不是则拼接词库目录
            string fullPath;
            if (Path.IsPathRooted(filePath))
            {
                fullPath = filePath;
            }
            else
            {
                string folderPath = Path.Combine(AppContext.BaseDirectory, "wordlist");
                fullPath = Path.Combine(folderPath, filePath);
            }

            if (!File.Exists(fullPath))
            {
                _ = ShowErrorDialogAsync("词库文件不存在。");
                return;
            }

            try
            {
                var lines = File.ReadAllLines(fullPath);
                wordList = lines.Where(line => !string.IsNullOrWhiteSpace(line))
                                .Select(line => line.Trim())
                                .Select(line => 
                                {
                                    // 尝试解析单词和翻译（格式：单词|翻译）
                                    var parts = line.Split('|');
                                    if (parts.Length >= 2)
                                    {
                                        return new WordTranslationPair 
                                        { 
                                            Word = parts[0].Trim(), 
                                            Translation = parts[1].Trim() 
                                        };
                                    }
                                    else
                                    {
                                        return new WordTranslationPair 
                                        { 
                                            Word = line, 
                                            Translation = "" 
                                        };
                                    }
                                })
                                .ToList();
            }
            catch (Exception ex)
            {
                _ = ShowErrorDialogAsync($"读取词库文件失败：{ex.Message}");
                return;
            }

            if (wordList.Count == 0)
            {
                _ = ShowEmptyListDialogAsync();
                return;
            }

            if (randomOrder)
            {
                ShuffleWordList();
            }

            totalWords = wordList.Count;
            currentIndex = 0;
            correctCount = 0;
            correctIndices.Clear();
            isTestActive = true;
            UpdateUI();
            _ = PlayCurrentWordAndStartCountdown();
        }

        private void ShuffleWordList()
        {
            int n = wordList.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                var temp = wordList[k];
                wordList[k] = wordList[n];
                wordList[n] = temp;
            }
        }

        private void UpdateUI()
        {
            ProgressText.Text = $"第 {currentIndex + 1} / {totalWords} 个";
            
            if (_isPaperMode)
            {
                ScoreText.Text = string.Empty;
                InputTextBox.Visibility = Visibility.Collapsed;
                SubmitButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                ScoreText.Text = $"正确: {correctCount} / {totalWords}";
                InputTextBox.Visibility = Visibility.Visible;
                SubmitButton.Visibility = Visibility.Visible;
                InputTextBox.Text = string.Empty;
            }
            
            CountdownText.Text = string.Empty;
            SpeakingStatusText.Text = string.Empty;
            // 保留翻译显示，实现实时翻译
        }

        private async Task PlayCurrentWordAndStartCountdown()
        {
            if (!isTestActive || currentIndex >= totalWords) return;

            countdownTimer?.Stop();

            var wordPair = wordList[currentIndex];
            string word = wordPair.Word;
            string translation = wordPair.Translation;
            
            // 实时检查并显示翻译
            if (string.IsNullOrEmpty(translation) && _readTranslation)
            {
                // 自动翻译
                try
                {
                    translation = await _translateService.TranslateAsync(word, "en", "zh");
                    if (!string.IsNullOrEmpty(translation))
                    {
                        wordPair.Translation = translation;
                        TranslationText.Text = $"翻译: {translation}";
                        // 保存翻译到翻译库
                        _translationLibraryService.SaveTranslation(word, translation);
                        _translationLibraryService.SaveToFile();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"翻译错误：{ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(translation))
            {
                // 如果已有翻译，立即显示
                TranslationText.Text = $"翻译: {translation}";
            }
            
            SpeakingStatusText.Text = "正在朗读...";
            isSpeaking = true;
            
            try
            {
                // 第一步：英文朗读 - 使用设置的英文语音模型
                await SpeakEnglishWordAsync(word);
                
                // 第二步：中文朗读 - 使用设置的中文语音模型
                if (_readTranslation && !string.IsNullOrEmpty(translation))
                {
                    await SpeakChineseTranslationAsync(translation);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语音播放错误：{ex.Message}");
            }
            finally
            {
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }

            if (isTestActive && currentIndex < totalWords)
            {
                StartCountdown();
            }
        }

        private async Task SpeakEnglishWordAsync(string word)
        {
            if (synthesizer == null) return;

            // 1. 彻底取消所有正在进行的朗读（包括中文实例，防止干扰）
            chineseSynthesizer?.SpeakAsyncCancelAll();
            synthesizer.SpeakAsyncCancelAll();

            // 2. 强制重新应用英文语音设置
            var englishVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsEnglishVoiceName;
            if (!string.IsNullOrEmpty(englishVoiceName))
            {
                SetEnglishVoice(englishVoiceName);
            }

            // 3. 稍微等待一小会（约50ms），让 SAPI 引擎完成内部切换
            await Task.Delay(50);

            speakTaskSource = new TaskCompletionSource<bool>();
            synthesizer.SpeakAsync(word);

            // 增加超时保护
            var completedTask = await Task.WhenAny(speakTaskSource.Task, Task.Delay(8000));
            if (completedTask != speakTaskSource.Task)
            {
                synthesizer.SpeakAsyncCancelAll();
                System.Diagnostics.Debug.WriteLine("英文语音播放超时");
            }
        }

        private async Task SpeakChineseTranslationAsync(string translation)
        {
            if (chineseSynthesizer == null)
            {
                chineseSynthesizer = new SpeechSynthesizer();
                chineseSynthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                chineseSynthesizer.SetOutputToDefaultAudioDevice();
            }
            
            // 在朗读前设置中文语音模型
            var chineseVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsChineseVoiceName;
            if (!string.IsNullOrEmpty(chineseVoiceName))
            {
                SetChineseVoice(chineseVoiceName);
            }
            
            await Task.Delay(300);
            
            speakTaskSource = new TaskCompletionSource<bool>();
            chineseSynthesizer.SpeakAsyncCancelAll();
            chineseSynthesizer.SpeakAsync(translation);
            
            var completedTask = await Task.WhenAny(speakTaskSource.Task, Task.Delay(10000));
            if (completedTask != speakTaskSource.Task)
            {
                chineseSynthesizer.SpeakAsyncCancelAll();
                System.Diagnostics.Debug.WriteLine("中文语音播放超时");
            }
        }

        private void StartCountdown()
        {
            remainingSeconds = countdownSeconds;
            CountdownText.Text = $"剩余 {remainingSeconds} 秒";
            countdownTimer?.Start();
        }

        private void CountdownTimer_Tick(object? sender, object e)
        {
            remainingSeconds--;
            CountdownText.Text = $"剩余 {remainingSeconds} 秒";

            if (remainingSeconds <= 0)
            {
                countdownTimer?.Stop();
                AutoSubmit();
            }
        }

        private async void Submit()
        {
            if (!isTestActive || currentIndex >= totalWords) return;
            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
                chineseSynthesizer?.SpeakAsyncCancelAll();
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }

            string input = InputTextBox.Text?.Trim() ?? "";
            string correct = wordList[currentIndex].Word.Trim();

            if (input.Equals(correct, StringComparison.OrdinalIgnoreCase) && !correctIndices.Contains(currentIndex))
            {
                correctCount++;
                correctIndices.Add(currentIndex);
                ScoreText.Text = $"正确: {correctCount} / {totalWords}";
            }

            MoveToNextWord();
        }

        private void AutoSubmit()
        {
            if (!isTestActive || currentIndex >= totalWords) return;

            string input = InputTextBox.Text?.Trim() ?? "";
            string correct = wordList[currentIndex].Word.Trim();

            // Only increment correctCount if this index hasn't been correctly answered before
            if (input.Equals(correct, StringComparison.OrdinalIgnoreCase) && !correctIndices.Contains(currentIndex))
            {
                correctCount++;
                correctIndices.Add(currentIndex);
                ScoreText.Text = $"正确: {correctCount} / {totalWords}";
            }

            MoveToNextWord();
        }

        private void MoveToNextWord()
        {
            if (currentIndex + 1 < totalWords)
            {
                currentIndex++;
                UpdateUI();
                _ = PlayCurrentWordAndStartCountdown();
            }
            else
            {
                _ = EndTestAsync();
            }
        }

        private void Skip()
        {
            if (!isTestActive || currentIndex >= totalWords) return;
            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
                chineseSynthesizer?.SpeakAsyncCancelAll();
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }
            
            MoveToNextWord();
        }

        private void Replay()
        {
            if (!isTestActive || currentIndex >= totalWords) return;
            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
                chineseSynthesizer?.SpeakAsyncCancelAll();
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }
            
            _ = PlayCurrentWordAndStartCountdown();
        }

        private void Previous()
        {
            if (!isTestActive) return;
            if (currentIndex <= 0) return;

            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
                chineseSynthesizer?.SpeakAsyncCancelAll();
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }
            
            currentIndex--;
            UpdateUI();
            _ = PlayCurrentWordAndStartCountdown();
        }

        private async Task EndTestAsync()
        {
            isTestActive = false;
            countdownTimer?.Stop();

            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
            }

            if (speakTaskSource != null)
            {
                try
                {
                    await speakTaskSource.Task;
                }
                catch
                {
                }
            }

            if (_isPaperMode)
            {
                // 显示提示框
                var dialog = new ContentDialog
                {
                    Title = "听写完成",
                    Content = "请核对您的答案，然后点击继续查看标准答案。",
                    PrimaryButtonText = "继续",
                    XamlRoot = this.XamlRoot
                };

                await dialog.ShowAsync();
                // 导航到AnswersPage显示答案
                Frame?.Navigate(typeof(AnswersPage), wordList);
            }
            else
            {
                await ShowOnlineModeResultDialogAsync();
            }
        }

        private async Task ShowPaperModeResultDialogAsync()
        {
            var inputDialog = new ContentDialog
            {
                Title = "测试完成",
                Content = new StackPanel
                {
                    Margin = new Thickness(0, 10, 0, 0),
                    Children = 
                    {
                        new TextBlock { Text = "请输入您答对的单词数量：" },
                        new NumberBox 
                        { 
                            Width = 200, 
                            Minimum = 0, 
                            Maximum = totalWords,
                            Value = 0,
                            Margin = new Thickness(0, 10, 0, 0)
                        }
                    }
                },
                PrimaryButtonText = "确定",
                CloseButtonText = "返回主菜单",
                XamlRoot = this.XamlRoot
            };

            var result = await inputDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var numberBox = inputDialog.Content as StackPanel;
                var inputBox = numberBox?.Children[1] as NumberBox;
                var userCorrectCount = (int)(inputBox?.Value ?? 0);

                if (userCorrectCount > totalWords)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "正确数不能超过总单词数",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    await ShowPaperModeResultDialogAsync();
                    return;
                }

                double accuracy = totalWords > 0 ? (double)userCorrectCount / totalWords * 100 : 0;

                await SaveTestResultAsync(userCorrectCount, accuracy);

                var accuracyDialog = new ContentDialog
                {
                    Title = "测试结果",
                    Content = $"正确率：{accuracy:F1}% ({userCorrectCount}/{totalWords})",
                    PrimaryButtonText = "再测一次",
                    CloseButtonText = "返回主菜单",
                    XamlRoot = this.XamlRoot
                };

                var accuracyResult = await accuracyDialog.ShowAsync();

                if (accuracyResult == ContentDialogResult.Primary)
                {
                    currentIndex = 0;
                    correctCount = 0;
                    correctIndices.Clear();
                    UpdateUI();
                    isTestActive = true;
                    _ = PlayCurrentWordAndStartCountdown();
                }
                else
                {
                    Frame?.GoBack();
                }
            }
            else
            {
                Frame?.GoBack();
            }
        }

        private async Task ShowOnlineModeResultDialogAsync()
        {
            double accuracy = totalWords > 0 ? (double)correctCount / totalWords * 100 : 0;

            await SaveTestResultAsync(correctCount, accuracy);

            ContentDialog dialog = new ContentDialog
            {
                Title = "测试完成",
                Content = $"您的得分：{correctCount}/{totalWords} ({accuracy:F1}%)",
                PrimaryButtonText = "再测一次",
                CloseButtonText = "返回主菜单",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                currentIndex = 0;
                correctCount = 0;
                correctIndices.Clear();
                UpdateUI();
                isTestActive = true;
                _ = PlayCurrentWordAndStartCountdown();
            }
            else
            {
                Frame?.GoBack();
            }
        }

        private async Task ShowPleaseWaitDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "提示",
                Content = "请先等待朗读结束",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async Task ShowErrorDialogAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
            Frame?.GoBack();
        }

        private async Task ShowEmptyListDialogAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "提示",
                Content = "词库为空，请先添加单词。",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
            Frame?.GoBack();
        }

        private void ShowErrorDialog(string message)
        {
            _ = ShowErrorDialogAsync(message);
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            Submit();
        }

        private void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            Replay();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            Skip();
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            Previous();
        }

        private async void EndButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await EndTestAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EndButton_Click error: {ex.Message}");
                _ = ShowErrorDialogAsync("结束测试过程中发生错误");
            }
        }

        private void CountdownSetter_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (args.NewValue >= 1 && args.NewValue <= 60)
            {
                countdownSeconds = (int)args.NewValue;
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                Submit();
                e.Handled = true;
            }
        }

        private async Task SaveTestResultAsync(int correctCount, double accuracy)
        {
            try
            {
                var settingsService = App.SharedViewModel?.Settings;
                if (settingsService == null) return;

                // 确保设置已加载
                await settingsService.LoadSettingsAsync();

                var currentUser = settingsService.Settings.CurrentUser;
                if (string.IsNullOrEmpty(currentUser)) return;

                var testHistory = await settingsService.LoadTestHistoryAsync(currentUser);

                var testResult = new TestResult
                {
                    Timestamp = DateTime.Now,
                    TotalWords = totalWords,
                    CorrectCount = correctCount,
                    Accuracy = accuracy,
                    WordListName = currentFileName
                };

                testHistory.Add(testResult);
                await settingsService.SaveTestHistoryAsync(currentUser, testHistory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存测试结果失败: {ex.Message}");
            }
        }

        private void SetVoicesFromSettings()
        {
            try
            {
                // 只设置英文语音到 synthesizer
                var englishVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsEnglishVoiceName;
                if (!string.IsNullOrEmpty(englishVoiceName))
                {
                    SetEnglishVoice(englishVoiceName);
                }

                // 只设置中文语音到 chineseSynthesizer
                var chineseVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsChineseVoiceName;
                if (!string.IsNullOrEmpty(chineseVoiceName))
                {
                    SetChineseVoice(chineseVoiceName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置语音失败: {ex.Message}");
            }
        }

        private void SetEnglishVoice(string voiceName)
        {
            try
            {
                if (synthesizer == null) return;

                // 获取所有已启用的语音
                var installedVoices = synthesizer.GetInstalledVoices();

                // 打印所有可用语音用于调试
                System.Diagnostics.Debug.WriteLine("=== synthesizer 可用语音列表 ===");
                foreach (var v in installedVoices)
                {
                    System.Diagnostics.Debug.WriteLine($"可用语音: {v.VoiceInfo.Name} | 语言: {v.VoiceInfo.Culture}");
                }

                // 1. 尝试匹配用户设置的特定名称
                var targetVoice = installedVoices.FirstOrDefault(v =>
                    v.Enabled && v.VoiceInfo.Name == voiceName);

                if (targetVoice != null)
                {
                    synthesizer.SelectVoice(targetVoice.VoiceInfo.Name);
                    System.Diagnostics.Debug.WriteLine($"英文语音已设置为: {voiceName}");
                }
                else
                {
                    // 2. 保底：如果设置的语音失效，强制寻找任何一个 Culture 为 "en" 的语音
                    var fallbackVoice = installedVoices.FirstOrDefault(v =>
                        v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase));

                    if (fallbackVoice != null)
                    {
                        synthesizer.SelectVoice(fallbackVoice.VoiceInfo.Name);
                        System.Diagnostics.Debug.WriteLine($"警告：未找到指定语音 {voiceName}，已自动回退到英文语音：{fallbackVoice.VoiceInfo.Name}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"错误：未找到任何英文语音，将使用系统默认语音");
                    }
                }
            }
            catch (ArgumentException ex)
            {
                // 如果提供的语音名称找不到，会抛出 ArgumentException
                System.Diagnostics.Debug.WriteLine($"错误：找不到名为 '{voiceName}' 的英文语音模型。请检查名称是否正确。详细信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置英文语音失败: {ex.Message}");
            }
        }

        private void SetChineseVoice(string voiceName)
        {
            try
            {
                if (chineseSynthesizer == null) return;

                // 获取所有已启用的语音
                var installedVoices = chineseSynthesizer.GetInstalledVoices();

                // 打印所有可用语音用于调试
                System.Diagnostics.Debug.WriteLine("=== chineseSynthesizer 可用语音列表 ===");
                foreach (var v in installedVoices)
                {
                    System.Diagnostics.Debug.WriteLine($"可用语音: {v.VoiceInfo.Name} | 语言: {v.VoiceInfo.Culture}");
                }

                // 1. 尝试匹配用户设置的特定名称
                var targetVoice = installedVoices.FirstOrDefault(v =>
                    v.Enabled && v.VoiceInfo.Name == voiceName);

                if (targetVoice != null)
                {
                    chineseSynthesizer.SelectVoice(targetVoice.VoiceInfo.Name);
                    System.Diagnostics.Debug.WriteLine($"中文语音已设置为: {voiceName}");
                }
                else
                {
                    // 2. 保底：如果设置的语音失效，强制寻找任何一个 Culture 为 "zh" 的语音
                    var fallbackVoice = installedVoices.FirstOrDefault(v =>
                        v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase));

                    if (fallbackVoice != null)
                    {
                        chineseSynthesizer.SelectVoice(fallbackVoice.VoiceInfo.Name);
                        System.Diagnostics.Debug.WriteLine($"警告：未找到指定语音 {voiceName}，已自动回退到中文语音：{fallbackVoice.VoiceInfo.Name}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"错误：未找到任何中文语音，将使用系统默认语音");
                    }
                }
            }
            catch (ArgumentException ex)
            {
                // 如果提供的语音名称找不到，会抛出 ArgumentException
                System.Diagnostics.Debug.WriteLine($"错误：找不到名为 '{voiceName}' 的中文语音模型。请检查名称是否正确。详细信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置中文语音失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // 停止定时器
            countdownTimer?.Stop();
            countdownTimer = null;
            
            // 释放SpeechSynthesizer资源
            if (synthesizer != null)
            {
                synthesizer.SpeakCompleted -= Synthesizer_SpeakCompleted;
                synthesizer.Dispose();
                synthesizer = null;
            }
            
            if (chineseSynthesizer != null)
            {
                chineseSynthesizer.SpeakCompleted -= Synthesizer_SpeakCompleted;
                chineseSynthesizer.Dispose();
                chineseSynthesizer = null;
            }
        }
    }

    public class DictationTestParams
    {
        public string FileName { get; set; } = string.Empty;
        public bool RandomOrder { get; set; } = false;

        public DictationTestParams(string fileName, bool randomOrder = false)
        {
            FileName = fileName;
            RandomOrder = randomOrder;
        }
    }
}