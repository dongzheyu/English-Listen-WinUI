using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;
using Microsoft.UI.Xaml;

namespace English_Listen_WinUI.ViewModels
{
    public class TestResultViewModel
    {
        public TestResult Result { get; set; } = new();

        public string WordListName => Result.WordListName;
        public string TimestampFormatted => Result.Timestamp.ToString("yyyy-MM-dd HH:mm");
        public int CorrectCount => Result.CorrectCount;
        public int TotalWords => Result.TotalWords;
        public bool IsLearningRecord => Result.RecordType == "learning";
        public string AccuracyFormatted => IsLearningRecord ? "" : $"{Result.Accuracy:F1}%";
        public Visibility AccuracyVisibility => IsLearningRecord ? Visibility.Collapsed : Visibility.Visible;
        public bool CanRedictate => Result.Words is { Count: > 0 };
        public bool CanRelearn => IsLearningRecord && Result.Words is { Count: > 0 };
        public Visibility RedictateVisibility => CanRedictate ? Visibility.Visible : Visibility.Collapsed;
        public Visibility RelearnVisibility => CanRelearn ? Visibility.Visible : Visibility.Collapsed;
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService = new();
        private readonly SpeechService _speechService = new();

        private string _currentPage = "Home";
        private string _currentWordListName = "";
        private List<string> _currentWords = new();
        private bool _isRandomOrder;
        private int _readInterval = 5;

        private StudyPlanSettings _studyPlan = new();
        private List<TestResult> _testHistory = new();
        private int _themeMode; // 0 = Light, 1 = Dark, 2 = System
        private string _userStatus = "未登录";
        private ObservableCollection<UserData> _users = new();
        private string _welcomeMessage = "欢迎使用英语听写训练系统";
        private string _wordsText = "";

        public MainViewModel()
        {
            Debug.WriteLine("[MainViewModel] Constructor started");

            try
            {
                Debug.WriteLine("[MainViewModel] Services already initialized inline");

                NavigateCommand = new RelayCommand<string>(Navigate);
                ToggleThemeCommand = new RelayCommand(ToggleTheme);
                SaveWordsCommand = new RelayCommand(async () =>
                {
                    try
                    {
                        await SaveWordsAsync();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"SaveWordsAsync 异常: {ex.Message}");
                    }
                });
                LoadWordsCommand = new RelayCommand<string>(async (file) =>
                {
                    try
                    {
                        await LoadWordsAsync(file);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LoadWordsAsync 异常: {ex.Message}");
                    }
                });

                Debug.WriteLine("[MainViewModel] Commands initialized");

                _ = InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Constructor error: {ex.Message}");
                Debug.WriteLine($"[MainViewModel] Stack trace: {ex.StackTrace}");
            }

            Debug.WriteLine("[MainViewModel] Constructor completed");
        }

        public SpeechService SpeechService => _speechService;

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

        public string CurrentWordListName
        {
            get => _currentWordListName;
            set => SetProperty(ref _currentWordListName, value);
        }

