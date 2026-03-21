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

namespace English_Listen_WinUI.Views
{
    public sealed partial class DictationTestPage : Page
    {
        private List<WordTranslationPair> wordList = new List<WordTranslationPair>();
        private int currentIndex;
        private int correctCount;
        private int totalWords;
        private int countdownSeconds = 10;
        private int remainingSeconds;
        private DispatcherTimer? countdownTimer;
        private SpeechSynthesizer? synthesizer;
        private TaskCompletionSource<bool>? speakTaskSource;
        private bool isTestActive;
        private bool isSpeaking = false;
        private Random random = new Random();
        private string currentFileName = string.Empty;
        private HashSet<int> correctIndices = new HashSet<int>();
        private BaiduTranslateService _translateService;
        private SpeechSynthesizer? chineseSynthesizer;

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }

        public DictationTestPage()
        {
            this.InitializeComponent();
            synthesizer = new SpeechSynthesizer();
            synthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            _translateService = new BaiduTranslateService();
            InputTextBox.Focus(FocusState.Programmatic);
        }

        private void Synthesizer_SpeakCompleted(object? sender, SpeakCompletedEventArgs e)
        {
            speakTaskSource?.TrySetResult(true);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var mainWindow = App.MainWindow as MainWindow;
            mainWindow?.SetSidebarVisibility(false);

            if (e.Parameter is DictationTestParams testParams)
            {
                currentFileName = testParams.FileName;
                LoadWordList(currentFileName, testParams.RandomOrder);
            }
            else if (e.Parameter.GetType().Name == "DictationTestParamsWithTranslations")
            {
                var wordListProperty = e.Parameter.GetType().GetProperty("WordList");
                var randomOrderProperty = e.Parameter.GetType().GetProperty("RandomOrder");
                
                if (wordListProperty != null && randomOrderProperty != null)
                {
                    var wordListValue = wordListProperty.GetValue(e.Parameter) as List<WordTranslationPair>;
                    var randomOrderValue = (bool)(randomOrderProperty.GetValue(e.Parameter) ?? false);
                    
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
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            var mainWindow = App.MainWindow as MainWindow;
            mainWindow?.SetSidebarVisibility(true);

            countdownTimer?.Stop();
            synthesizer?.SpeakAsyncCancelAll();
            chineseSynthesizer?.SpeakAsyncCancelAll();
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
            ScoreText.Text = $"正确: {correctCount} / {totalWords}";
            InputTextBox.Text = string.Empty;
            CountdownText.Text = string.Empty;
            SpeakingStatusText.Text = string.Empty;
            TranslationText.Text = string.Empty;
        }

        private async Task PlayCurrentWordAndStartCountdown()
        {
            if (!isTestActive || currentIndex >= totalWords) return;

            countdownTimer?.Stop();

            var wordPair = wordList[currentIndex];
            string word = wordPair.Word;
            string translation = wordPair.Translation;
            
            SpeakingStatusText.Text = "正在朗读...";
            isSpeaking = true;
            
            try
            {
                if (synthesizer != null)
                {
                    speakTaskSource = new TaskCompletionSource<bool>();
                    synthesizer.SpeakAsyncCancelAll();
                    synthesizer.SpeakAsync(word);
                    
                    var completedTask = await Task.WhenAny(speakTaskSource.Task, Task.Delay(10000));
                    if (completedTask != speakTaskSource.Task)
                    {
                        synthesizer.SpeakAsyncCancelAll();
                        System.Diagnostics.Debug.WriteLine("语音播放超时");
                    }
                }
                
                if (!string.IsNullOrEmpty(translation))
                {
                    if (chineseSynthesizer == null)
                    {
                        chineseSynthesizer = new SpeechSynthesizer();
                        chineseSynthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                    }
                    
                    await Task.Delay(300);
                    
                    speakTaskSource = new TaskCompletionSource<bool>();
                    chineseSynthesizer.SpeakAsyncCancelAll();
                    chineseSynthesizer.SpeakAsync(translation);
                    
                    var completedTask = await Task.WhenAny(speakTaskSource.Task, Task.Delay(10000));
                    if (completedTask != speakTaskSource.Task)
                    {
                        chineseSynthesizer.SpeakAsyncCancelAll();
                    }
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

            TranslationText.Text = !string.IsNullOrEmpty(translation) ? $"翻译: {translation}" : "";

            if (isTestActive && currentIndex < totalWords)
            {
                StartCountdown();
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

            // 如果正在播放语音，先取消
            if (isSpeaking)
            {
                synthesizer?.SpeakAsyncCancelAll();
            }

            // 等待当前语音播放任务完成
            if (speakTaskSource != null)
            {
                try
                {
                    await speakTaskSource.Task;
                }
                catch
                {
                    // 忽略取消异常
                }
            }

            double accuracy = totalWords > 0 ? (double)correctCount / totalWords * 100 : 0;

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

        private void ShowEmptyListDialog()
        {
            _ = ShowEmptyListDialogAsync();
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
            await EndTestAsync();
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
