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
using Windows.UI;

namespace English_Listen_WinUI.ViewModels
{
    public class TestResultViewModel
    {
        public TestResult Result { get; set; } = new();

        public string WordListName => Result.WordListName;
        public string TimestampFormatted => Result.Timestamp.ToString("yyyy-MM-dd HH:mm");
        public int CorrectCount => Result.CorrectCount;
        public int TotalWords => Result.TotalWords;
        public string AccuracyFormatted => $"{Result.Accuracy:F1}%";
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly SpeechService _speechService;

        public SpeechService SpeechService => _speechService;

        private string _currentPage = "Home";
        private int _themeMode; // 0 = Light, 1 = Dark, 2 = System
        private string _welcomeMessage = "欢迎使用英语听写训练系统";
        private string _userStatus = "未登录";
        private string _currentWord = "";
        private int _countdown;
        private bool _isPaused;
        private bool _isTesting;
        private int _readInterval = 5;
        private int _currentIndex;
        private string _currentWordListName = "";
        private List<string> _currentWords = new();
        private List<TestResult> _testHistory = new();
        private string _wordsText = "";
        private bool _showAnswers;
        private int _dictationMode; // 0 = 纸笔听写, 1 = 在线听写
        private List<string> _userInputs = new();
        private bool _isRandomOrder;
        private bool _isOnlineDictationMode;
        private List<string> _originalWordsOrder = new();
        private ObservableCollection<UserData> _users = new();

        public ObservableCollection<UserData> Users
        {
            get => _users;
            set => SetProperty(ref _users, value);
        }

        public string CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public int ThemeMode
        {
            get => _themeMode;
            set => SetProperty(ref _themeMode, value);
        }

        public string WelcomeMessage
        {
            get => _welcomeMessage;
            set => SetProperty(ref _welcomeMessage, value);
        }

        public string UserStatus
        {
            get => _userStatus;
            set => SetProperty(ref _userStatus, value);
        }

        public string CurrentWord
        {
            get => _currentWord;
            set => SetProperty(ref _currentWord, value);
        }