        public List<string> CurrentWords
        {
            get => _currentWords;
            private set => SetProperty(ref _currentWords, value);
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
                    UpdateCurrentWordsFromText();
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
                    _settingsService.Settings.IsRandomOrder = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public StudyPlanSettings StudyPlan
        {
            get => _studyPlan;
            set
            {
                if (SetProperty(ref _studyPlan, value))
                {
                    _settingsService.Settings.StudyPlan = value;
                    _ = _settingsService.SaveSettingsAsync();
                }
            }
        }

        public int WordsCount => _currentWords.Count;

        public SettingsService Settings => _settingsService;

        public ObservableCollection<string> WordListFiles { get; } = new();
        public ObservableCollection<TestResultViewModel> TestHistoryViewModels { get; } = new();
        public ObservableCollection<string> AvailableVoices { get; } = new();

        public ICommand NavigateCommand { get; } = null!;
        public ICommand ToggleThemeCommand { get; } = null!;
        public ICommand SaveWordsCommand { get; } = null!;
        public ICommand LoadWordsCommand { get; } = null!;

        public bool CanStartTest => CurrentWords.Count > 0;

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

            OnPropertyChanged(nameof(CanStartTest));
            if (SaveWordsCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }

        private void Navigate(string? page)
        {
            if (string.IsNullOrEmpty(page)) return;
            CurrentPage = page;
        }

        private void ToggleTheme()
        {
            ThemeMode = (ThemeMode + 1) % 3;
            _settingsService.Settings.ThemeMode = ThemeMode;
            _ = _settingsService.SaveSettingsAsync();
        }

        public async Task InitializeAsync()
        {
            try
            {
                Debug.WriteLine("[MainViewModel] InitializeAsync started");

                await _settingsService.LoadSettingsAsync();
                _themeMode = _settingsService.Settings.ThemeMode;
                _readInterval = _settingsService.Settings.ReadInterval;
                _isRandomOrder = _settingsService.Settings.IsRandomOrder;
                _studyPlan = _settingsService.Settings.StudyPlan ?? new StudyPlanSettings();

                await LoadWordListFilesAsync();
                await LoadTestHistoryAsync();

                await LoadWordsFromTempFileAsync();

                var users = await _settingsService.LoadUsersAsync();
                await LoadUsersFromListAsync(users);

                LoadVoices();
                OnPropertyChanged(nameof(ThemeMode));
                OnPropertyChanged(nameof(ReadInterval));
                OnPropertyChanged(nameof(IsRandomOrder));
                OnPropertyChanged(nameof(StudyPlan));

                Debug.WriteLine("[MainViewModel] InitializeAsync completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] InitializeAsync error: {ex.Message}");
                Debug.WriteLine($"[MainViewModel] Stack trace: {ex.StackTrace}");
            }
        }

        public async Task LoadUsersFromListAsync(List<UserData> users)
        {
            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(user);
            }

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
            AvailableVoices.Clear();
            try
            {
                var voices = _speechService.GetWindowsTtsVoices();
                foreach (var voice in voices)
                {
                    if (!string.IsNullOrEmpty(voice.Name))
                    {
                        AvailableVoices.Add(voice.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载语音列表失败: {ex.Message}");
            }
        }

        public async Task SaveWordsAsync()
        {
            if (string.IsNullOrEmpty(CurrentWordListName))
            {
                CurrentWordListName = "default.txt";
            }

            var filePath = Path.Combine(
                _settingsService.GetWordlistDirectory(),
                CurrentWordListName);

            var words = WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();

            await _settingsService.SaveWordsToFileAsync(filePath, words);

            CurrentWords = words;

            await TempFileHelper.WriteWordsAsync(words);
        }

        public async Task LoadWordsAsync(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;

            CurrentWordListName = fileName;
            var filePath = Path.Combine(
                _settingsService.GetWordlistDirectory(),
                fileName);

            var words = await _settingsService.LoadWordsFromFileAsync(filePath);
            CurrentWords = words;
            WordsText = string.Join(Environment.NewLine, words);

            await TempFileHelper.WriteWordsAsync(words);

            OnPropertyChanged(nameof(CanStartTest));
            if (SaveWordsCommand is RelayCommand rc)
            {
                rc.RaiseCanExecuteChanged();
            }
        }

        public async Task LoadWordsFromTempFileAsync()
        {
            var words = await TempFileHelper.ReadWordsAsync();
            CurrentWords = words;
            WordsText = string.Join(Environment.NewLine, words);

            CurrentWordListName = "临时词库";

            OnPropertyChanged(nameof(CanStartTest));
            if (SaveWordsCommand is RelayCommand rc)
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
                WordListFiles.Add(Path.GetFileName(file));
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
            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(
                _settingsService.GetWordlistDirectory(),
                fileName);

            File.Copy(sourcePath, destPath, true);
            await LoadWordListFilesAsync();
        }

        /// <summary>
        /// 保存学习计划
        /// </summary>
        public async Task SaveStudyPlanAsync()
        {
            _settingsService.Settings.StudyPlan = _studyPlan;
            await _settingsService.SaveSettingsAsync();
        }

        public void Cleanup()
        {
            try
            {
                _speechService?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup失败: {ex.Message}");
            }
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Func<bool>? _canExecute;
        private readonly Action _execute;

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
        private readonly Func<T?, bool>? _canExecute;
        private readonly Action<T?> _execute;

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