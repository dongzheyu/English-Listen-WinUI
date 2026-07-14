using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.UI.Dispatching;
using Timer = System.Timers.Timer;

namespace English_Listen_WinUI.Services
{
    public class ModernDictationService : IDisposable
    {
        public enum SpeechState
        {
            Idle,
            Speaking,
            Paused,
            Completed
        }

        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _speechLock = new SemaphoreSlim(1, 1);
        private Timer? _countdownTimer;
        private string _currentChineseVoice = "";
        private int _currentCountdown;
        private int _currentIndex;
        private string _currentVoice = "";
        private bool _disposed;
        private bool _hasAudioDevice = false;
        private bool _isPaused;
        private bool _isRandomOrder;
        private bool _isTesting;
        private int _readInterval;
        private int _speechGeneration = 0;
        private SpeechSynthesizer? _speechService;
        private List<WordTranslationPair> _wordList;

        public ModernDictationService()
        {
            try
            {
                _speechService = new SpeechSynthesizer();
                try
                {
                    _speechService.SetOutputToDefaultAudioDevice();
                    _hasAudioDevice = true;
                    Debug.WriteLine("ModernDictationService: 音频设备初始化成功");
                }
                catch (Exception ex)
                {
                    _hasAudioDevice = false;
                    Debug.WriteLine($"ModernDictationService SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    _speechService?.Dispose();
                    _speechService = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModernDictationService SpeechSynthesizer 初始化失败: {ex.Message}");
                _speechService = null;
                _hasAudioDevice = false;
            }

            _wordList = new List<WordTranslationPair>();
            _currentIndex = 0;
            _readInterval = 5;
            _isRandomOrder = false;
            _isPaused = false;
            _isTesting = false;

            try
            {
                _countdownTimer = new Timer(1000);
                _countdownTimer.Elapsed += OnCountdownTimerElapsed;
                _countdownTimer.AutoReset = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModernDictationService Timer 初始化失败: {ex.Message}");
            }
        }

        public bool HasAudioDevice => _hasAudioDevice;

        public string AudioDeviceStatus => _hasAudioDevice ? "音频设备正常" : "未检测到音频设备";

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Elapsed -= OnCountdownTimerElapsed;
                _countdownTimer.Dispose();
            }

            _speechService?.Dispose();
            _operationLock?.Dispose();
            _speechLock?.Dispose();
        }

        public event Action<string, string, int, int, bool>? WordChanged;
        public event Action<int>? CountdownChanged;
        public event Action<bool, bool>? TestStateChanged;
        public event Action<bool>? SpeechStatusChanged;
        public event Action? TestCompleted;

        public bool CheckAudioDeviceAvailable()
        {
            if (_speechService == null) return false;

            try
            {
                _speechService.SetOutputToDefaultAudioDevice();
                _hasAudioDevice = true;
                return true;
            }
            catch
            {
                _hasAudioDevice = false;
                return false;
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
                catch
                {
                }
            }
        }

        public void SetChineseVoice(string voiceName)
        {
            _currentChineseVoice = voiceName;
        }

        public async Task<bool> StartTest(int dictationMode)
        {
            await _operationLock.WaitAsync();
            try
            {
                if (_wordList.Count == 0)
                    return false;

                _isTesting = true;
                _isPaused = false;
                _currentIndex = 0;

                await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));