        public int Countdown
        {
            get => _countdown;
            set => SetProperty(ref _countdown, value);
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

        public int ReadInterval
        {
            get => _readInterval;
            set
            {
                if (SetProperty(ref _readInterval, value))
                {
                    _settingsService.Settings.ReadInterval = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public int CurrentIndex
        {
            get => _currentIndex;
            set => SetProperty(ref _currentIndex, value);
        }

        public string CurrentWordListName
        {
            get => _currentWordListName;
            set => SetProperty(ref _currentWordListName, value);
        }

        public List<TestResult> TestHistory
        {
            get => _testHistory;
            set => SetProperty(ref _testHistory, value);
        }

        public string WordsText
        {
            get => _wordsText;
            set 
            { 
                if (SetProperty(ref _wordsText, value))
                {
                    // Update CurrentWords when WordsText changes
                    UpdateCurrentWordsFromText();
                }
            }
        }

        public bool ShowAnswers
        {
            get => _showAnswers;
            set => SetProperty(ref _showAnswers, value);
        }

        public int DictationMode
        {
            get => _dictationMode;
            set => SetProperty(ref _dictationMode, value);
        }

        public List<string> CurrentWords
        {
            get => _currentWords;
            set => SetProperty(ref _currentWords, value);
        }

        public List<string> UserInputs
        {
            get => _userInputs;
            set => SetProperty(ref _userInputs, value);
        }

        public bool IsRandomOrder
        {
            get => _isRandomOrder;
            set
            {
                if (SetProperty(ref _isRandomOrder, value))
                {
                    _settingsService.Settings.IsRandomOrder = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public bool IsOnlineDictationMode
        {
            get => _isOnlineDictationMode;
            set => SetProperty(ref _isOnlineDictationMode, value);
        }

        public List<string> OriginalWordsOrder
        {
            get => _originalWordsOrder;
            set => SetProperty(ref _originalWordsOrder, value);
        }

        public int WordsCount => _currentWords.Count;

        private void UpdateCurrentWordsFromText()
        {
            if (string.IsNullOrEmpty(WordsText))
            {
                CurrentWords = new List<string>();
            }
            else
            {
                var words = WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList();
                CurrentWords = words;
            }
            
            // Update command availability
            OnPropertyChanged(nameof(CanStartTest));
            if (StartTestCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }

        public Services.SettingsService Settings => _settingsService;

        public ObservableCollection<string> WordListFiles { get; } = new();
        public ObservableCollection<TestResultViewModel> TestHistoryViewModels { get; } = new();
        public ObservableCollection<string> AvailableVoices { get; } = new();

        private System.Timers.Timer? _testTimer;
        private int _testCountdown;

        public ICommand NavigateCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand StartTestCommand { get; }
        public ICommand StopTestCommand { get; }
        public ICommand NextWordCommand { get; }
        public ICommand PreviousWordCommand { get; }
        public ICommand RepeatWordCommand { get; }
        public ICommand PauseResumeCommand { get; }
        public ICommand SaveWordsCommand { get; }
        public ICommand LoadWordsCommand { get; }

        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _speechService = new SpeechService();

            NavigateCommand = new RelayCommand<string>(Navigate);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            StartTestCommand = new RelayCommand(StartTest, CanStartTest);
            StopTestCommand = new RelayCommand(StopTest, () => IsTesting);
            NextWordCommand = new RelayCommand(NextWord, () => IsTesting);
            PreviousWordCommand = new RelayCommand(PreviousWord, () => IsTesting);
            RepeatWordCommand = new RelayCommand(RepeatWord, () => IsTesting);
            PauseResumeCommand = new RelayCommand(PauseResume, () => IsTesting);
            SaveWordsCommand = new RelayCommand(async () => await SaveWordsAsync());
            LoadWordsCommand = new RelayCommand<string>(async (file) => await LoadWordsAsync(file));

            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            await _settingsService.LoadSettingsAsync();
            _themeMode = _settingsService.Settings.ThemeMode;
            _readInterval = _settingsService.Settings.ReadInterval;
            _isRandomOrder = _settingsService.Settings.IsRandomOrder;

            await LoadWordListFilesAsync();
            await LoadTestHistoryAsync();
            
            // 加载临时词库，确保大文本框和听写功能都能访问
            await LoadWordsFromTempFileAsync();

            // Load users without password (will only load unencrypted users)
            var users = await _settingsService.LoadUsersAsync();
            await LoadUsersFromListAsync(users);

            LoadVoices();
            OnPropertyChanged(nameof(ThemeMode));
            OnPropertyChanged(nameof(ReadInterval));
            OnPropertyChanged(nameof(IsRandomOrder));
        }

        public async Task LoadUsersFromListAsync(List<UserData> users)
        {
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }
            
            // Update UI based on loaded users
            if (Users.Count > 0)
            {
                UserStatus = $"已加载 {Users.Count} 个用户";
            }
            else
            {
                UserStatus = "未发现用户或用户数据已加密";
            }
        }

        private void LoadVoices()
        {
            // SAPI voices removed - only Flite voice models available
            AvailableVoices.Clear();
            // Flite voice models are predefined in the UI
        }

        private void Navigate(string? page)
        {
            if (string.IsNullOrEmpty(page)) return;
            CurrentPage = page;
        }

        private void ToggleTheme()
        {
            // Cycle through theme modes: 0 (Light) -> 1 (Dark) -> 2 (System)
            ThemeMode = (ThemeMode + 1) % 3;
            _settingsService.Settings.ThemeMode = ThemeMode;
            _ = _settingsService.SaveSettingsAsync();
        }

        private bool CanStartTest()
        {
            return _currentWords.Count > 0 && !IsTesting;
        }

        private async void StartTest()
        {
            if (_currentWords.Count == 0) return;

            IsTesting = true;
            IsPaused = false;
            CurrentIndex = 0;
            _testCountdown = 0;

            // Handle random order
            if (IsRandomOrder)
            {
                var random = new Random();
                CurrentWords = _currentWords.OrderBy(x => random.Next()).ToList();
            }

            // Initialize user inputs for online mode
            if (DictationMode == 1)
            {
                UserInputs = new List<string>(new string[_currentWords.Count]);
            }

            ShowTestInterface();
            await PlayCurrentWordAsync();
            
            // Start timer only for paper dictation mode
            if (DictationMode == 0)
            {
                StartTimer();
            }
        }

        private void StopTest()
        {
            StopTimer();
            _speechService.Stop();
            IsTesting = false;
            IsPaused = false;
            CurrentPage = "Home";
        }

        private async void NextWord()
        {
            if (CurrentIndex < CurrentWords.Count - 1)
            {
                CurrentIndex++;
                await PlayCurrentWordAsync();
            }
        }

        private async void PreviousWord()
        {
            if (CurrentIndex > 0)
            {
                CurrentIndex--;
                await PlayCurrentWordAsync();
            }
        }

        private async void RepeatWord()
        {
            await _speechService.SpeakAsync(CurrentWord, _settingsService.Settings.FliteVoiceModel);
        }

        private void PauseResume()
        {
            IsPaused = !IsPaused;
            if (!IsPaused)
            {
                // 恢复音频播放和计时器
                _speechService.Resume();
                StartTimer();
            }
            else
            {
                // 暂停音频播放和计时器
                _speechService.Pause();
                StopTimer();
            }
        }

        private async void StartTimer()
        {
            StopTimer();
            _testTimer = new System.Timers.Timer(1000);
            _testTimer.Elapsed += async (s, e) =>
            {
                if (!IsPaused)
                {
                    _testCountdown++;
                    var countdown = ReadInterval - _testCountdown;
                    var currentIdx = CurrentIndex;
                    var totalWords = CurrentWords.Count;
                    var shouldStop = _testCountdown >= ReadInterval;

                    try
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                            Windows.UI.Core.CoreDispatcherPriority.Normal,
                            () =>
                            {
                                Countdown = countdown;
                                if (shouldStop)
                                {
                                    _testCountdown = 0;
                                    if (currentIdx < totalWords - 1)
                                    {
                                        NextWord();
                                    }
                                    else
                                    {
                                        StopTest();
                                    }
                                }
                            });
                    }
                    catch { }
                }
            };
            _testTimer.Start();
        }

        private void StopTimer()
        {
            _testTimer?.Stop();
            _testTimer?.Dispose();
            _testTimer = null;
        }

        private async Task PlayCurrentWordAsync()
        {
            if (CurrentIndex >= 0 && CurrentIndex < CurrentWords.Count)
            {
                CurrentWord = CurrentWords[CurrentIndex];
                Countdown = ReadInterval;
                _testCountdown = 0;
                await _speechService.SpeakAsync(CurrentWord, _settingsService.Settings.FliteVoiceModel);
            }
        }

        private void ShowTestInterface()
        {
            CurrentPage = "Test";
        }

        public async Task SaveWordsAsync()
        {
            if (string.IsNullOrEmpty(CurrentWordListName))
            {
                CurrentWordListName = "default.txt";
            }

            var filePath = System.IO.Path.Combine(
                _settingsService.GetWordlistDirectory(),
                CurrentWordListName);

            var words = WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            await _settingsService.SaveWordsToFileAsync(filePath, words);
            
            // Update CurrentWords
            CurrentWords = words;
            
            // 同时保存到临时文件用于听考
            await System.IO.File.WriteAllLinesAsync(TempWordListPath, words);
        }

        private static readonly string TempWordListPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");

        public async Task LoadWordsAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            CurrentWordListName = fileName;
            var filePath = System.IO.Path.Combine(
                _settingsService.GetWordlistDirectory(),
                fileName);

            var words = await _settingsService.LoadWordsFromFileAsync(filePath);
            CurrentWords = words;
            WordsText = string.Join(Environment.NewLine, words);
            
            // 保存到临时文件用于听考
            await System.IO.File.WriteAllLinesAsync(TempWordListPath, words);

            OnPropertyChanged(nameof(CanStartTest));
            if (StartTestCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }
        
        public async Task LoadWordsFromTempFileAsync()
        {
            // 如果临时文件不存在，创建新的空文件
            if (!System.IO.File.Exists(TempWordListPath))
            {
                await System.IO.File.WriteAllTextAsync(TempWordListPath, "");
            }
            
            var words = await _settingsService.LoadWordsFromFileAsync(TempWordListPath);
            CurrentWords = words;
            WordsText = string.Join(Environment.NewLine, words);
            
            CurrentWordListName = "临时词库";

            OnPropertyChanged(nameof(CanStartTest));
            if (StartTestCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }

        public async Task LoadWordListFilesAsync()
        {
            WordListFiles.Clear();
            var files = await _settingsService.GetWordlistFilesAsync();
            foreach (var file in files)
            {
                WordListFiles.Add(System.IO.Path.GetFileName(file));
            }

            if (WordListFiles.Count > 0)
            {
                await LoadWordsAsync(WordListFiles[0]);
            }
        }

        private async Task LoadTestHistoryAsync()
        {
            var currentUser = _settingsService.Settings.CurrentUser;
            TestHistory = await _settingsService.LoadTestHistoryAsync(currentUser ?? "");
            TestHistoryViewModels.Clear();
            foreach (var result in TestHistory.OrderByDescending(h => h.Timestamp))
            {
                TestHistoryViewModels.Add(new TestResultViewModel { Result = result });
            }
        }

        public async Task ImportWordsFromFileAsync(string sourcePath)
        {
            var fileName = System.IO.Path.GetFileName(sourcePath);
            var destPath = System.IO.Path.Combine(
                _settingsService.GetWordlistDirectory(),
                fileName);

            System.IO.File.Copy(sourcePath, destPath, true);
            await LoadWordListFilesAsync();
        }

        public void Cleanup()
        {
            StopTimer();
            _speechService.Dispose();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter) => _execute();
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
        public void Execute(object? parameter) => _execute((T?)parameter);
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
