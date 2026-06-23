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
using Microsoft.UI.Xaml.Media;
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
        private bool _isPlaying = false;
        private Random random = new Random();
        private string currentFileName = string.Empty;
        private HashSet<int> correctIndices = new HashSet<int>();
        private BaiduTranslateService _translateService;
        private TranslationLibraryService _translationLibraryService;
        private SpeechSynthesizer? chineseSynthesizer;
        private readonly object _synthesisLock = new object();
        private readonly object _disposalLock = new object();

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }

        private bool _hasAudioDevice = false;

        public DictationTestPage()
        {
            this.InitializeComponent();
            InitializeAudioDevices();

            countdownTimer = new DispatcherTimer();
            countdownTimer.Interval = TimeSpan.FromSeconds(1);
            countdownTimer.Tick += CountdownTimer_Tick;
            _translateService = null!;
            _translationLibraryService = null!;
        }

        private void InitializeAudioDevices()
        {
            bool englishAudioOk = false;
            bool chineseAudioOk = false;

            try
            {
                synthesizer = new SpeechSynthesizer();
                synthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                try
                {
                    synthesizer.SetOutputToDefaultAudioDevice();
                    englishAudioOk = true;
                    System.Diagnostics.Debug.WriteLine("英文 Synthesizer: 音频设备初始化成功");
                }
                catch (Exception ex)
                {
                    englishAudioOk = false;
                    System.Diagnostics.Debug.WriteLine($"英文 Synthesizer SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    synthesizer?.Dispose();
                    synthesizer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"英文 Synthesizer 初始化失败: {ex.Message}");
                synthesizer = null;
                englishAudioOk = false;
            }

            try
            {
                chineseSynthesizer = new SpeechSynthesizer();
                chineseSynthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                try
                {
                    chineseSynthesizer.SetOutputToDefaultAudioDevice();
                    chineseAudioOk = true;
                    System.Diagnostics.Debug.WriteLine("中文 Synthesizer: 音频设备初始化成功");
                }
                catch (Exception ex)
                {
                    chineseAudioOk = false;
                    System.Diagnostics.Debug.WriteLine($"中文 Synthesizer SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    chineseSynthesizer?.Dispose();
                    chineseSynthesizer = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"中文 Synthesizer 初始化失败: {ex.Message}");
                chineseSynthesizer = null;
                chineseAudioOk = false;
            }

            _hasAudioDevice = englishAudioOk || chineseAudioOk;
        }

        private async Task ShowNoAudioDeviceWarningAsync()
        {
            try
            {
                MainWindow.ShowNotification("当前系统没有可用的音频输出设备。\n\n听写测试需要音频设备来播放单词朗读。\n\n请检查：\n1. 是否连接了扬声器或耳机\n2. 音频设备是否被其他程序占用\n3. 声卡驱动是否正常");
                Frame?.GoBack();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示无音频设备提示失败: {ex.Message}");
                Frame?.GoBack();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            try
            {
                // 重新初始化音频设备（Dispose 销毁了 synthesizer，返回到此页时需要重建）
                InitializeAudioDevices();

                // 检查音频设备是否可用
                if (!_hasAudioDevice)
                {
                    System.Diagnostics.Debug.WriteLine("DictationTestPage: 未检测到音频设备，显示警告");
                    await ShowNoAudioDeviceWarningAsync();
                    return;
                }

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
                else if (e.Parameter is WordsPage.DictationTestParamsWithTranslations testParamsWithTranslations)
                {
                    wordList = testParamsWithTranslations.WordList;
                    if (testParamsWithTranslations.RandomOrder)
                    {
                        ShuffleWordList();
                    }
                    totalWords = wordList.Count;
                    currentIndex = 0;
                    correctCount = 0;
                    correctIndices.Clear();
                    isTestActive = true;
                    _readTranslation = testParamsWithTranslations.ReadTranslation;
                    _isPaperMode = testParamsWithTranslations.IsPaperMode;
                    UpdateUI();
                    _ = PlayCurrentWordAndStartCountdown();
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
                    _ = InputTextBox.Focus(FocusState.Programmatic);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNavigatedTo error: {ex.Message}");
                ShowErrorDialog($"页面加载失败: {ex.Message}");
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
                string appDataPath;
                try
                {
                    appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                }
                catch
                {
                    appDataPath = AppContext.BaseDirectory;
                }
                string folderPath = Path.Combine(appDataPath, "wordlist");
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
            try
            {
                if (ProgressText != null)
                    ProgressText.Text = $"第 {currentIndex + 1} / {totalWords} 个";
                
                if (_isPaperMode)
                {
                    if (ScoreText != null)
                        ScoreText.Text = string.Empty;
                    if (InputTextBox != null)
                        InputTextBox.Visibility = Visibility.Collapsed;
                    if (SubmitButton != null)
                        SubmitButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (ScoreText != null)
                        ScoreText.Text = $"正确: {correctCount} / {totalWords}";
                    if (InputTextBox != null)
                    {
                        InputTextBox.Visibility = Visibility.Visible;
                        InputTextBox.Text = string.Empty;
                    }
                    if (SubmitButton != null)
                        SubmitButton.Visibility = Visibility.Visible;
                }
                
                if (CountdownText != null)
                    CountdownText.Text = string.Empty;
                if (SpeakingStatusText != null)
                    SpeakingStatusText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUI error: {ex.Message}");
            }
        }

        private async Task PlayCurrentWordAndStartCountdown()
        {
            lock (_disposalLock)
            {
                if (!isTestActive || currentIndex >= totalWords || _isPlaying) return;
                _isPlaying = true;
            }

            countdownTimer?.Stop();

            var wordPair = wordList[currentIndex];
            string word = wordPair.Word;
            string translation = wordPair.Translation;
            
            try
            {
                // 实时检查并显示翻译
                if (string.IsNullOrEmpty(translation) && _readTranslation)
                {
                    // 自动翻译
                    try
                    {
                        _translateService ??= new BaiduTranslateService();
                        translation = await _translateService.TranslateAsync(word, "en", "zh");
                        if (!string.IsNullOrEmpty(translation))
                        {
                            wordPair.Translation = translation;
                            TranslationText.Text = $"翻译: {translation}";
                            // 保存翻译到翻译库
                            _translationLibraryService ??= new TranslationLibraryService();
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

                lock (_disposalLock)
                {
                    if (isTestActive && currentIndex < totalWords)
                    {
                        StartCountdown();
                    }
                }
            }
            finally
            {
                lock (_disposalLock)
                {
                    _isPlaying = false;
                }
            }
        }

        private async Task SpeakEnglishWordAsync(string word)
        {
            SpeechSynthesizer? localSynthesizer = null;
            
            lock (_disposalLock)
            {
                if (!isTestActive) return;
                localSynthesizer = synthesizer;
            }
            
            if (localSynthesizer == null) return;

            try
            {
                lock (_synthesisLock)
                {
                    try
                    {
                        chineseSynthesizer?.SpeakAsyncCancelAll();
                        localSynthesizer.SpeakAsyncCancelAll();
                    }
                    catch { }
                }

                var englishVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsEnglishVoiceName;
                if (!string.IsNullOrEmpty(englishVoiceName))
                {
                    try
                    {
                        SetEnglishVoice(englishVoiceName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"设置英文语音失败: {ex.Message}");
                    }
                }

                await Task.Delay(50);

                lock (_disposalLock)
                {
                    if (!isTestActive) return;
                    speakTaskSource = new TaskCompletionSource<bool>();
                }
                
                lock (_synthesisLock)
                {
                    try
                    {
                        localSynthesizer.SpeakAsync(word);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"SpeakAsync失败: {ex.Message}");
                        speakTaskSource?.TrySetResult(false);
                    }
                }

                var tcs = speakTaskSource; // 捕获局部引用，防止 Dispose/Submit 并发设为 null
                var completedTask = await Task.WhenAny(tcs!.Task, Task.Delay(8000));
                if (completedTask != tcs.Task)
                {
                    lock (_synthesisLock)
                    {
                        try
                        {
                            localSynthesizer.SpeakAsyncCancelAll();
                        }
                        catch { }
                    }
                    System.Diagnostics.Debug.WriteLine("英文语音播放超时");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"英文语音播放异常: {ex.Message}");
            }
        }

        private async Task SpeakChineseTranslationAsync(string translation)
        {
            SpeechSynthesizer? localChineseSynthesizer = null;
            
            lock (_disposalLock)
            {
                if (!isTestActive) return;
                localChineseSynthesizer = chineseSynthesizer;
            }
            
            if (localChineseSynthesizer == null)
            {
                try
                {
                    lock (_disposalLock)
                    {
                        if (!isTestActive) return;
                        localChineseSynthesizer = new SpeechSynthesizer();
                        localChineseSynthesizer.SpeakCompleted += Synthesizer_SpeakCompleted;
                        localChineseSynthesizer.SetOutputToDefaultAudioDevice();
                        chineseSynthesizer = localChineseSynthesizer;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"中文Synthesizer初始化失败: {ex.Message}");
                    return;
                }
            }
            
            try
            {
                var chineseVoiceName = App.SharedViewModel?.Settings?.Settings?.WindowsTtsChineseVoiceName;
                if (!string.IsNullOrEmpty(chineseVoiceName))
                {
                    try
                    {
                        SetChineseVoice(chineseVoiceName);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"设置中文语音失败: {ex.Message}");
                    }
                }
                
                await Task.Delay(300);
                
                lock (_disposalLock)
                {
                    if (!isTestActive) return;
                    speakTaskSource = new TaskCompletionSource<bool>();
                }
                
                lock (_synthesisLock)
                {
                    try
                    {
                        localChineseSynthesizer.SpeakAsyncCancelAll();
                        localChineseSynthesizer.SpeakAsync(translation);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"中文SpeakAsync失败: {ex.Message}");
                        speakTaskSource?.TrySetResult(false);
                    }
                }
                
                var tcs = speakTaskSource; // 捕获局部引用，防止 Dispose/Submit 并发设为 null
                var completedTask = await Task.WhenAny(tcs!.Task, Task.Delay(10000));
                if (completedTask != tcs.Task)
                {
                    lock (_synthesisLock)
                    {
                        try
                        {
                            localChineseSynthesizer.SpeakAsyncCancelAll();
                        }
                        catch { }
                    }
                    System.Diagnostics.Debug.WriteLine("中文语音播放超时");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"中文语音播放异常: {ex.Message}");
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
            try
            {
                lock (_disposalLock)
                {
                    if (!isTestActive || currentIndex >= totalWords) return;
                }

                countdownTimer?.Stop();

                if (isSpeaking)
                {
                    lock (_synthesisLock)
                    {
                        try
                        {
                            synthesizer?.SpeakAsyncCancelAll();
                            chineseSynthesizer?.SpeakAsyncCancelAll();
                        }
                        catch { }
                    }
                    isSpeaking = false;
                    speakTaskSource = null;
                    SpeakingStatusText.Text = string.Empty;
                }

                lock (_disposalLock)
                {
                    _isPlaying = false;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Submit 异常: {ex.Message}");
            }
        }

        private void AutoSubmit()
        {
            lock (_disposalLock)
            {
                if (!isTestActive || currentIndex >= totalWords) return;
            }

            string input = InputTextBox.Text?.Trim() ?? "";
            string correct = wordList[currentIndex].Word.Trim();

            // Only increment correctCount if this index hasn't been correctly answered before
            if (input.Equals(correct, StringComparison.OrdinalIgnoreCase) && !correctIndices.Contains(currentIndex))
            {
                correctCount++;
                correctIndices.Add(currentIndex);
                ScoreText.Text = $"正确: {correctCount} / {totalWords}";
            }

            lock (_disposalLock)
            {
                _isPlaying = false;
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
            lock (_disposalLock)
            {
                if (!isTestActive || currentIndex >= totalWords) return;
            }
            
            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                lock (_synthesisLock)
                {
                    try
                    {
                        synthesizer?.SpeakAsyncCancelAll();
                        chineseSynthesizer?.SpeakAsyncCancelAll();
                    }
                    catch { }
                }
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }

            lock (_disposalLock)
            {
                _isPlaying = false;
            }
            
            MoveToNextWord();
        }

        private void Replay()
        {
            lock (_disposalLock)
            {
                if (!isTestActive || currentIndex >= totalWords) return;
            }
            
            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                lock (_synthesisLock)
                {
                    try
                    {
                        synthesizer?.SpeakAsyncCancelAll();
                        chineseSynthesizer?.SpeakAsyncCancelAll();
                    }
                    catch { }
                }
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }

            lock (_disposalLock)
            {
                _isPlaying = false;
            }
            
            _ = PlayCurrentWordAndStartCountdown();
        }

        private void Previous()
        {
            lock (_disposalLock)
            {
                if (!isTestActive) return;
                if (currentIndex <= 0) return;
            }

            countdownTimer?.Stop();
            
            if (isSpeaking)
            {
                lock (_synthesisLock)
                {
                    try
                    {
                        synthesizer?.SpeakAsyncCancelAll();
                        chineseSynthesizer?.SpeakAsyncCancelAll();
                    }
                    catch { }
                }
                isSpeaking = false;
                speakTaskSource = null;
                SpeakingStatusText.Text = string.Empty;
            }

            lock (_disposalLock)
            {
                _isPlaying = false;
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
                lock (_synthesisLock)
                {
                    try
                    {
                        synthesizer?.SpeakAsyncCancelAll();
                    }
                    catch { }
                }
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
                // 纸笔模式：调用对话框让用户输入正确数并保存结果
                await ShowPaperModeResultDialogAsync();
            }
            else
            {
                await ShowOnlineModeResultDialogAsync();
            }
        }

        private async Task ShowPaperModeResultDialogAsync()
        {
            int userCorrectCount = 0;

            while (true)
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

                if (result != ContentDialogResult.Primary)
                {
                    return;
                }

                var stackPanel = inputDialog.Content as StackPanel;
                var inputBox = FindChild<NumberBox>(stackPanel);
                userCorrectCount = (int)(inputBox?.Value ?? 0);

                if (userCorrectCount > totalWords)
                {
                    MainWindow.ShowNotification("正确数不能超过总单词数");
                    continue;
                }

                break;
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
            MainWindow.ShowNotification("请先等待朗读结束");
        }

        private static T? FindChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private async Task ShowErrorDialogAsync(string message)
        {
            MainWindow.ShowNotification(message);
            Frame?.GoBack();
        }

        private async Task ShowEmptyListDialogAsync()
        {
            MainWindow.ShowNotification("词库为空，请先添加单词。");
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
                if (string.IsNullOrEmpty(currentUser)) currentUser = "未登录";

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
            SpeechSynthesizer? localSynthesizer = null;
            
            lock (_disposalLock)
            {
                localSynthesizer = synthesizer;
            }
            
            if (localSynthesizer == null) return;

            try
            {
                var installedVoices = localSynthesizer.GetInstalledVoices();

                System.Diagnostics.Debug.WriteLine("=== synthesizer 可用语音列表 ===");
                foreach (var v in installedVoices)
                {
                    System.Diagnostics.Debug.WriteLine($"可用语音: {v.VoiceInfo.Name} | 语言: {v.VoiceInfo.Culture}");
                }

                var targetVoice = installedVoices.FirstOrDefault(v =>
                    v.Enabled && v.VoiceInfo.Name == voiceName);

                if (targetVoice != null)
                {
                    localSynthesizer.SelectVoice(targetVoice.VoiceInfo.Name);
                    System.Diagnostics.Debug.WriteLine($"英文语音已设置为: {voiceName}");
                }
                else
                {
                    var fallbackVoice = installedVoices.FirstOrDefault(v =>
                        v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase));

                    if (fallbackVoice != null)
                    {
                        localSynthesizer.SelectVoice(fallbackVoice.VoiceInfo.Name);
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
                System.Diagnostics.Debug.WriteLine($"错误：找不到名为 '{voiceName}' 的英文语音模型。请检查名称是否正确。详细信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置英文语音失败: {ex.Message}");
            }
        }

        private void SetChineseVoice(string voiceName)
        {
            SpeechSynthesizer? localChineseSynthesizer = null;
            
            lock (_disposalLock)
            {
                localChineseSynthesizer = chineseSynthesizer;
            }
            
            if (localChineseSynthesizer == null) return;

            try
            {
                var installedVoices = localChineseSynthesizer.GetInstalledVoices();

                System.Diagnostics.Debug.WriteLine("=== chineseSynthesizer 可用语音列表 ===");
                foreach (var v in installedVoices)
                {
                    System.Diagnostics.Debug.WriteLine($"可用语音: {v.VoiceInfo.Name} | 语言: {v.VoiceInfo.Culture}");
                }

                var targetVoice = installedVoices.FirstOrDefault(v =>
                    v.Enabled && v.VoiceInfo.Name == voiceName);

                if (targetVoice != null)
                {
                    localChineseSynthesizer.SelectVoice(targetVoice.VoiceInfo.Name);
                    System.Diagnostics.Debug.WriteLine($"中文语音已设置为: {voiceName}");
                }
                else
                {
                    var fallbackVoice = installedVoices.FirstOrDefault(v =>
                        v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase));

                    if (fallbackVoice != null)
                    {
                        localChineseSynthesizer.SelectVoice(fallbackVoice.VoiceInfo.Name);
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
                System.Diagnostics.Debug.WriteLine($"错误：找不到名为 '{voiceName}' 的中文语音模型。请检查名称是否正确。详细信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置中文语音失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (_disposalLock)
            {
                isTestActive = false;
                
                countdownTimer?.Stop();
                countdownTimer = null;
                
                if (synthesizer != null)
                {
                    try
                    {
                        synthesizer.SpeakCompleted -= Synthesizer_SpeakCompleted;
                    }
                    catch { }
                    
                    try
                    {
                        synthesizer.SpeakAsyncCancelAll();
                    }
                    catch { }
                    
                    try
                    {
                        synthesizer.Dispose();
                    }
                    catch { }
                    
                    synthesizer = null;
                }
                
                if (chineseSynthesizer != null)
                {
                    try
                    {
                        chineseSynthesizer.SpeakCompleted -= Synthesizer_SpeakCompleted;
                    }
                    catch { }
                    
                    try
                    {
                        chineseSynthesizer.SpeakAsyncCancelAll();
                    }
                    catch { }
                    
                    try
                    {
                        chineseSynthesizer.Dispose();
                    }
                    catch { }
                    
                    chineseSynthesizer = null;
                }

                try { (_translateService as IDisposable)?.Dispose(); } catch { }
                try { (_translationLibraryService as IDisposable)?.Dispose(); } catch { }
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