                // fire and forget with proper error handling
                _ = SpeakCurrentWordAsyncSafe();
                return true;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task StopTestAsync()
        {
            await _operationLock.WaitAsync();
            try
            {
                _isTesting = false;
                _isPaused = false;
                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();

                await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task PauseResumeAsync()
        {
            await _operationLock.WaitAsync();
            try
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
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task NextWordAsync()
        {
            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting || _currentIndex >= _wordList.Count - 1)
                    return;

                // ponytail: stop countdown + speech before switching
                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();
                _speechGeneration++;

                _currentIndex++;
                _ = SpeakCurrentWordAsyncSafe();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task PreviousWordAsync()
        {
            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting || _currentIndex <= 0)
                    return;

                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();
                _speechGeneration++;

                _currentIndex--;
                _ = SpeakCurrentWordAsyncSafe();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task RepeatWordAsync()
        {
            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting)
                    return;

                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();
                _speechGeneration++;

                _ = SpeakCurrentWordAsyncSafe();
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task SpeakCurrentWordAsyncSafe()
        {
            await _speechLock.WaitAsync();
            try
            {
                await SpeakCurrentWordAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SpeakCurrentWordAsyncSafe 异常: {ex.Message}");
            }
            finally
            {
                _speechLock.Release();
            }
        }

        private async Task SpeakCurrentWordAsync()
        {
            if (_currentIndex >= _wordList.Count)
                return;

            var myGeneration = _speechGeneration; // ponytail: stale-gen guard
            var wordPair = _wordList[_currentIndex];
            var word = wordPair.Word;
            var translation = wordPair.Translation;
            var isLastWord = _currentIndex == _wordList.Count - 1;
            await InvokeOnUIThread(() =>
                WordChanged?.Invoke(word, translation, _currentIndex + 1, _wordList.Count, isLastWord));

            await InvokeOnUIThread(() => CountdownChanged?.Invoke(-1));
            await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(true));

            try
            {
                if (_speechService != null)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            _speechService.Speak(word);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"英文朗读异常: {ex.Message}");
                        }
                    });

                    // ponytail: check gen before continuing — stale call bails out
                    if (_speechGeneration != myGeneration) return;

                    if (!string.IsNullOrEmpty(translation))
                    {
                        await Task.Delay(500);
                        if (_speechGeneration != myGeneration) return;

                        if (!string.IsNullOrEmpty(_currentChineseVoice))
                        {
                            try
                            {
                                _speechService.SelectVoice(_currentChineseVoice);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"设置中文语音失败: {ex.Message}");
                            }
                        }

                        await Task.Run(() =>
                        {
                            try
                            {
                                _speechService.Speak(translation);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"中文朗读异常: {ex.Message}");
                            }
                        });

                        if (_speechGeneration != myGeneration) return;

                        if (!string.IsNullOrEmpty(_currentVoice))
                        {
                            try
                            {
                                _speechService.SelectVoice(_currentVoice);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"恢复英文语音失败: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"朗读异常: {ex.Message}");
            }
            finally
            {
                await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(false));

                // ponytail: only fire events if this gen is still current
                if (_speechGeneration == myGeneration)
                {
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
        }

        private void StartCountdown()
        {
            _currentCountdown = _readInterval;
            _ = InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));

            if (!_isPaused)
            {
                _countdownTimer?.Start();
            }
        }

        private async void OnCountdownTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_isPaused)
                return;

            await _operationLock.WaitAsync();
            try
            {
                _currentCountdown--;

                if (_currentCountdown <= 0)
                {
                    _countdownTimer?.Stop();

                    if (_currentIndex < _wordList.Count - 1)
                    {
                        _speechService?.SpeakAsyncCancelAll();
                        _speechGeneration++;
                        _currentIndex++;
                        _ = SpeakCurrentWordAsyncSafe();
                    }
                    else
                    {
                        await StopTestAsync();
                    }
                }
                else
                {
                    await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCountdownTimerElapsed 异常: {ex.Message}");
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task InvokeOnUIThread(Action action)
        {
            try
            {
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();

                if (dispatcherQueue != null)
                {
                    var tcs = new TaskCompletionSource();
                    var result = dispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            action();
                            tcs.TrySetResult();
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    });

                    if (result)
                    {
                        await tcs.Task;
                    }
                    else
                    {
                        action();
                    }
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UI thread dispatch failed: {ex.Message}");
                try
                {
                    action();
                }
                catch (Exception ex2)
                {
                    Debug.WriteLine($"Direct action execution also failed: {ex2.Message}");
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

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }
    }
}