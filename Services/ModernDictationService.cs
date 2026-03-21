using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace English_Listen_WinUI.Services
{
    public class ModernDictationService
    {
        private WindowsTtsService _speechService;
        private string _currentVoice = "";
        private string _currentChineseVoice = "";
        private List<WordTranslationPair> _wordList;
        private int _currentIndex;
        private int _readInterval;
        private bool _isRandomOrder;
        private bool _isPaused;
        private bool _isTesting;
        private System.Timers.Timer _countdownTimer;
        private int _currentCountdown;
        
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
            _speechService = new WindowsTtsService();
            _wordList = new List<WordTranslationPair>();
            _currentIndex = 0;
            _readInterval = 5;
            _isRandomOrder = false;
            _isPaused = false;
            _isTesting = false;
            
            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += OnCountdownTimerElapsed;
            
            // Subscribe to speech service events for fine-grained control
            _speechService.StateChanged += OnSpeechStateChanged;
            _speechService.SpeakCompleted += OnSpeakCompleted;
        }

        private void OnSpeechStateChanged(WindowsTtsService.SpeechState state)
        {
            System.Diagnostics.Debug.WriteLine($"Speech state changed to: {state}");
            // This event is triggered when the speech state changes
            // Use this for reactive logic like enabling buttons when reading completes
        }

        private void OnSpeakCompleted()
        {
            System.Diagnostics.Debug.WriteLine("Speech completed");
            // This event is triggered after an async reading task is completely finished
            // Use this for actions that need to happen after speech finishes
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
            if (!string.IsNullOrEmpty(voiceName))
            {
                _speechService.SelectVoice(voiceName);
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
            
            // Start with the first word immediately
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
                _speechService.Pause();
            }
            else
            {
                _countdownTimer.Start();
                _speechService.Resume();
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
            
            // Show "正在朗读" immediately when starting speech
            await InvokeOnUIThread(() => CountdownChanged?.Invoke(-1)); // -1 indicates "正在朗读"
            await InvokeOnUIThread(() => SpeechStatusChanged?.Invoke(true));
            
            try
            {
                // Use State attribute to check current state before starting to avoid conflicts
                if (_speechService.CurrentState == WindowsTtsService.SpeechState.Speaking)
                {
                    System.Diagnostics.Debug.WriteLine("Speech service is currently speaking, waiting...");
                    // Wait for current speech to complete
                    await Task.Delay(500);
                }
                
                // Read the word
                await _speechService.SpeakAsync(word);
                
                // If translation exists, read it after the word
                if (!string.IsNullOrEmpty(translation))
                {
                    await Task.Delay(500); // Small pause between word and translation
                    
                    // Switch to Chinese voice if available
                    if (!string.IsNullOrEmpty(_currentChineseVoice))
                    {
                        _speechService.SelectVoice(_currentChineseVoice);
                    }
                    
                    await _speechService.SpeakAsync(translation);
                    
                    // Switch back to English voice
                    if (!string.IsNullOrEmpty(_currentVoice))
                    {
                        _speechService.SelectVoice(_currentVoice);
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
                
                // If this was the last word and speech is done, trigger completion
                if (isLastWord)
                {
                    await InvokeOnUIThread(() => TestCompleted?.Invoke());
                }
                else
                {
                    // Start countdown for next word after speech completes
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
            
            // Unsubscribe from events
            if (_speechService != null)
            {
                _speechService.StateChanged -= OnSpeechStateChanged;
                _speechService.SpeakCompleted -= OnSpeakCompleted;
                _speechService?.Dispose();
            }
        }

        public WindowsTtsService.SpeechState GetCurrentSpeechState()
        {
            return _speechService?.CurrentState ?? WindowsTtsService.SpeechState.Idle;
        }
    }
}