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
using English_Listen_WinUI;

namespace English_Listen_WinUI.ViewModels
{
    public class ModernDictationViewModel : ViewModelBase
    {
        private readonly ModernDictationService _dictationService;
        private readonly SettingsService _settingsService;
        
        private string _currentWord = "";
        private string _currentTranslation = "";
        private int _countdown = 0;
        private bool _isPaused = false;
        private bool _isTesting = false;
        private int _currentIndex = 0;
        private int _totalWords = 0;
        private string _countdownText = "";
        private bool _isSpeaking = false;
        private List<string> _wordList = new();
        private bool _showWordList = true;
        private bool _isFullScreen = false;
        private bool _isSidebarVisible = true;
        private bool _isTestCompleted = false;

        public event Action<bool>? SidebarVisibilityChanged;
        public event Action? NavigateToHome;
        
        // Settings
        private int _readInterval = 5;
        private bool _isRandomOrder = false;
        private string _englishVoice = "";
        private string _chineseVoice = "";
        
        // Voice lists
        private List<Models.VoiceInfo> _englishVoices = new();
        private List<Models.VoiceInfo> _chineseVoices = new();
        
        public ModernDictationViewModel()
        {
            _dictationService = new ModernDictationService();
            _settingsService = App.SharedViewModel?.Settings ?? new SettingsService();
            
            // Wire up events
            _dictationService.WordChanged += OnWordChanged;
            _dictationService.CountdownChanged += OnCountdownChanged;
            _dictationService.TestStateChanged += OnTestStateChanged;
            _dictationService.SpeechStatusChanged += OnSpeechStatusChanged;
            _dictationService.TestCompleted += OnTestCompleted;
            
            // Initialize commands
            StartTestCommand = new RelayCommand(StartTest, CanStartTest);
            StopTestCommand = new RelayCommand(StopTest, () => IsTesting);
            NextWordCommand = new RelayCommand(NextWord, () => IsTesting);
            PreviousWordCommand = new RelayCommand(PreviousWord, () => IsTesting);
            RepeatWordCommand = new RelayCommand(RepeatWord, () => IsTesting);
            PauseResumeCommand = new RelayCommand(PauseResume, () => IsTesting);
            ReturnToHomeCommand = new RelayCommand(ReturnToHome);
            ShowAnswersCommand = new RelayCommand(ShowAnswers, () => IsTestCompleted);
            
            _ = InitializeAsync();
            
            // Load words from main view model
            LoadWordsFromMainViewModel();
            
            // Load available voices
            LoadAvailableVoices();
            
            // 检查音频设备
            if (!_dictationService.HasAudioDevice)
            {
                System.Diagnostics.Debug.WriteLine("ModernDictationViewModel: 警告 - 未检测到音频设备");
            }
        }

        public bool HasAudioDevice => _dictationService.HasAudioDevice;

        public string AudioDeviceStatus => _dictationService.AudioDeviceStatus;
        
        #region Properties
        
        public string CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }
        
