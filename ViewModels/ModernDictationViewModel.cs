using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;
using English_Listen_WinUI;

namespace English_Listen_WinUI.ViewModels
{
    public class ModernDictationViewModel : ViewModelBase
    {
        private const int MaxWords = 10000;
        private const int MaxWordLength = 256;

        private readonly ModernDictationService _dictationService;
        private readonly SettingsService _settingsService;
        private string _currentWord = string.Empty;
        private string _currentTranslation = string.Empty;
        private int _countdown;
        private bool _isPaused;
        private bool _isTesting;
        private int _currentIndex;
        private int _totalWords;
        private string _countdownText = string.Empty;
        private bool _isSpeaking;
        private List<string> _wordList = new();
        private bool _showWordList = true;
        private bool _isFullScreen;
        private bool _isSidebarVisible = true;
        private bool _isTestCompleted;
        private int _readInterval = 5;
        private bool _isRandomOrder;
        private string _englishVoice = string.Empty;
        private string _chineseVoice = string.Empty;
        private List<Models.VoiceInfo> _englishVoices = new();
        private List<Models.VoiceInfo> _chineseVoices = new();

        public event Action<bool>? SidebarVisibilityChanged;
        public event Action? NavigateToHome;

        public ModernDictationViewModel()
        {
            _dictationService = new ModernDictationService();
            _settingsService = App.SharedViewModel?.Settings ?? new SettingsService();

            _dictationService.WordChanged += OnWordChanged;
            _dictationService.CountdownChanged += OnCountdownChanged;
            _dictationService.TestStateChanged += OnTestStateChanged;
            _dictationService.SpeechStatusChanged += OnSpeechStatusChanged;
            _dictationService.TestCompleted += OnTestCompleted;

            StartTestCommand = new RelayCommand(StartTest, CanStartTest);
            StopTestCommand = new RelayCommand(StopTest, () => IsTesting);
            NextWordCommand = new RelayCommand(NextWord, () => IsTesting);
            PreviousWordCommand = new RelayCommand(PreviousWord, () => IsTesting);
            RepeatWordCommand = new RelayCommand(RepeatWord, () => IsTesting);
            PauseResumeCommand = new RelayCommand(PauseResume, () => IsTesting);
            ReturnToHomeCommand = new RelayCommand(ReturnToHome);
            ShowAnswersCommand = new RelayCommand(ShowAnswers, () => IsTestCompleted);

            _ = InitializeAsync();
            LoadWordsFromMainViewModel();
            LoadAvailableVoices();
        }

        public bool HasAudioDevice => _dictationService.HasAudioDevice;
        public string AudioDeviceStatus => _dictationService.AudioDeviceStatus;

        public string CurrentWord { get => _currentWord; set => SetProperty(ref _currentWord, value); }
        public string CurrentTranslation { get => _currentTranslation; set => SetProperty(ref _currentTranslation, value); }

        public int Countdown
        {
            get => _countdown;
            set
            {
                if (SetProperty(ref _countdown, value))
                    UpdateCountdownText();
            }
        }

        public string CountdownText { get => _countdownText; set => SetProperty(ref _countdownText, value); }
        public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }
        public bool IsTesting { get => _isTesting; set => SetProperty(ref _isTesting, value); }
        public int CurrentIndex { get => _currentIndex; set => SetProperty(ref _currentIndex, value); }
        public int TotalWords { get => _totalWords; set => SetProperty(ref _totalWords, value); }
        public bool IsSpeaking { get => _isSpeaking; set => SetProperty(ref _isSpeaking, value); }
        public bool IsLastWord { get; set; }

