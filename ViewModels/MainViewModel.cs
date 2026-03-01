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
    public class TestResultViewModel : INotifyPropertyChanged
    {
        public TestResult Result { get; set; } = new();

        public string WordListName => Result.WordListName;
        public string TimestampFormatted => Result.Timestamp.ToString("yyyy-MM-dd HH:mm");
        public int CorrectCount => Result.CorrectCount;
        public int TotalWords => Result.TotalWords;
        public string AccuracyFormatted => $"{Result.Accuracy:F1}%";

        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning disable CS0067
#pragma warning restore CS0067
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly SpeechService _speechService;

        private string _currentPage = "Home";
        private bool _isDarkTheme;
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

        public string CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set => SetProperty(ref _isDarkTheme, value);
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
            set => SetProperty(ref _wordsText, value);
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

        private async Task InitializeAsync()
        {
            await _settingsService.LoadSettingsAsync();
            _isDarkTheme = _settingsService.Settings.IsDarkTheme;
            _readInterval = _settingsService.Settings.ReadInterval;

            await LoadWordListFilesAsync();
            await LoadTestHistoryAsync();

            LoadVoices();
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(ReadInterval));
        }

        private void LoadVoices()
        {
            AvailableVoices.Clear();
            foreach (var voice in _speechService.GetAvailableVoices())
            {
                AvailableVoices.Add(voice);
            }
        }

        private void Navigate(string? page)
        {
            if (string.IsNullOrEmpty(page)) return;
            CurrentPage = page;
        }

        private void ToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            _settingsService.Settings.IsDarkTheme = IsDarkTheme;
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

            ShowTestInterface();
            await PlayCurrentWordAsync();
            StartTimer();
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
            if (CurrentIndex < _currentWords.Count - 1)
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
            await _speechService.SpeakAsync(CurrentWord, _settingsService.Settings.SpeechEngine);
        }

        private void PauseResume()
        {
            IsPaused = !IsPaused;
            if (!IsPaused)
            {
                StartTimer();
            }
            else
            {
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
                    var totalWords = _currentWords.Count;
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
            if (CurrentIndex >= 0 && CurrentIndex < _currentWords.Count)
            {
                CurrentWord = _currentWords[CurrentIndex];
                Countdown = ReadInterval;
                _testCountdown = 0;
                await _speechService.SpeakAsync(CurrentWord, _settingsService.Settings.SpeechEngine);
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
        }

        public async Task LoadWordsAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            CurrentWordListName = fileName;
            var filePath = System.IO.Path.Combine(
                _settingsService.GetWordlistDirectory(),
                fileName);

            var words = await _settingsService.LoadWordsFromFileAsync(filePath);
            _currentWords = words;
            WordsText = string.Join(Environment.NewLine, words);

            OnPropertyChanged(nameof(CanStartTest));
            if (StartTestCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }

        private async Task LoadWordListFilesAsync()
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
            TestHistory = await _settingsService.LoadTestHistoryAsync();
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