        public string CurrentTranslation
        {
            get => _currentTranslation;
            set => SetProperty(ref _currentTranslation, value);
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
        
        public bool IsLastWord { get; set; }
        
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
        
        public string EnglishVoice
        {
            get => _englishVoice;
            set => SetProperty(ref _englishVoice, value);
        }
        
        public string ChineseVoice
        {
            get => _chineseVoice;
            set => SetProperty(ref _chineseVoice, value);
        }
        
        public List<Models.VoiceInfo> EnglishVoices
        {
            get => _englishVoices;
            private set => SetProperty(ref _englishVoices, value);
        }
        
        public List<Models.VoiceInfo> ChineseVoices
        {
            get => _chineseVoices;
            private set => SetProperty(ref _chineseVoices, value);
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
        
        public bool ShowWordList
        {
            get => _showWordList;
            set => SetProperty(ref _showWordList, value);
        }
        
        public bool IsFullScreen
        {
            get => _isFullScreen;
            set => SetProperty(ref _isFullScreen, value);
        }
        
        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set
            {
                if (SetProperty(ref _isSidebarVisible, value))
                {
                    SidebarVisibilityChanged?.Invoke(value);
                }
            }
        }
        
        public bool IsTestCompleted
        {
            get => _isTestCompleted;
            set => SetProperty(ref _isTestCompleted, value);
        }
        
        public Action ShowAnswersAction { get; set; } = null!;
        
        #endregion
        
        #region Commands
        
        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand NextWordCommand { get; }
        public ICommand PreviousWordCommand { get; }
        public ICommand RepeatWordCommand { get; }
        public ICommand PauseResumeCommand { get; }
        public ICommand ReturnToHomeCommand { get; }
        public ICommand ShowAnswersCommand { get; }
        
        #endregion
        
        #region Event Handlers
        
        private void OnWordChanged(string word, string translation, int currentIndex, int totalWords, bool isLastWord)
        {
            CurrentWord = word;
            CurrentTranslation = translation;
            CurrentIndex = currentIndex - 1;
            TotalWords = totalWords;
            IsLastWord = isLastWord;
        }
        
        private void OnCountdownChanged(int countdown)
        {
            Countdown = countdown;
        }
        
        private void OnTestStateChanged(bool isTesting, bool isPaused)
        {
            IsTesting = isTesting;
            IsPaused = isPaused;
            if (!isTesting)
            {
                IsTestCompleted = false;
            }
        }
        
        private void OnSpeechStatusChanged(bool isSpeaking)
        {
            IsSpeaking = isSpeaking;
        }

        private void OnTestCompleted()
        {
            IsTestCompleted = true;
            IsTesting = false;
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
            
            // Enable full-screen mode and hide word list when starting test
            IsFullScreen = true;
            ShowWordList = false;
            IsSidebarVisible = false; // Hide sidebar
            
            // Set up service with words and translations
            _dictationService.SetWords(WordList);
            _dictationService.SetRandomOrder(IsRandomOrder);
            _dictationService.SetReadInterval(ReadInterval);
            
            // Set English voice for reading
            if (!string.IsNullOrEmpty(EnglishVoice))
            {
                _dictationService.SetVoice(EnglishVoice);
            }
            
            // Set Chinese voice for reading translations
            if (!string.IsNullOrEmpty(ChineseVoice))
            {
                _dictationService.SetChineseVoice(ChineseVoice);
            }
            
            await _dictationService.StartTest(0); // 0 = paper dictation mode
        }
        
        private async void StopTest()
        {
            await _dictationService.StopTestAsync();
            
            // Disable full-screen mode and show word list again when test stops
            IsFullScreen = false;
            ShowWordList = true;
            IsSidebarVisible = true; // Show sidebar
        }
        
        private async void NextWord()
        {
            await _dictationService.NextWordAsync();
        }
        
        private async void PreviousWord()
        {
            await _dictationService.PreviousWordAsync();
        }
        
        private async void RepeatWord()
        {
            await _dictationService.RepeatWordAsync();
        }
        
        private async void PauseResume()
        {
            await _dictationService.PauseResumeAsync();
        }
        
        private void ReturnToHome()
        {
            // Reset sidebar visibility
            IsSidebarVisible = true;
            
            // Navigate to home
            NavigateToHome?.Invoke();
        }
        
        private void ShowAnswers()
        {
            // Show the word list as answers
            ShowAnswersAction?.Invoke();
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
        
        private void LoadWordsFromMainViewModel()
        {
            var mainViewModel = App.SharedViewModel;
            if (mainViewModel != null && mainViewModel.CurrentWords != null)
            {
                WordList = new List<string>(mainViewModel.CurrentWords);
            }
        }
        
        private void LoadAvailableVoices()
        {
            // Get available voices from speech service
            var speechService = App.SharedViewModel?.SpeechService;
            if (speechService != null)
            {
                var voices = speechService.GetWindowsTtsVoices();
                
                // Separate English and Chinese voices
                EnglishVoices = voices.Where(v => v.Culture?.StartsWith("en") == true).ToList();
                ChineseVoices = voices.Where(v => v.Culture?.StartsWith("zh") == true).ToList();
                
                // Set default voices
                if (EnglishVoices.Count > 0)
                {
                    EnglishVoice = EnglishVoices[0].DisplayName;
                }
                if (ChineseVoices.Count > 0)
                {
                    ChineseVoice = ChineseVoices[0].DisplayName;
                }
            }
        }
        
        public void Dispose()
        {
            try
            {
                _dictationService?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dispose失败: {ex.Message}");
            }
        }
        
        #endregion
    }
}