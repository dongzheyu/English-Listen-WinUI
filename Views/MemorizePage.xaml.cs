using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;
using English_Listen_WinUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace English_Listen_WinUI.Views
{
    public class MemorizeWordItem : ViewModelBase
    {
        private bool _isSelected = false;
        private string _learnedStatus = "";
        private string _translation = string.Empty;
        private string _word = string.Empty;

        public string Word
        {
            get => _word;
            set => SetProperty(ref _word, value);
        }

        public string Translation
        {
            get => _translation;
            set => SetProperty(ref _translation, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string LearnedStatus
        {
            get => _learnedStatus;
            set => SetProperty(ref _learnedStatus, value);
        }
    }

    public sealed partial class MemorizePage : Page
    {
        private static readonly List<DailyQuote> _quotes = new()
        {
            new("Persistence is the hard work you do after you get tired of doing the hard work you already did.",
                "Newt Gingrich", "坚持就是在你厌倦了已经付出的辛苦之后，仍然继续努力。"),
            new("It does not matter how slowly you go as long as you do not stop.", "Confucius",
                "不在乎你走得多慢，只要你不停下来。"),
            new("The secret of getting ahead is getting started.", "Mark Twain",
                "领先的秘诀就是开始行动。"),
            new("Success is the sum of small efforts, repeated day in and day out.", "Robert Collier",
                "成功是日复一日的小努力的积累。"),
            new("A journey of a thousand miles begins with a single step.", "Lao Tzu",
                "千里之行，始于足下。"),
            new("The difference between ordinary and extraordinary is that little extra.",
                "Jimmy Johnson", "平凡与非凡之间的区别就在于那一点点额外的付出。"),
            new("Don't watch the clock; do what it does. Keep going.", "Sam Levenson",
                "不要盯着时钟看，要像它一样不停地走。"),
            new("Learning is not attained by chance, it must be sought for with ardor and attended to with diligence.",
                "Abigail Adams", "学问不是靠偶然获得的，必须以热情去追求，以勤奋去维护。"),
            new(
                "The more that you read, the more things you will know. The more that you learn, the more places you'll go.",
                "Dr. Seuss", "你读得越多，知道的就越多。你学得越多，能去的地方就越多。"),
            new(
                "Knowledge is power. Information is liberating. Education is the premise of progress, in every society, in every family.",
                "Kofi Annan", "知识就是力量。信息带来解放。教育是每个社会、每个家庭进步的前提。"),
            new("Live as if you were to die tomorrow. Learn as if you were to live forever.",
                "Mahatma Gandhi", "像明天就会死去一样生活，像永远会活着一样学习。"),
            new("Education is the passport to the future, for tomorrow belongs to those who prepare for it today.",
                "Malcolm X", "教育是通向未来的通行证，因为明天属于今天为之做准备的人。"),
            new("The beautiful thing about learning is that no one can take it away from you.",
                "B.B. King", "学习的美好之处在于，没有人能把它从你身上夺走。"),
            new("An investment in knowledge pays the best interest.", "Benjamin Franklin",
                "对知识的投资回报率最高。"),
            new("Your attitude, not your aptitude, will determine your altitude.", "Zig Ziglar",
                "决定你高度的不是能力，而是你的态度。"),
            new("There are no shortcuts to any place worth going.", "Beverly Sills",
                "任何值得去的地方都没有捷径。"),
            new("Success is not final, failure is not fatal: it is the courage to continue that counts.",
                "Winston Churchill", "成功不是终点，失败也不是致命的，重要的是继续前进的勇气。"),
            new("The only way to do great work is to love what you do.", "Steve Jobs",
                "做出伟大工作的唯一方法就是热爱你所做的事情。"),
            new("Believe you can and you're halfway there.", "Theodore Roosevelt",
                "相信你能做到，你就已经成功了一半。"),
            new("I have not failed. I've just found 10,000 ways that won't work.", "Thomas Edison",
                "我没有失败，我只是发现了一万种行不通的方法。"),
        };

        private readonly HashSet<string> _readWords = new();
        private readonly TranslationLibraryService _translationLibrary;
        private readonly MainViewModel _viewModel;
        private DispatcherTimer? _autoSaveTimer;
        private int _currentIndex;
        private bool _isInitializing = false;
        private List<MemorizeWordItem> _originalWordList = new();
        private List<DictationTestPage.WordTranslationPair> _studyWords = new();
        private SpeechSynthesizer? _synthesizer;
        private int _totalWords;
        private List<MemorizeWordItem> _wordList = new();

        public MemorizePage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            _translationLibrary = new TranslationLibraryService();
            InitializeSynthesizer();
            Loaded += MemorizePage_Loaded;

            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }

        private void InitializeSynthesizer()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"语音初始化失败: {ex.Message}");
                _synthesizer = null;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is List<DictationTestPage.WordTranslationPair> wordListParam)
            {
                // 从外部传入单词列表，直接进入背单词模式
                _studyWords = wordListParam;
                _totalWords = _studyWords.Count;
                _currentIndex = 0;
                ConfigPanel.Visibility = Visibility.Collapsed;
                StudyPanel.Visibility = Visibility.Visible;
                (App.MainWindow as MainWindow)?.SetSidebarVisibility(false);
                ShowCurrentWord();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            if (_synthesizer != null)
            {
                try
                {
                    _synthesizer.SpeakAsyncCancelAll();
                }
                catch
                {
                }

                try
                {
                    _synthesizer.Dispose();
                }
                catch
                {
                }

                _synthesizer = null;
            }

            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Tick -= AutoSaveTimer_Tick;
                _autoSaveTimer = null;
            }

            _ = SaveCurrentState();
        }

        private async Task SaveCurrentState()
        {
            try
            {
                var selected = _wordList.Where(w => w.IsSelected).Select(w => w.Word).ToList();
                _viewModel.StudyPlan.SelectedWords = selected;

                // 合并本次朗读过的单词到 LearnedWords
                var learned = new HashSet<string>(_viewModel.StudyPlan.LearnedWords ?? new());
                learned.UnionWith(_readWords);
                _viewModel.StudyPlan.LearnedWords = learned.ToList();

                await _viewModel.SaveStudyPlanAsync();
                UpdateWordsListBox();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存状态失败: {ex.Message}");
            }
        }

        private async void MemorizePage_Loaded(object sender, RoutedEventArgs e)
        {
            _isInitializing = true;
            try
            {
                PopulateWordBookList();

                var plan = _viewModel.StudyPlan;
                DailyWordCountBox.Value = Math.Clamp(plan.DailyWordCount, 5, 200);
                RandomOrderToggle.IsOn = plan.RandomOrder;

                // 向后兼容：旧数据只有 SelectedWordList（单个文件）
                if (plan.SelectedWordLists.Count == 0 && !string.IsNullOrEmpty(plan.SelectedWordList))
                {
                    plan.SelectedWordLists.Add(plan.SelectedWordList);
                }

                // 如果词书已锁定，禁用列表并显示更改按钮
                if (plan.IsWordListLocked && plan.SelectedWordLists.Count > 0)
                {
                    WordBookItemsControl.IsEnabled = false;
                    LoadWordlistButton.Visibility = Visibility.Collapsed;
                    ChangeWordListButton.Visibility = Visibility.Visible;

                    foreach (var file in plan.SelectedWordLists)
                    {
                        SetWordBookChecked(file, true);
                    }
                }

                if (plan.SelectedWordLists.Count > 0)
                {
                    await LoadWordlistAsync(plan.SelectedWordLists);

                    // 恢复选中状态
                    if (plan.SelectedWords?.Count > 0)
                    {
                        foreach (var item in _wordList)
                        {
                            item.IsSelected = plan.SelectedWords.Contains(item.Word);
                        }
                    }

                    UpdateWordsListBox();
                }

                ShowRandomQuote();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void PopulateWordBookList()
        {
            WordBookItemsControl.Items.Clear();
            foreach (var file in _viewModel.WordListFiles)
            {
                WordBookItemsControl.Items.Add(file);
            }
        }

        /// <summary>
        /// 加载词库文件，支持多文件合并，与 WordsPage 分组加载逻辑一致
        /// </summary>
        private async Task LoadWordlistAsync(IEnumerable<string> fileNames)
        {
            var wordDir = _viewModel.Settings.GetWordlistDirectory();
            var wordItems = await Task.Run(() =>
            {
                var allWords = new List<string>();
                foreach (var fileName in fileNames)
                {
                    var filePath = Path.Combine(wordDir, fileName);
                    if (!File.Exists(filePath)) continue;

                    var lines = File.ReadAllLines(filePath);
                    allWords.AddRange(lines
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .Select(line => line.Trim())
                        .Where(w => !string.IsNullOrEmpty(w)));
                }

                return allWords
                    .Select(w => new MemorizeWordItem
                    {
                        Word = w,
                        Translation = _translationLibrary.GetTranslation(w) ?? ""
                    })
                    .ToList();
            });

            _wordList = wordItems;
            _originalWordList = new List<MemorizeWordItem>(wordItems);
            UpdateWordsListBox();
        }

        private void UpdateWordsListBox()
        {
            var learnedWords = new HashSet<string>(_viewModel.StudyPlan.LearnedWords ?? new());
            foreach (var item in _wordList)
            {
                item.LearnedStatus = learnedWords.Contains(item.Word) ? "已学习" : "";
            }

            WordsListBox.ItemsSource = null;
            WordsListBox.ItemsSource = _wordList;
            WordsListBox.UpdateLayout();
            WordCountTextBlock.Text = $"单词数量: {_wordList.Count}";
        }

        private void ShowRandomQuote()
        {
            var rng = new Random();
            var quote = _quotes[rng.Next(_quotes.Count)];
            QuoteTextBlock.Text = $"\"{quote.Text}\"";
            QuoteTranslationBlock.Text = quote.Translation;
            QuoteAuthorBlock.Text = $"- {quote.Author}";
        }

        // ========== 词库加载和选择（与 WordsPage 相同逻辑）==========

        private async void LoadWordlistButton_Click(object sender, RoutedEventArgs e)
        {
            var checkedBooks = GetCheckedWordBooks();
            if (checkedBooks.Count == 0)
            {
                MainWindow.ShowNotification("请先选择词库");
                return;
            }

            await LoadWordlistAsync(checkedBooks);
            _viewModel.StudyPlan.SelectedWordLists = new List<string>(checkedBooks);
            _viewModel.StudyPlan.SelectedWordList = checkedBooks[0];
            _viewModel.StudyPlan.IsWordListLocked = true;
            await _viewModel.SaveStudyPlanAsync();

            WordBookItemsControl.IsEnabled = false;
            LoadWordlistButton.Visibility = Visibility.Collapsed;
            ChangeWordListButton.Visibility = Visibility.Visible;
        }

        private async void ChangeWordListButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "更改词书",
                Content = "更改词书将丢失当前背诵进度，确定要更改吗？",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 解锁词书选择
                _viewModel.StudyPlan.IsWordListLocked = false;
                _viewModel.StudyPlan.SelectedWordLists.Clear();
                await _viewModel.SaveStudyPlanAsync();

                WordBookItemsControl.IsEnabled = true;
                LoadWordlistButton.Visibility = Visibility.Visible;
                ChangeWordListButton.Visibility = Visibility.Collapsed;
            }
        }

        private List<string> GetCheckedWordBooks()
        {
            var checkedBooks = new List<string>();
            if (WordBookItemsControl?.Items == null) return checkedBooks;
            foreach (var item in WordBookItemsControl.Items)
            {
                var container = WordBookItemsControl.ContainerFromItem(item) as FrameworkElement;
                if (container == null) continue;
                var checkBox = FindCheckBox(container);
                if (checkBox?.IsChecked == true)
                {
                    checkedBooks.Add(item?.ToString() ?? "");
                }
            }

            return checkedBooks;
        }

        private CheckBox? FindCheckBox(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is CheckBox cb) return cb;
                var result = FindCheckBox(child);
                if (result != null) return result;
            }

            return null;
        }

        private void SetWordBookChecked(string fileName, bool isChecked)
        {
            if (WordBookItemsControl?.Items == null) return;
            foreach (var item in WordBookItemsControl.Items)
            {
                if (item?.ToString() == fileName)
                {
                    var container = WordBookItemsControl.ContainerFromItem(item) as FrameworkElement;
                    if (container == null) return;
                    var checkBox = FindCheckBox(container);
                    if (checkBox != null) checkBox.IsChecked = isChecked;
                    return;
                }
            }
        }

        private void WordBookCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;
        }

        private void SelectAllBooksButton_Click(object sender, RoutedEventArgs e)
        {
            if (WordBookItemsControl?.Items == null) return;
            foreach (var item in WordBookItemsControl.Items)
            {
                var container = WordBookItemsControl.ContainerFromItem(item) as FrameworkElement;
                if (container == null) continue;
                var checkBox = FindCheckBox(container);
                if (checkBox != null) checkBox.IsChecked = true;
            }
        }

        private void InvertBooksButton_Click(object sender, RoutedEventArgs e)
        {
            if (WordBookItemsControl?.Items == null) return;
            foreach (var item in WordBookItemsControl.Items)
            {
                var container = WordBookItemsControl.ContainerFromItem(item) as FrameworkElement;
                if (container == null) continue;
                var checkBox = FindCheckBox(container);
                if (checkBox != null) checkBox.IsChecked = !(checkBox.IsChecked ?? false);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        /// <summary>
        /// 搜索过滤逻辑，与 WordsPage 保持一致
        /// </summary>
        private void ApplyFilter()
        {
            var searchText = SearchTextBox.Text;
            if (string.IsNullOrEmpty(searchText))
            {
                _wordList = _originalWordList
                    .Select(w => new MemorizeWordItem
                    {
                        Word = w.Word,
                        Translation = w.Translation,
                        IsSelected = w.IsSelected
                    })
                    .ToList();
            }
            else
            {
                _wordList = _originalWordList
                    .Where(w => w.Word.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    .Select(w => new MemorizeWordItem
                    {
                        Word = w.Word,
                        Translation = w.Translation,
                        IsSelected = w.IsSelected
                    })
                    .ToList();
            }

            UpdateWordsListBox();
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var selectAll = SelectAllCheckBox.IsChecked ?? false;
            foreach (var item in _wordList)
            {
                item.IsSelected = selectAll;
            }

            // 同步原始列表
            foreach (var original in _originalWordList)
            {
                var match = _wordList.FirstOrDefault(w => w.Word == original.Word);
                if (match != null)
                    original.IsSelected = match.IsSelected;
            }

            TriggerAutoSave();
        }

        // ========== 实时保存 ==========

        private async void DailyWordCountBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isInitializing || _viewModel == null) return;
            _viewModel.StudyPlan.DailyWordCount = (int)Math.Clamp(sender.Value, 5, 200);
            await _viewModel.SaveStudyPlanAsync();
        }

        private async void RandomOrderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewModel == null) return;
            _viewModel.StudyPlan.RandomOrder = RandomOrderToggle.IsOn;
            await _viewModel.SaveStudyPlanAsync();
        }

        private void TriggerAutoSave()
        {
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private async void AutoSaveTimer_Tick(object? sender, object e)
        {
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
            }

            await SaveCurrentState();
        }

        private async void ResetProgressButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StudyPlan.LearnedWords.Clear();
            _readWords.Clear();
            await _viewModel.SaveStudyPlanAsync();
            UpdateWordsListBox();
            MainWindow.ShowNotification("学习进度已重置");
        }

        private async void ClearSelectedLearnedButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _wordList.Where(w => w.IsSelected).Select(w => w.Word).ToHashSet();
            if (selected.Count == 0)
            {
                MainWindow.ShowNotification("请先勾选要清除学习状态的单词");
                return;
            }

            var learned = new HashSet<string>(_viewModel.StudyPlan.LearnedWords ?? new());
            learned.ExceptWith(selected);
            _viewModel.StudyPlan.LearnedWords = learned.ToList();
            _readWords.ExceptWith(selected);
            await _viewModel.SaveStudyPlanAsync();
            UpdateWordsListBox();
            MainWindow.ShowNotification($"已清除 {selected.Count} 个单词的学习状态");
        }

        // ========== 开始背单词 ==========

        private async void StartMemorizeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedWords = _wordList.Where(w => w.IsSelected).ToList();
            if (selectedWords.Count == 0)
            {
                selectedWords = _originalWordList.Where(w => w.IsSelected).ToList();
            }

            if (selectedWords.Count == 0)
            {
                MainWindow.ShowNotification("请先选择要背诵的单词");
                return;
            }

            // 过滤掉已学习的单词
            var learnedWords = new HashSet<string>(_viewModel.StudyPlan.LearnedWords ?? new());
            selectedWords = selectedWords.Where(w => !learnedWords.Contains(w.Word)).ToList();
            if (selectedWords.Count == 0)
            {
                MainWindow.ShowNotification("所选单词已全部学习完毕");
                return;
            }

            // 限制每日数量
            int dailyCount = (int)Math.Clamp(DailyWordCountBox.Value, 5, 200);
            var studyList = selectedWords.Take(dailyCount).ToList();

            if (RandomOrderToggle.IsOn)
            {
                var rng = new Random();
                studyList = studyList.OrderBy(_ => rng.Next()).ToList();
            }

            _studyWords = studyList.Select(w => new DictationTestPage.WordTranslationPair
            {
                Word = w.Word,
                Translation = w.Translation
            }).ToList();

            _totalWords = _studyWords.Count;
            _currentIndex = 0;

            // 更新学习统计（同一天不重复增加天数）
            var today = DateTime.Now.Date;
            if (_viewModel.StudyPlan.LastStudyDate.Date != today)
            {
                _viewModel.StudyPlan.CompletedDays++;
            }

            _viewModel.StudyPlan.LastStudyDate = DateTime.Now;
            _viewModel.StudyPlan.TotalLearnedWords += _totalWords;
            await _viewModel.SaveStudyPlanAsync();

            // 切换面板
            ConfigPanel.Visibility = Visibility.Collapsed;
            StudyPanel.Visibility = Visibility.Visible;
            (App.MainWindow as MainWindow)?.SetSidebarVisibility(false);
            ShowCurrentWord();
        }

        private void BackToConfigButton_Click(object sender, RoutedEventArgs e)
        {
            StudyPanel.Visibility = Visibility.Collapsed;
            ConfigPanel.Visibility = Visibility.Visible;
            (App.MainWindow as MainWindow)?.SetSidebarVisibility(true);
        }

        // ========== 背单词逻辑（保留原有功能）==========

        private void ShowCurrentWord()
        {
            if (_studyWords.Count == 0) return;

            var pair = _studyWords[_currentIndex];
            WordText.Text = pair.Word;
            TranslationText.Text = pair.Translation;
            ProgressText.Text = $"第 {_currentIndex + 1} / {_totalWords} 个";

            PreviousButton.IsEnabled = _currentIndex > 0;
            NextButton.IsEnabled = _currentIndex < _totalWords - 1;

            _readWords.Add(pair.Word);
            SpeakCurrentWord();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex > 0)
            {
                _currentIndex--;
                ShowCurrentWord();
                await SaveCurrentState();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentIndex < _totalWords - 1)
            {
                _currentIndex++;
                ShowCurrentWord();
                await SaveCurrentState();
            }
        }

        private void EndButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCompletionDialog();
        }

        private async void ShowCompletionDialog()
        {
            if (_studyWords.Count == 0)
            {
                StudyPanel.Visibility = Visibility.Collapsed;
                ConfigPanel.Visibility = Visibility.Visible;
                (App.MainWindow as MainWindow)?.SetSidebarVisibility(true);
                return;
            }

            // ponytail: 学习完成后写入 TestHistory，保存单词列表以便后续复习听考
            await SaveStudyRecordAsync();

            var dialog = new ContentDialog
            {
                Title = "学习完成",
                PrimaryButtonText = "去听考",
                CloseButtonText = "返回",
                XamlRoot = this.XamlRoot
            };

            var sp = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
            sp.Children.Add(new TextBlock
            {
                Text = $"本次学习了 {_studyWords.Count} 个单词，是否进行听写测试？",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 16
            });
            dialog.Content = sp;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ShowDictationSettingsAndNavigateAsync();
            }
            else
            {
                StudyPanel.Visibility = Visibility.Collapsed;
                ConfigPanel.Visibility = Visibility.Visible;
                (App.MainWindow as MainWindow)?.SetSidebarVisibility(true);
            }
        }

        // ponytail: 学习完成写入一条 TestHistory 记录，Words 字段供后续复习选择
        private async Task SaveStudyRecordAsync()
        {
            try
            {
                var settingsService = _viewModel?.Settings;
                if (settingsService == null)
                {
                    MainWindow.ShowNotification("保存学习记录失败: settingsService 为空");
                    return;
                }

                await settingsService.LoadSettingsAsync();

                var currentUser = settingsService.Settings.CurrentUser ?? "";

                var testHistory = await settingsService.LoadTestHistoryAsync(currentUser);
                if (_studyWords.Count == 0)
                {
                    MainWindow.ShowNotification("保存学习记录失败: 没有学习单词");
                    return;
                }

                var record = new TestResult
                {
                    Timestamp = DateTime.Now,
                    TotalWords = _studyWords.Count,
                    CorrectCount = _studyWords.Count,
                    Accuracy = 100.0,
                    WordListName = string.Join(", ", _viewModel!.StudyPlan.SelectedWordLists ?? new()),
                    Words = _studyWords.Select(w => new WordTranslationPair
                        { Word = w.Word, Translation = w.Translation }).ToList(),
                    RecordType = "learning"
                };
                testHistory.Add(record);
                await settingsService.SaveTestHistoryAsync(currentUser, testHistory);

                // 同步到 ViewModel
                _viewModel.TestHistory = testHistory;
                _viewModel.TestHistoryViewModels.Clear();
                foreach (var r in testHistory)
                    _viewModel.TestHistoryViewModels.Add(new TestResultViewModel { Result = r });

                // 标记朗读过的单词为已学习
                var learned = new HashSet<string>(_viewModel.StudyPlan.LearnedWords ?? new());
                learned.UnionWith(_readWords);
                _viewModel.StudyPlan.LearnedWords = learned.ToList();
                await _viewModel.SaveStudyPlanAsync();

                MainWindow.ShowNotification($"学习记录已保存 ({_studyWords.Count} 个单词)");
            }
            catch (Exception ex)
            {
                MainWindow.ShowNotification($"保存学习记录失败: {ex.Message}");
            }
        }

        private async Task ShowDictationSettingsAndNavigateAsync()
        {
            var dialog = new ContentDialog
            {
                Title = "听写测试选项",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 10 };

            var modeStackPanel = new StackPanel { Spacing = 5 };
            var onlineModeRadio = new RadioButton { Content = "在线听写", IsChecked = true, GroupName = "DictationMode" };
            var paperModeRadio = new RadioButton { Content = "纸笔听写", GroupName = "DictationMode" };
            modeStackPanel.Children.Add(onlineModeRadio);
            modeStackPanel.Children.Add(paperModeRadio);
            stackPanel.Children.Add(modeStackPanel);

            var randomOrderSwitch = new ToggleSwitch
                { Header = "随机顺序", IsOn = false, OffContent = "关闭", OnContent = "开启" };
            stackPanel.Children.Add(randomOrderSwitch);

            var readTranslationSwitch = new ToggleSwitch
                { Header = "朗读翻译", IsOn = false, OffContent = "关闭", OnContent = "开启" };
            stackPanel.Children.Add(readTranslationSwitch);

            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var testParams = new WordsPage.DictationTestParamsWithTranslations(
                    _studyWords,
                    randomOrderSwitch.IsOn,
                    readTranslationSwitch.IsOn,
                    paperModeRadio.IsChecked ?? false);
                Frame?.Navigate(typeof(DictationTestPage), testParams);
            }
        }

        private void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            SpeakCurrentWord();
        }


        private async void SpeakCurrentWord()
        {
            if (_synthesizer == null || _studyWords.Count == 0) return;

            try
            {
                _synthesizer.SpeakAsyncCancelAll();
                var word = _studyWords[_currentIndex].Word;

                var englishVoiceName = _viewModel.Settings?.Settings?.WindowsTtsEnglishVoiceName;
                if (!string.IsNullOrEmpty(englishVoiceName))
                {
                    try
                    {
                        _synthesizer.SelectVoice(englishVoiceName);
                    }
                    catch
                    {
                    }
                }

                _synthesizer.SpeakAsync(word);

                var translation = _studyWords[_currentIndex].Translation;
                if (!string.IsNullOrEmpty(translation))
                {
                    await Task.Delay(500);

                    var chineseVoiceName = _viewModel.Settings?.Settings?.WindowsTtsChineseVoiceName;
                    if (!string.IsNullOrEmpty(chineseVoiceName))
                    {
                        try
                        {
                            _synthesizer.SelectVoice(chineseVoiceName);
                        }
                        catch
                        {
                        }
                    }

                    _synthesizer.SpeakAsync(translation);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"朗读失败: {ex.Message}");
            }
        }

        private record DailyQuote(string Text, string Author, string Translation);
    }
}