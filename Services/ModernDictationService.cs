using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private const int MaxWords = 10000;
        private const int MaxWordLength = 256;
        private const int MaxTranslationLength = 2048;

        public enum SpeechState
        {
            Idle,
            Speaking,
            Paused,
            Completed
        }

        private readonly SemaphoreSlim _operationLock = new(1, 1);
        private readonly SemaphoreSlim _speechLock = new(1, 1);
        private readonly DispatcherQueue? _dispatcherQueue;
        private Timer? _countdownTimer;
        private string _currentChineseVoice = string.Empty;
        private int _currentCountdown;
        private int _currentIndex;
        private string _currentVoice = string.Empty;
        private bool _disposed;
        private bool _hasAudioDevice;
        private bool _isPaused;
        private bool _isRandomOrder;
        private bool _isTesting;
        private int _readInterval;
        private int _speechGeneration;
        private SpeechSynthesizer? _speechService;
        private readonly List<WordTranslationPair> _wordList = new();

        public ModernDictationService()
        {
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            try
            {
                _speechService = new SpeechSynthesizer();
                try
                {
                    _speechService.SetOutputToDefaultAudioDevice();
                    _hasAudioDevice = true;
                }
                catch (Exception ex)
                {
                    _hasAudioDevice = false;
                    Debug.WriteLine($"ModernDictationService SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    _speechService.Dispose();
                    _speechService = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModernDictationService SpeechSynthesizer 初始化失败: {ex.Message}");
                _speechService = null;
                _hasAudioDevice = false;
            }

            _readInterval = 5;

            try
            {
                _countdownTimer = new Timer(1000)
                {
                    AutoReset = false
                };
                _countdownTimer.Elapsed += OnCountdownTimerElapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModernDictationService Timer 初始化失败: {ex.Message}");
            }
        }

        public bool HasAudioDevice => _hasAudioDevice;
        public string AudioDeviceStatus => _hasAudioDevice ? "音频设备正常" : "未检测到音频设备";

        public event Action<string, string, int, int, bool>? WordChanged;
        public event Action<int>? CountdownChanged;
        public event Action<bool, bool>? TestStateChanged;
        public event Action<bool>? SpeechStatusChanged;
        public event Action? TestCompleted;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _isTesting = false;
            _isPaused = false;
            _speechGeneration++;

            if (_countdownTimer != null)
            {
                _countdownTimer.Stop();
                _countdownTimer.Elapsed -= OnCountdownTimerElapsed;
                _countdownTimer.Dispose();
                _countdownTimer = null;
            }

            try
            {
                _speechService?.SpeakAsyncCancelAll();
            }
            catch
            {
            }

            _speechService?.Dispose();
            _speechService = null;
        }

        public bool CheckAudioDeviceAvailable()
        {
            if (_disposed || _speechService == null)
                return false;

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
            if (_disposed)
                return SpeechState.Completed;
            return _isTesting ? SpeechState.Speaking : SpeechState.Idle;
        }

        public void SetWords(List<string> words)
        {
            if (_disposed || words == null)
                return;

            _wordList.Clear();
            foreach (var word in words.Take(MaxWords))
            {
                if (string.IsNullOrWhiteSpace(word))
                    continue;

                var normalized = word.Trim();
                if (normalized.Length <= MaxWordLength)
                    _wordList.Add(new WordTranslationPair { Word = normalized, Translation = string.Empty });
            }

            _currentIndex = 0;
            if (_isRandomOrder && _wordList.Count > 0)
                ShuffleWordList();
        }

        public void SetWordsWithTranslations(List<WordTranslationPair> words)
        {
            if (_disposed || words == null)
                return;

            _wordList.Clear();
            foreach (var pair in words.Take(MaxWords))
            {
                if (pair == null || string.IsNullOrWhiteSpace(pair.Word))
                    continue;

                var word = pair.Word.Trim();
                var translation = pair.Translation?.Trim() ?? string.Empty;
                if (word.Length > MaxWordLength || translation.Length > MaxTranslationLength)
                    continue;

                _wordList.Add(new WordTranslationPair { Word = word, Translation = translation });
            }

            _currentIndex = 0;
            if (_isRandomOrder && _wordList.Count > 0)
                ShuffleWordList();
        }

        public void SetRandomOrder(bool randomOrder)
        {
            if (_disposed)
                return;

            _isRandomOrder = randomOrder;
            if (_isRandomOrder && _wordList.Count > 0)
                ShuffleWordList();
        }

        public void SetReadInterval(int interval)
        {
            if (_disposed)
                return;

            _readInterval = Math.Clamp(interval, 1, 60);
        }

        public void SetVoice(string? voiceName)
        {
            if (_disposed)
                return;

            _currentVoice = voiceName?.Trim() ?? string.Empty;
            if (_currentVoice.Length > 256)
                _currentVoice = string.Empty;

            SelectVoice(_currentVoice);
        }

        public void SetChineseVoice(string? voiceName)
        {
            if (_disposed)
                return;

            _currentChineseVoice = voiceName?.Trim() ?? string.Empty;
            if (_currentChineseVoice.Length > 256)
                _currentChineseVoice = string.Empty;
        }

        private void SelectVoice(string voiceName)
        {
            if (_speechService == null || string.IsNullOrEmpty(voiceName))
                return;

            try
            {
                _speechService.SelectVoice(voiceName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置语音失败: {ex.Message}");
            }
        }

        public async Task<bool> StartTest(int dictationMode)
        {
            if (_disposed)
                return false;

            await _operationLock.WaitAsync();
            try
            {
                if (_wordList.Count == 0 || _isTesting)
                    return false;

                _isTesting = true;
                _isPaused = false;
                _currentIndex = 0;
                _speechGeneration++;

                await InvokeOnUIThread(() => TestStateChanged?.Invoke(true, false));
                _ = SpeakCurrentWordAsyncSafe(_speechGeneration);
                return true;
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task StopTestAsync()
        {
            if (_disposed)
                return;

            await _operationLock.WaitAsync();
            try
            {
                StopTestCore();
                await InvokeOnUIThread(() => TestStateChanged?.Invoke(false, false));
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private void StopTestCore()
        {
            _isTesting = false;
            _isPaused = false;
            _speechGeneration++;
            _countdownTimer?.Stop();
            try
            {
                _speechService?.SpeakAsyncCancelAll();
            }
            catch
            {
            }
        }

        public async Task PauseResumeAsync()
        {
            if (_disposed)
                return;

            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting)
                    return;

                _isPaused = !_isPaused;
                if (_isPaused)
                    _countdownTimer?.Stop();
                else
                    _countdownTimer?.Start();

                await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
            }
            finally
            {
                _operationLock.Release();
            }
        }

        public async Task NextWordAsync()
        {
            await MoveWordAsync(1);
        }

        public async Task PreviousWordAsync()
        {
            await MoveWordAsync(-1);
        }

        public async Task RepeatWordAsync()
        {
            if (_disposed)
                return;

            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting)
                    return;

                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();
                _speechGeneration++;
                _ = SpeakCurrentWordAsyncSafe(_speechGeneration);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task MoveWordAsync(int delta)
        {
            if (_disposed)
                return;

            await _operationLock.WaitAsync();
            try
            {
                if (!_isTesting)
                    return;

                var nextIndex = _currentIndex + delta;
                if (nextIndex < 0 || nextIndex >= _wordList.Count)
                    return;

                _countdownTimer?.Stop();
                _speechService?.SpeakAsyncCancelAll();
                _speechGeneration++;
                _currentIndex = nextIndex;
                _ = SpeakCurrentWordAsyncSafe(_speechGeneration);
            }
            finally
            {
                _operationLock.Release();
            }
        }

        private async Task SpeakCurrentWordAsyncSafe(int generation)
        {
            await _speechLock.WaitAsync();
            try
            {
                if (_disposed || !_isTesting || generation != _speechGeneration)
                    return;

                await SpeakCurrentWordAsync(generation);
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

        private async Task SpeakCurrentWordAsync(int generation)
        {
            if (_disposed || _currentIndex >= _wordList.Count || generation != _speechGeneration)
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
                if (_speechService == null || generation != _speechGeneration)
                    return;

                await Task.Run(() => _speechService.Speak(word));
                if (_disposed || generation != _speechGeneration)
                    return;

                if (!string.IsNullOrEmpty(translation))
                {
                    await Task.Delay(500);
                    if (_disposed || generation != _speechGeneration)
                        return;

                    SelectVoice(_currentChineseVoice);
                    await Task.Run(() => _speechService.Speak(translation));
                    if (_disposed || generation != _speechGeneration)
                        return;

                    SelectVoice(_currentVoice);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"朗读异常: {ex.Message}");
            }
            finally
            {
                await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(false));

                if (!(_disposed || generation != _speechGeneration || !_isTesting))
                {
                    if (isLastWord)
                    {
                        _isTesting = false;
                        await InvokeOnUIThread(() => TestCompleted?.Invoke());
                        await InvokeOnUIThread(() => TestStateChanged?.Invoke(false, false));
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
            if (_disposed || !_isTesting || _isPaused)
                return;

            _currentCountdown = Math.Clamp(_readInterval, 1, 60);
            _ = InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
            _countdownTimer?.Start();
        }

        private async void OnCountdownTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            if (_disposed || _isPaused || !_isTesting)
                return;

            try
            {
                await _operationLock.WaitAsync();
                try
                {
                    if (_disposed || _isPaused || !_isTesting)
                        return;

                    _currentCountdown--;
                    if (_currentCountdown > 0)
                    {
                        await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
                        return;
                    }

                    _countdownTimer?.Stop();
                    if (_currentIndex < _wordList.Count - 1)
                    {
                        _speechService?.SpeakAsyncCancelAll();
                        _speechGeneration++;
                        _currentIndex++;
                        _ = SpeakCurrentWordAsyncSafe(_speechGeneration);
                    }
                    else
                    {
                        StopTestCore();
                        await InvokeOnUIThread(() => TestCompleted?.Invoke());
                        await InvokeOnUIThread(() => TestStateChanged?.Invoke(false, false));
                    }
                }
                finally
                {
                    _operationLock.Release();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnCountdownTimerElapsed 异常: {ex.Message}");
            }
        }

        private async Task InvokeOnUIThread(Action action)
        {
            if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_dispatcherQueue.TryEnqueue(() =>
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
            }))
            {
                return;
            }

            await tcs.Task;
        }

        private void ShuffleWordList()
        {
            for (var i = _wordList.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (_wordList[i], _wordList[j]) = (_wordList[j], _wordList[i]);
            }
        }

        public class WordTranslationPair
        {
            public required string Word { get; set; }
            public required string Translation { get; set; }
        }
    }
}