        public int ReadInterval
        {
            get => _readInterval;
            set
            {
                var normalized = Math.Clamp(value, 1, 60);
                if (SetProperty(ref _readInterval, normalized))
                {
                    _dictationService.SetReadInterval(normalized);
                    _settingsService.Settings.ReadInterval = normalized;
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

        public string EnglishVoice { get => _englishVoice; set => SetProperty(ref _englishVoice, value); }
        public string ChineseVoice { get => _chineseVoice; set => SetProperty(ref _chineseVoice, value); }
        public List<Models.VoiceInfo> EnglishVoices { get => _englishVoices; private set => SetProperty(ref _englishVoices, value); }
        public List<Models.VoiceInfo> ChineseVoices { get => _chineseVoices; private set => SetProperty(ref _chineseVoices, value); }

        public List<string> WordList
        {
            get => _wordList;
            set
            {
                var normalized = NormalizeWords(value);
                if (!SetProperty(ref _wordList, normalized))
                    return;

                _dictationService.SetWords(normalized);
                TotalWords = normalized.Count;
            }
        }

        public bool ShowWordList { get => _showWordList; set => SetProperty(ref _showWordList, value); }
        public bool IsFullScreen { get => _isFullScreen; set => SetProperty(ref _isFullScreen, value); }

        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set
            {
                if (SetProperty(ref _isSidebarVisible, value))
                    SidebarVisibilityChanged?.Invoke(value);
            }
        }

        public bool IsTestCompleted { get => _isTestCompleted; set => SetProperty(ref _isTestCompleted, value); }
        public Action ShowAnswersAction { get; set; } = null!;

        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand NextWordCommand { get; }
        public ICommand PreviousWordCommand { get; }
        public ICommand RepeatWordCommand { get; }
        public ICommand PauseResumeCommand { get; }
        public ICommand ReturnToHomeCommand { get; }
        public ICommand ShowAnswersCommand { get; }

        private void OnWordChanged(string word, string translation, int currentIndex, int totalWords, bool isLastWord)
        {
            CurrentWord = word;
            CurrentTranslation = translation;
            CurrentIndex = Math.Max(0, currentIndex - 1);
            TotalWords = Math.Min(totalWords, MaxWords);
            IsLastWord = isLastWord;
        }

        private void OnCountdownChanged(int countdown) => Countdown = countdown;

        private void OnTestStateChanged(bool isTesting, bool isPaused)
        {
            IsTesting = isTesting;
            IsPaused = isPaused;
            if (!isTesting)
                IsTestCompleted = false;
        }

        private void OnSpeechStatusChanged(bool isSpeaking) => IsSpeaking = isSpeaking;
        private void OnTestCompleted() { IsTestCompleted = true; IsTesting = false; }

        private bool CanStartTest() => WordList.Count > 0 && !IsTesting;

        private async void StartTest()
        {
            if (!CanStartTest())
                return;

            IsFullScreen = true;
            ShowWordList = false;
            IsSidebarVisible = false;

            _dictationService.SetWords(WordList);
            _dictationService.SetRandomOrder(IsRandomOrder);
            _dictationService.SetReadInterval(ReadInterval);
            _dictationService.SetVoice(EnglishVoice);
            _dictationService.SetChineseVoice(ChineseVoice);
            await _dictationService.StartTest(0);
        }

        private async void StopTest()
        {
            await _dictationService.StopTestAsync();
            IsFullScreen = false;
            ShowWordList = true;
            IsSidebarVisible = true;
        }

        private async void NextWord() => await _dictationService.NextWordAsync();
        private async void PreviousWord() => await _dictationService.PreviousWordAsync();
        private async void RepeatWord() => await _dictationService.RepeatWordAsync();
        private async void PauseResume() => await _dictationService.PauseResumeAsync();

        private void ReturnToHome()
        {
            IsSidebarVisible = true;
            NavigateToHome?.Invoke();
        }

        private void ShowAnswers() => ShowAnswersAction?.Invoke();

        private void UpdateCountdownText()
        {
            CountdownText = IsSpeaking
                ? "正在朗读..."
                : Countdown == -1
                    ? "准备朗读..."
                    : Countdown > 0
                        ? $"倒计时: {Countdown}秒"
                        : string.Empty;
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _settingsService.LoadSettingsAsync();
                ReadInterval = _settingsService.Settings.ReadInterval;
                IsRandomOrder = _settingsService.Settings.IsRandomOrder;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ModernDictationViewModel 初始化失败: {ex.Message}");
            }
        }

        public void LoadWordsFromText(string? text)
        {
            WordList = NormalizeWords((text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        private void LoadWordsFromMainViewModel()
        {
            var mainViewModel = App.SharedViewModel;
            if (mainViewModel?.CurrentWords != null)
                WordList = new List<string>(mainViewModel.CurrentWords);
        }

        private void LoadAvailableVoices()
        {
            var speechService = App.SharedViewModel?.SpeechService;
            if (speechService == null)
                return;

            var voices = speechService.GetWindowsTtsVoices();
            EnglishVoices = voices.Where(v => v.Culture?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true).ToList();
            ChineseVoices = voices.Where(v => v.Culture?.StartsWith("zh", StringComparison.OrdinalIgnoreCase) == true).ToList();

            if (EnglishVoices.Count > 0)
                EnglishVoice = EnglishVoices[0].DisplayName;
            if (ChineseVoices.Count > 0)
                ChineseVoice = ChineseVoices[0].DisplayName;
        }

        private static List<string> NormalizeWords(IEnumerable<string>? words)
        {
            if (words == null)
                return new List<string>();

            return words
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word.Trim())
                .Where(word => word.Length <= MaxWordLength)
                .Take(MaxWords)
                .ToList();
        }

        public void Dispose()
        {
            try
            {
                _dictationService.WordChanged -= OnWordChanged;
                _dictationService.CountdownChanged -= OnCountdownChanged;
                _dictationService.TestStateChanged -= OnTestStateChanged;
                _dictationService.SpeechStatusChanged -= OnSpeechStatusChanged;
                _dictationService.TestCompleted -= OnTestCompleted;
                _dictationService.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dispose失败: {ex.Message}");
            }
        }
    }
}