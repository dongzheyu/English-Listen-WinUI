using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Speech.Synthesis;

namespace English_Listen_WinUI.Services
{
    public class ModernDictationService
    {
        private SpeechSynthesizer? _speechService;
        private string _currentVoice = "";
        private string _currentChineseVoice = "";
        private List<WordTranslationPair> _wordList;
        private int _currentIndex;
        private int _readInterval;
        private bool _isRandomOrder;
        private bool _isPaused;
        private bool _isTesting;
        private System.Timers.Timer? _countdownTimer;
        private int _currentCountdown;

        public enum SpeechState
        {
            Idle,
            Speaking,
            Paused,
            Completed
        }

        public event Action<string, string, int, int, bool>? WordChanged;
        public event Action<int>? CountdownChanged;
        public event Action<bool, bool>? TestStateChanged;
        public event Action<bool>? SpeechStatusChanged;
        public event Action? TestCompleted;

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }

        public ModernDictationService()
        {
            try
            {
                _speechService = new SpeechSynthesizer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModernDictationService SpeechSynthesizer 初始化失败: {ex.Message}");
                _speechService = null;
            }

            _wordList = new List<WordTranslationPair>();
            _currentIndex = 0;
            _readInterval = 5;
            _isRandomOrder = false;
            _isPaused = false;
            _isTesting = false;

            try
            {
                _countdownTimer = new System.Timers.Timer(1000);
                _countdownTimer.Elapsed += OnCountdownTimerElapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModernDictationService Timer 初始化失败: {ex.Message}");
            }
        }

        public SpeechState GetCurrentSpeechState()
        {
            return _isTesting ? SpeechState.Speaking : SpeechState.Idle;
        }

        public void SetWords(List<string> words)
        {
            _wordList.Clear();
            foreach (var word in words)
            {
                _wordList.Add(new WordTranslationPair { Word = word, Translation = "" });
            }

            if (_isRandomOrder && _wordList.Count > 0)
            {
                ShuffleWordList();
            }
        }

        public void SetWordsWithTranslations(List<WordTranslationPair> words)
        {
            _wordList.Clear();
            _wordList.AddRange(words);

            if (_isRandomOrder && _wordList.Count > 0)
            {
                ShuffleWordList();
            }
        }

        public void SetRandomOrder(bool randomOrder)
        {
            _isRandomOrder = randomOrder;
            if (_isRandomOrder && _wordList.Count > 0)
            {
                ShuffleWordList();
            }
        }

        public void SetReadInterval(int interval)
        {
            _readInterval = interval;
        }

        public void SetVoice(string voiceName)
        {
            _currentVoice = voiceName;
            if (!string.IsNullOrEmpty(voiceName) && _speechService != null)
            {
                try
                {
                    _speechService.SelectVoice(voiceName);
                }
                catch { }
            }
        }

        public void SetChineseVoice(string voiceName)
        {
            _currentChineseVoice = voiceName;
        }

        public async Task<bool> StartTest(int dictationMode)
        {
            if (_wordList.Count == 0)
                return false;

            _isTesting = true;
            _isPaused = false;
            _currentIndex = 0;

            await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));

            await SpeakCurrentWordAsync();

            return true;
        }

        public async void StopTest()
        {
            _isTesting = false;
            _isPaused = false;
            _countdownTimer?.Stop();
            _speechService?.SpeakAsyncCancelAll();

            await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
        }

        public async void PauseResume()
        {
            if (!_isTesting)
                return;

            _isPaused = !_isPaused;

            if (_isPaused)
            {
                _countdownTimer?.Stop();
            }
            else
            {
                _countdownTimer?.Start();
            }

            await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
        }

        public async void NextWord()
        {
            if (!_isTesting || _currentIndex >= _wordList.Count - 1)
                return;

            _currentIndex++;
            await SpeakCurrentWordAsync();
        }

        public async void PreviousWord()
        {
            if (!_isTesting || _currentIndex <= 0)
                return;

            _currentIndex--;
            await SpeakCurrentWordAsync();
        }

        public async void RepeatWord()
        {
            if (!_isTesting)
                return;

            await SpeakCurrentWordAsync();
        }

        private async Task SpeakCurrentWordAsync()
        {
            if (_currentIndex >= _wordList.Count)
                return;

            var wordPair = _wordList[_currentIndex];
            var word = wordPair.Word;
            var translation = wordPair.Translation;
            var isLastWord = _currentIndex == _wordList.Count - 1;
            await InvokeOnUIThread(() => WordChanged?.Invoke(word, translation, _currentIndex + 1, _wordList.Count, isLastWord));

            await InvokeOnUIThread(() => CountdownChanged?.Invoke(-1));
            await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(true));

            try
            {
                if (_speechService != null)
                {
                    await Task.Run(() => _speechService.Speak(word));

                    if (!string.IsNullOrEmpty(translation))
                    {
                        await Task.Delay(500);

                        if (!string.IsNullOrEmpty(_currentChineseVoice))
                        {
                            try
                            {
                                _speechService.SelectVoice(_currentChineseVoice);
                            }
                            catch { }
                        }

                        await Task.Run(() => _speechService.Speak(translation));

                        if (!string.IsNullOrEmpty(_currentVoice))
                        {
                            try
                            {
                                _speechService.SelectVoice(_currentVoice);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"朗读异常: {ex.Message}");
            }
            finally
            {
                await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(false));

                if (isLastWord)
                {
                    await InvokeOnUIThread(() => TestCompleted?.Invoke());
                }
                else
                {
                    StartCountdown();
                }
            }
        }

        private async void StartCountdown()
        {
            _currentCountdown = _readInterval;
            await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));

            if (!_isPaused)
            {
                _countdownTimer?.Start();
            }
        }

        private async void OnCountdownTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isPaused)
                return;

            _currentCountdown--;

            if (_currentCountdown <= 0)
            {
                _countdownTimer?.Stop();

                if (_currentIndex < _wordList.Count - 1)
                {
                    _currentIndex++;
                    await SpeakCurrentWordAsync();
                }
                else
                {
                    StopTest();
                }
            }
            else
            {
                await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
            }
        }

        private async Task InvokeOnUIThread(Action action)
        {
            try
            {
                // 尝试获取CoreApplication.MainView，如果失败则尝试其他方法
                var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView?.CoreWindow?.Dispatcher;
                
                if (dispatcher != null)
                {
                    await dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () => action());
                }
                else
                {
                    // 如果无法获取UI调度器，直接执行操作（可能在非UI线程）
                    action();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI thread dispatch failed: {ex.Message}");
                // 作为后备方案，直接执行操作
                try
                {
                    action();
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"Direct action execution also failed: {ex2.Message}");
                }
            }
        }

        private void ShuffleWordList()
        {
            var random = new Random();
            for (int i = _wordList.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = _wordList[i];
                _wordList[i] = _wordList[j];
                _wordList[j] = temp;
            }
        }

        public void Dispose()
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();
            _speechService?.Dispose();
        }
    }
}