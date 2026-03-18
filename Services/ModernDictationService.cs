using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;

namespace English_Listen_WinUI.Services
{
    public class ModernDictationService
    {
        private SpeechSynthesizer _speechSynthesizer;
        private List<string> _wordList;
        private int _currentIndex;
        private int _readInterval;
        private bool _isRandomOrder;
        private bool _isPaused;
        private bool _isTesting;
        private System.Timers.Timer _countdownTimer;
        private int _currentCountdown;
        
        public event Action<string, int, int>? WordChanged;
        public event Action<int>? CountdownChanged;
        public event Action<bool, bool>? TestStateChanged;
        public event Action<bool>? SpeechStatusChanged;
        
        public ModernDictationService()
        {
            _speechSynthesizer = new SpeechSynthesizer();
            _wordList = new List<string>();
            _currentIndex = 0;
            _readInterval = 5;
            _isRandomOrder = false;
            _isPaused = false;
            _isTesting = false;
            
            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += OnCountdownTimerElapsed;
        }
        
        public void SetWords(List<string> words)
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
        
        public async Task<bool> StartTest(int dictationMode)
        {
            if (_wordList.Count == 0)
                return false;
                
            _isTesting = true;
            _isPaused = false;
            _currentIndex = 0;
            
            await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
            
            // Start with the first word
            await SpeakCurrentWordAsync();
            
            return true;
        }
        
        public async void StopTest()
        {
            _isTesting = false;
            _isPaused = false;
            _countdownTimer.Stop();
            
            await InvokeOnUIThread(() => TestStateChanged?.Invoke(_isTesting, _isPaused));
        }
        
        public async void PauseResume()
        {
            if (!_isTesting)
                return;
                
            _isPaused = !_isPaused;
            
            if (_isPaused)
            {
                _countdownTimer.Stop();
            }
            else
            {
                _countdownTimer.Start();
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
                
            var word = _wordList[_currentIndex];
            await InvokeOnUIThread(() => WordChanged?.Invoke(word, _currentIndex + 1, _wordList.Count));
            
            // Show "正在朗读" during speech
            await InvokeOnUIThread(() => CountdownChanged?.Invoke(-1)); // -1 indicates "正在朗读"
            
            await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(true));
            
            try
            {
                var stream = await _speechSynthesizer.SynthesizeTextToStreamAsync(word);
                // For now, we'll simulate speech completion
                await Task.Delay(1000); // Simulate speech duration
            }
            catch
            {
                // Fallback: simulate speech
                await Task.Delay(1000);
            }
            
            await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(false));
            
            // Start countdown for next word
            StartCountdown();
        }
        
        private async void StartCountdown()
        {
            _currentCountdown = _readInterval;
            await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
            
            if (!_isPaused)
            {
                _countdownTimer.Start();
            }
        }
        
        private async void OnCountdownTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (_isPaused)
                return;
                
            _currentCountdown--;
            
            if (_currentCountdown <= 0)
            {
                _countdownTimer.Stop();
                
                // Move to next word or stop if last word
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
                // Invoke on UI thread
                await InvokeOnUIThread(() => CountdownChanged?.Invoke(_currentCountdown));
            }
        }
        
        private async Task InvokeOnUIThread(Action action)
        {
            try
            {
                // Use Windows.ApplicationModel.Core.CoreApplication.MainView for UI thread dispatch
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    Windows.UI.Core.CoreDispatcherPriority.Normal,
                    () => action());
            }
            catch (Exception ex)
            {
                // If UI thread dispatch fails, the app might be shutting down
                System.Diagnostics.Debug.WriteLine($"UI thread dispatch failed: {ex.Message}");
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
            _speechSynthesizer?.Dispose();
        }
    }
}