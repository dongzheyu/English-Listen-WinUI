using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;

namespace English_Listen_WinUI.ViewModels
{
    public class ModernDictationViewModel : ViewModelBase
    {
        private readonly ModernDictationService _dictationService;
        private readonly SettingsService _settingsService;
        
        private string _currentWord = "";
        private int _countdown = 0;
        private bool _isPaused = false;
        private bool _isTesting = false;
        private int _currentIndex = 0;
        private int _totalWords = 0;
        private string _countdownText = "";
        private bool _isSpeaking = false;
        private List<string> _wordList = new();
        
        // Settings
        private int _readInterval = 5;
        private bool _isRandomOrder = false;
        private string _currentVoice = "Default";
        
        public ModernDictationViewModel()
        {
            _dictationService = new ModernDictationService();
            _settingsService = new SettingsService();
            
            // Wire up events
            _dictationService.WordChanged += OnWordChanged;
            _dictationService.CountdownChanged += OnCountdownChanged;
            _dictationService.TestStateChanged += OnTestStateChanged;
            _dictationService.SpeechStatusChanged += OnSpeechStatusChanged;
            
            // Initialize commands
            StartTestCommand = new RelayCommand(StartTest, CanStartTest);
            StopTestCommand = new RelayCommand(StopTest, () => IsTesting);
            NextWordCommand = new RelayCommand(NextWord, () => IsTesting);
            PreviousWordCommand = new RelayCommand(PreviousWord, () => IsTesting);
            RepeatWordCommand = new RelayCommand(RepeatWord, () => IsTesting);
            PauseResumeCommand = new RelayCommand(PauseResume, () => IsTesting);
            
            _ = InitializeAsync();
        }
        
        #region Properties
        
        public string CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }
        
        public int Countdown
        {
            get => _countdown;
            set 
            {
                if (SetProperty(ref _countdown, value))
                {
                    UpdateCountdownText();
                }
            }
        }
        
        public string CountdownText
        {
            get => _countdownText;
            set => SetProperty(ref _countdownText, value);
        }
        
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }
        
        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }
        
        public int CurrentIndex
        {
            get => _currentIndex;
            set => SetProperty(ref _currentIndex, value);
        }
        
        public int TotalWords
        {
            get => _totalWords;
            set => SetProperty(ref _totalWords, value);
        }
        
        public bool IsSpeaking
        {
            get => _isSpeaking;
            set => SetProperty(ref _isSpeaking, value);
        }
        
        public int ReadInterval
        {
            get => _readInterval;
            set 
            {
                if (SetProperty(ref _readInterval, value))
                {
                    _dictationService.SetReadInterval(value);
                    _settingsService.Settings.ReadInterval = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }
        
        public bool IsRandomOrder
        {
            get => _isRandomOrder;
            set 
            {
                if (SetProperty(ref _isRandomOrder, value))
                {
                    _dictationService.SetRandomOrder(value);
                    _settingsService.Settings.IsRandomOrder = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }
        
        public string CurrentVoice
        {
            get => _currentVoice;
            set => SetProperty(ref _currentVoice, value);
        }
        
        public List<string> WordList
        {
            get => _wordList;
            set 
            {
                SetProperty(ref _wordList, value);
                _dictationService.SetWords(value);
                TotalWords = value.Count;
            }
        }
        
        #endregion
        
        #region Commands
        
        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand NextWordCommand { get; }
        public ICommand PreviousWordCommand { get; }
        public ICommand RepeatWordCommand { get; }
        public ICommand PauseResumeCommand { get; }
        
        #endregion
        
        #region Event Handlers
        
        private void OnWordChanged(string word, int currentIndex, int totalWords)
        {
            CurrentWord = word;
            CurrentIndex = currentIndex - 1; // Convert to 0-based
            TotalWords = totalWords;
        }
        
        private void OnCountdownChanged(int countdown)
        {
            Countdown = countdown;
        }
        
        private void OnTestStateChanged(bool isTesting, bool isPaused)
        {
            IsTesting = isTesting;
            IsPaused = isPaused;
        }
        
        private void OnSpeechStatusChanged(bool isSpeaking)
        {
            IsSpeaking = isSpeaking;
        }
        
        #endregion
        
        #region Command Implementations
        
        private bool CanStartTest()
        {
            return WordList.Count > 0 && !IsTesting;
        }
        
        private async void StartTest()
        {
            if (WordList.Count == 0) return;
            
            // Set up the service
            _dictationService.SetWords(WordList);
            _dictationService.SetRandomOrder(IsRandomOrder);
            _dictationService.SetReadInterval(ReadInterval);
            
            await _dictationService.StartTest(0); // 0 = paper dictation mode
        }
        
        private void StopTest()
        {
            _dictationService.StopTest();
        }
        
        private void NextWord()
        {
            _dictationService.NextWord();
        }
        
        private void PreviousWord()
        {
            _dictationService.PreviousWord();
        }
        
        private void RepeatWord()
        {
            _dictationService.RepeatWord();
        }
        
        private void PauseResume()
        {
            _dictationService.PauseResume();
        }
        
        #endregion
        
        #region Helper Methods
        
        private void UpdateCountdownText()
        {
            if (IsSpeaking)
            {
                CountdownText = "正在朗读...";
            }
            else if (Countdown == -1)
            {
                CountdownText = "准备朗读...";
            }
            else if (Countdown > 0)
            {
                CountdownText = $"倒计时: {Countdown}秒";
            }
            else
            {
                CountdownText = "";
            }
        }
        
        private async Task InitializeAsync()
        {
            await _settingsService.LoadSettingsAsync();
            ReadInterval = _settingsService.Settings.ReadInterval;
            IsRandomOrder = _settingsService.Settings.IsRandomOrder;
        }
        
        public void LoadWordsFromText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                WordList = new List<string>();
                return;
            }
            
            var words = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();
            
            WordList = words;
        }
        
        public void Dispose()
        {
            _dictationService?.Dispose();
        }
        
        #endregion
    }
}