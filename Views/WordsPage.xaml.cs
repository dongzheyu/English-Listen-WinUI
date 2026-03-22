using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Services;

namespace English_Listen_WinUI.Views
{
    public class WordItem : INotifyPropertyChanged
    {
        private string _word = string.Empty;
        private string _translation = string.Empty;
        private bool _isSelected = false;

        public string Word
        {
            get { return _word; }
            set { _word = value; OnPropertyChanged(); }
        }

        public string Translation
        {
            get { return _translation; }
            set { _translation = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed partial class WordsPage : Page
    {
        private readonly MainViewModel _viewModel;
        private string _originalWordsText = "";
        private string _selectedFileName = "";
        private DispatcherTimer? _autoSaveTimer;
        private List<WordItem> _wordList = new List<WordItem>();
        private BaiduTranslateService _translateService;
        private TranslationLibraryService _translationLibrary;

        public WordsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += WordsPage_Loaded;
            
            _translateService = new BaiduTranslateService();
            _translationLibrary = new TranslationLibraryService();
            
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        }

        private async void WordsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            if (_viewModel.WordListFiles.Count == 0)
            {
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch { }
            }
            
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
            
            if (!System.IO.File.Exists(tempPath))
            {
                System.IO.File.WriteAllText(tempPath, "");
            }
            
            var tempContent = System.IO.File.ReadAllText(tempPath);
            _wordList = tempContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .Select(w => new WordItem 
                { 
                    Word = w, 
                    Translation = _translationLibrary.GetTranslation(w) ?? "" 
                })
                .ToList();
            
            UpdateWordsListBox();
            _originalWordsText = tempContent;
            
            _viewModel.WordsText = tempContent;
            _viewModel.CurrentWordListName = "临时词库";
            
            PopulateWordList();
        }

        private void PopulateWordList()
        {
            WordlistListBox.Items.Clear();
            
            foreach (var fileName in _viewModel.WordListFiles)
            {
                WordlistListBox.Items.Add(fileName);
            }
        }

        private void UpdateWordsListBox()
        {
            WordsListBox.ItemsSource = null;
            WordsListBox.ItemsSource = _wordList;
            WordsListBox.UpdateLayout();
            
            // 更新单词数量显示
            if (WordCountTextBlock != null)
            {
                WordCountTextBlock.Text = $"单词数量: {_wordList.Count}";
            }
        }

        private void WordlistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WordlistListBox.SelectedItem != null)
            {
                _selectedFileName = WordlistListBox.SelectedItem.ToString() ?? "";
            }
        }

        private async void LoadWordlistButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || string.IsNullOrEmpty(_selectedFileName)) return;
            
            await _viewModel.LoadWordsAsync(_selectedFileName);
            _wordList = _viewModel.WordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .Select(w => new WordItem { Word = w, Translation = "" })
                .ToList();
            UpdateWordsListBox();
            _originalWordsText = _viewModel.WordsText;
        }

        private async void LoadGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "没有可加载的分组",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var dialog = new ContentDialog
            {
                Title = "加载分组",
                PrimaryButtonText = "加载",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var groupComboBox = new ComboBox
            {
                Header = "选择要加载的分组",
                ItemsSource = groups.Select(g => g.Name).ToList(),
                SelectedIndex = 0
            };
            stackPanel.Children.Add(groupComboBox);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedGroupName = groupComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedGroupName)) return;
                
                var selectedGroup = groups.FirstOrDefault(g => g.Name == selectedGroupName);
                if (selectedGroup == null) return;
                
                // 加载分组中的所有词库文件
                var allWords = new List<string>();
                foreach (var fileName in selectedGroup.WordListNames)
                {
                    var filePath = System.IO.Path.Combine(_viewModel.Settings.GetWordlistDirectory(), fileName);
                    if (System.IO.File.Exists(filePath))
                    {
                        var words = await _viewModel.Settings.LoadWordsFromFileAsync(filePath);
                        allWords.AddRange(words);
                    }
                }
                
                // 显示到列表
                _wordList = allWords.Select(w => new WordItem { Word = w, Translation = "" }).ToList();
                UpdateWordsListBox();
                _originalWordsText = string.Join("\n", allWords);
                
                // 更新ViewModel - WordsText setter will automatically update CurrentWords
                _viewModel.WordsText = _originalWordsText;
                _viewModel.CurrentWordListName = $"分组: {selectedGroupName}";
                
                var successDialog = new ContentDialog
                {
                    Title = "成功",
                    Content = $"分组 '{selectedGroupName}' 加载成功，包含 {allWords.Count} 个单词",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }

        private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "创建分组",
                PrimaryButtonText = "创建",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var nameBox = new TextBox { Header = "分组名称", PlaceholderText = "输入分组名称" };
            stackPanel.Children.Add(nameBox);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "分组名称不能为空",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }
                
                // 创建新分组
                var newGroup = new Models.WordListGroup
                {
                    Name = nameBox.Text.Trim()
                };
                
                // 保存分组
                var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
                groups.Add(newGroup);
                await _viewModel.Settings.SaveWordlistGroupsAsync(groups);
                
                var successDialog = new ContentDialog
                {
                    Title = "成功",
                    Content = $"分组 '{nameBox.Text}' 创建成功",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }

        private async void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "没有可编辑的分组",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var dialog = new ContentDialog
            {
                Title = "编辑分组",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            // 分组选择
            var groupComboBox = new ComboBox
            {
                Header = "选择分组",
                ItemsSource = groups.Select(g => g.Name).ToList(),
                SelectedIndex = 0
            };
            stackPanel.Children.Add(groupComboBox);
            
            // 词库文件列表
            var fileListView = new ListView
            {
                Header = "选择要添加到分组的词库文件",
                SelectionMode = ListViewSelectionMode.Multiple,
                Height = 200
            };
            fileListView.ItemsSource = _viewModel.WordListFiles;
            stackPanel.Children.Add(fileListView);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedGroupName = groupComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedGroupName)) return;
                
                var selectedGroup = groups.FirstOrDefault(g => g.Name == selectedGroupName);
                if (selectedGroup == null) return;
                
                // 更新分组中的词库文件
                selectedGroup.WordListNames.Clear();
                foreach (var selectedItem in fileListView.SelectedItems)
                {
                    if (selectedItem is string fileName)
                    {
                        selectedGroup.WordListNames.Add(fileName);
                    }
                }
                
                // 保存分组
                await _viewModel.Settings.SaveWordlistGroupsAsync(groups);
                
                var successDialog = new ContentDialog
                {
                    Title = "成功",
                    Content = $"分组 '{selectedGroupName}' 编辑成功",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await successDialog.ShowAsync();
            }
        }

        private async void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "没有可删除的分组",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var dialog = new ContentDialog
            {
                Title = "删除分组",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var groupComboBox = new ComboBox
            {
                Header = "选择要删除的分组",
                ItemsSource = groups.Select(g => g.Name).ToList(),
                SelectedIndex = 0
            };
            stackPanel.Children.Add(groupComboBox);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedGroupName = groupComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedGroupName)) return;
                
                // 确认删除
                var confirmDialog = new ContentDialog
                {
                    Title = "确认删除",
                    Content = $"确定要删除分组 '{selectedGroupName}' 吗？",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };
                
                var confirmResult = await confirmDialog.ShowAsync();
                if (confirmResult == ContentDialogResult.Primary)
                {
                    // 删除分组
                    groups.RemoveAll(g => g.Name == selectedGroupName);
                    await _viewModel.Settings.SaveWordlistGroupsAsync(groups);
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "成功",
                        Content = $"分组 '{selectedGroupName}' 删除成功",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }

        private async void WordlistManagementButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "词库管理",
                XamlRoot = this.XamlRoot,
                PrimaryButtonText = "关闭"
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 10 };
            
            // 词库文件列表
            var fileListPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            fileListPanel.Children.Add(new TextBlock { Text = "词库文件:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            
            var wordlistListBox = new ListBox 
            { 
                Width = 300, 
                Height = 100,
                ItemsSource = WordlistListBox.ItemsSource
            };
            wordlistListBox.SelectionChanged += (s, args) => 
            {
                if (wordlistListBox.SelectedItem != null)
                {
                    WordlistListBox.SelectedItem = wordlistListBox.SelectedItem;
                }
            };
            fileListPanel.Children.Add(wordlistListBox);
            stackPanel.Children.Add(fileListPanel);
            
            // 操作按钮 - 使用异步lambda来隐藏对话框后再执行操作
            var buttonsPanel1 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10 };
            
            var loadGroupButton = new Button { Content = "加载分组", Width = 100, Height = 35, Margin = new Thickness(5) };
            loadGroupButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); LoadGroupButton_Click(s, args); };
            buttonsPanel1.Children.Add(loadGroupButton);
            
            var createGroupButton = new Button { Content = "创建分组", Width = 100, Height = 35, Margin = new Thickness(5) };
            createGroupButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); CreateGroupButton_Click(s, args); };
            buttonsPanel1.Children.Add(createGroupButton);
            
            stackPanel.Children.Add(buttonsPanel1);
            
            var buttonsPanel2 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10 };
            
            var editGroupButton = new Button { Content = "编辑分组", Width = 100, Height = 35, Margin = new Thickness(5) };
            editGroupButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); EditGroupButton_Click(s, args); };
            buttonsPanel2.Children.Add(editGroupButton);
            
            var deleteGroupButton = new Button { Content = "删除分组", Width = 100, Height = 35, Margin = new Thickness(5) };
            deleteGroupButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); DeleteGroupButton_Click(s, args); };
            buttonsPanel2.Children.Add(deleteGroupButton);
            
            var deleteWordlistButton = new Button 
            { 
                Content = "删除词库", 
                Width = 100, 
                Height = 35, 
                Margin = new Thickness(5),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 68, 68)),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255))
            };
            deleteWordlistButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); DeleteWordlistButton_Click(s, args); };
            buttonsPanel2.Children.Add(deleteWordlistButton);
            
            stackPanel.Children.Add(buttonsPanel2);
            
            // 导入导出
            var buttonsPanel3 = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            
            var importButton = new Button { Content = "导入词库", Width = 100, Height = 35, Margin = new Thickness(5) };
            importButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); ImportButton_Click(s, args); };
            buttonsPanel3.Children.Add(importButton);
            
            var exportButton = new Button { Content = "导出词库", Width = 100, Height = 35, Margin = new Thickness(5) };
            exportButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); ExportButton_Click(s, args); };
            buttonsPanel3.Children.Add(exportButton);
            
            stackPanel.Children.Add(buttonsPanel3);
            
            dialog.Content = stackPanel;
            await dialog.ShowAsync();
        }

        private async void WordOperationsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "单词操作",
                XamlRoot = this.XamlRoot,
                PrimaryButtonText = "关闭"
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 15 };
            
            // 添加单词
            var addPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var wordInput = new TextBox 
            { 
                Width = 200, 
                PlaceholderText = "输入单词", 
                Margin = new Thickness(0, 0, 10, 0) 
            };
            wordInput.KeyDown += (s, args) =>
            {
                if (args.Key == Windows.System.VirtualKey.Enter)
                {
                    WordInput.Text = wordInput.Text;
                    AddWordButton_Click(s, args);
                    wordInput.Text = "";
                }
            };
            addPanel.Children.Add(wordInput);
            
            var addButton = new Button { Content = "添加单词", Width = 110, Height = 35 };
            addButton.Click += (s, args) =>
            {
                WordInput.Text = wordInput.Text;
                AddWordButton_Click(s, args);
                wordInput.Text = "";
            };
            addPanel.Children.Add(addButton);
            stackPanel.Children.Add(addPanel);
            
            // 操作按钮 - 使用异步lambda来隐藏对话框后再执行操作
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 10 };
            
            var deleteSelectedButton = new Button { Content = "删除选中", Width = 110, Height = 35 };
            deleteSelectedButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); DeleteSelectedButton_Click(s, args); };
            buttonsPanel.Children.Add(deleteSelectedButton);
            
            var clearButton = new Button { Content = "清空列表", Width = 110, Height = 35 };
            clearButton.Click += async (s, args) => { dialog.Hide(); await Task.Delay(100); ClearButton_Click(s, args); };
            buttonsPanel.Children.Add(clearButton);
            
            stackPanel.Children.Add(buttonsPanel);
            
            dialog.Content = stackPanel;
            await dialog.ShowAsync();
        }

        private async void StartDictationButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有单词
            if (_wordList.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "当前没有单词，请先添加单词。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // 询问随机顺序
            var dialog = new ContentDialog
            {
                Title = "听写测试选项",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 10 };
            
            var modeStackPanel = new StackPanel { Spacing = 5 };
            var onlineModeRadio = new RadioButton
            {
                Content = "在线听写",
                IsChecked = true,
                GroupName = "DictationMode"
            };
            var paperModeRadio = new RadioButton
            {
                Content = "纸笔听写",
                GroupName = "DictationMode"
            };
            modeStackPanel.Children.Add(onlineModeRadio);
            modeStackPanel.Children.Add(paperModeRadio);
            stackPanel.Children.Add(modeStackPanel);
            
            var randomOrderSwitch = new ToggleSwitch
            {
                Header = "随机顺序",
                IsOn = false,
                OffContent = "关闭",
                OnContent = "开启"
            };
            stackPanel.Children.Add(randomOrderSwitch);
            
            var readTranslationSwitch = new ToggleSwitch
            {
                Header = "朗读翻译",
                IsOn = false,
                OffContent = "关闭",
                OnContent = "开启"
            };
            stackPanel.Children.Add(readTranslationSwitch);
            
            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                bool randomOrder = randomOrderSwitch.IsOn;
                bool readTranslation = readTranslationSwitch.IsOn;
                bool isPaperMode = paperModeRadio.IsChecked ?? false;
                
                var wordListWithTranslations = _wordList.Select(w => 
                    new DictationTestPage.WordTranslationPair 
                    { 
                        Word = w.Word, 
                        Translation = w.Translation 
                    }).ToList();
                
                var testParams = new DictationTestParamsWithTranslations(wordListWithTranslations, randomOrder, readTranslation, isPaperMode);
                Frame?.Navigate(typeof(DictationTestPage), testParams);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _wordList.Clear();
            UpdateWordsListBox();
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private void AddWordButton_Click(object sender, RoutedEventArgs e)
        {
            var word = WordInput.Text.Trim();
            if (!string.IsNullOrEmpty(word) && !_wordList.Any(w => w.Word == word))
            {
                _wordList.Add(new WordItem { Word = word, Translation = "" });
                UpdateWordsListBox();
                WordInput.Text = "";
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer.Start();
                }
            }
        }

        private void WordInput_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                AddWordButton_Click(sender, e);
            }
        }

        private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _wordList.Where(w => w.IsSelected).ToList();
            foreach (var item in selectedItems)
            {
                _wordList.Remove(item);
            }
            UpdateWordsListBox();
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (SelectAllCheckBox == null) return;
            
            var selectAll = SelectAllCheckBox.IsChecked ?? false;
            
            foreach (var wordItem in _wordList)
            {
                wordItem.IsSelected = selectAll;
            }
        }

        private async void AutoSaveTimer_Tick(object? sender, object e)
        {
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
            }
            
            // 保存到临时文件并更新ViewModel
            await SaveToTempFileAsync();
            
            // 同步更新ViewModel - WordsText setter will automatically update CurrentWords
            _viewModel.WordsText = string.Join("\n", _wordList);
            _viewModel.CurrentWordListName = "临时词库";
        }
        
        private async Task SaveToTempFileAsync()
        {
            try
            {
                var words = _wordList.Select(w => w.Word).Where(w => !string.IsNullOrEmpty(w)).ToList();
                
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                await System.IO.File.WriteAllLinesAsync(tempPath, words);
            }
            catch (Exception ex)
            {
                // 如果无法创建临时文件，记录日志但不中断用户操作
                System.Diagnostics.Debug.WriteLine($"Auto-save failed: {ex.Message}");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ResetFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            FilterComboBox.SelectedIndex = 0;
            // 重新加载原始词库
            var originalLines = _originalWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => !string.IsNullOrEmpty(w))
                .Select(w => new WordItem { Word = w, Translation = "" })
                .ToList();
            _wordList = originalLines;
            UpdateWordsListBox();
        }

        private void ApplyFilter()
        {
            var searchText = SearchTextBox.Text;
            var filterType = (FilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "全部单词";
            
            // Check if _originalWordsText is initialized
            if (string.IsNullOrEmpty(_originalWordsText))
            {
                return;
            }
            
            if (string.IsNullOrEmpty(searchText) && filterType == "全部单词")
            {
                var originalLines = _originalWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrEmpty(w))
                    .Select(w => new WordItem { Word = w, Translation = "" })
                    .ToList();
                _wordList = originalLines;
                UpdateWordsListBox();
                return;
            }
            
            var lines = _originalWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines.AsEnumerable();
            
            // 应用搜索过滤
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredLines = filteredLines.Where(line => line.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }
            
            // 应用类型过滤
            switch (filterType)
            {
                case "以字母开头":
                    filteredLines = filteredLines.Where(line => char.IsLetter(line[0]));
                    break;
                case "以数字开头":
                    filteredLines = filteredLines.Where(line => char.IsDigit(line[0]));
                    break;
                case "包含特殊字符":
                    filteredLines = filteredLines.Where(line => line.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)));
                    break;
            }
            
            _wordList = filteredLines.Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w)).Select(w => new WordItem { Word = w, Translation = _translationLibrary.GetTranslation(w) ?? "" }).ToList();
            UpdateWordsListBox();
        }

        private async void BatchTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _wordList.Where(item => item.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "请选择要翻译的单词",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var remaining = _translateService.GetRemainingLimit();
            if (remaining < selectedItems.Count)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = $"翻译限额不足，剩余{remaining}次，需要{selectedItems.Count}次",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var translationsToSave = new List<(string Word, string Translation)>();
            
            foreach (var item in selectedItems)
            {
                try
                {
                    var translation = await _translateService.TranslateAsync(item.Word);
                    item.Translation = translation;
                    translationsToSave.Add((item.Word, translation));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"翻译失败: {item.Word} - {ex.Message}");
                    item.Translation = "翻译失败";
                }
            }
            
            _translationLibrary.SaveTranslations(translationsToSave);
            
            UpdateWordsListBox();
            
            var successDialog = new ContentDialog
            {
                Title = "成功",
                Content = $"成功翻译 {selectedItems.Count} 个单词，已保存到翻译库",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            // 加载词库文件列表
            await _viewModel.LoadWordListFilesAsync();
            
            if (_viewModel.WordListFiles.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "默认词库目录中没有词库文件",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            // 显示词库选择对话框
            var dialog = new ContentDialog
            {
                Title = "导入词库",
                PrimaryButtonText = "导入",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var fileComboBox = new ComboBox
            {
                Header = "选择要导入的词库文件",
                ItemsSource = _viewModel.WordListFiles,
                SelectedIndex = 0,
                Width = 300
            };
            stackPanel.Children.Add(fileComboBox);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedFileName = fileComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedFileName)) return;
                
                try
                {
                    // 加载选中的词库文件
                    await _viewModel.LoadWordsAsync(selectedFileName);
                    
                    // 从ViewModel获取词库内容（已经包含了从文件加载的内容）
                    _wordList = _viewModel.CurrentWords
                        .Where(w => !string.IsNullOrEmpty(w))
                        .Select(w => new WordItem { Word = w, Translation = "" })
                        .ToList();
                    
                    UpdateWordsListBox();
                    _originalWordsText = _viewModel.WordsText;
                    
                    // 重新加载临时文件以确保数据一致性
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                    if (System.IO.File.Exists(tempPath))
                    {
                        var tempContent = await System.IO.File.ReadAllTextAsync(tempPath);
                        _originalWordsText = tempContent;
                    }
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "成功",
                        Content = $"成功导入词库 '{selectedFileName}'，共 {_wordList.Count} 个单词",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = $"导入失败: {ex.Message}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void DeleteWordlistButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFileName = WordlistListBox.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedFileName))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "请先选择要删除的词库文件",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            // 显示确认对话框
            var confirmDialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除词库 '{selectedFileName}' 吗？此操作不可恢复。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };
            
            var result = await confirmDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    // 获取词库目录路径
                    var wordlistDir = string.Empty;
                    var settingsService = _viewModel.GetType().GetField("_settingsService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_viewModel);
                    if (settingsService != null)
                    {
                        var method = settingsService.GetType().GetMethod("GetWordlistDirectory");
                        wordlistDir = method?.Invoke(settingsService, null)?.ToString();
                    }
                    
                    if (string.IsNullOrEmpty(wordlistDir))
                    {
                        wordlistDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordlist");
                    }
                    
                    var filePath = Path.Combine(wordlistDir, selectedFileName);
                    
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        
                        // 重新加载词库文件列表
                        await _viewModel.LoadWordListFilesAsync();
                        PopulateWordList();
                        
                        var successDialog = new ContentDialog
                        {
                            Title = "成功",
                            Content = $"词库 '{selectedFileName}' 删除成功",
                            CloseButtonText = "确定",
                            XamlRoot = this.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    }
                    else
                    {
                        var errorDialog = new ContentDialog
                        {
                            Title = "错误",
                            Content = "词库文件不存在",
                            CloseButtonText = "确定",
                            XamlRoot = this.XamlRoot
                        };
                        await errorDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = $"删除失败: {ex.Message}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wordList.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "当前没有单词可导出",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            // 显示输入文件名的对话框
            var dialog = new ContentDialog
            {
                Title = "导出词库",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var textBox = new TextBox
            {
                Header = "输入词库文件名（不需要.txt后缀）",
                PlaceholderText = "例如：my_wordlist",
                Width = 300
            };
            stackPanel.Children.Add(textBox);
            
            dialog.Content = stackPanel;
            
            // 处理主按钮点击事件以验证输入
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                var fileName = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(fileName))
                {
                    args.Cancel = true;
                }
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var fileName = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(fileName))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "文件名不能为空",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    return;
                }
                
                // 确保文件名有.txt后缀
                if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".txt";
                }
                
                try
                {
                    // 获取词库目录路径
                    var wordlistDir = _viewModel.GetType().GetProperty("WordlistDirectory")?.GetValue(_viewModel)?.ToString();
                    if (string.IsNullOrEmpty(wordlistDir))
                    {
                        // 通过反射调用 GetWordlistDirectory 方法
                        var settingsService = _viewModel.GetType().GetField("_settingsService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_viewModel);
                        if (settingsService != null)
                        {
                            var method = settingsService.GetType().GetMethod("GetWordlistDirectory");
                            wordlistDir = method?.Invoke(settingsService, null)?.ToString();
                        }
                    }
                    
                    if (string.IsNullOrEmpty(wordlistDir))
                    {
                        // 使用默认路径
                        wordlistDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordlist");
                    }
                    
                    // 确保目录存在
                    if (!Directory.Exists(wordlistDir))
                    {
                        Directory.CreateDirectory(wordlistDir);
                    }
                    
                    var destPath = Path.Combine(wordlistDir, fileName);
                    
                    // 获取临时词库路径（当前列表的内容）
                    var tempContent = string.Join("\n", _wordList.Select(w => w.Word));
                    
                    // 写入文件
                    await Task.Run(() => File.WriteAllText(destPath, tempContent));
                    
                    // 重新加载词库文件列表
                    await _viewModel.LoadWordListFilesAsync();
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "成功",
                        Content = $"成功导出 {_wordList.Count} 个单词到词库目录\n文件名：{fileName}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = $"导出失败: {ex.Message}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
        
        public class DictationTestParamsWithTranslations
        {
            public List<DictationTestPage.WordTranslationPair> WordList { get; set; } = new List<DictationTestPage.WordTranslationPair>();
            public bool RandomOrder { get; set; } = false;
            public bool ReadTranslation { get; set; } = false;
            public bool IsPaperMode { get; set; } = false;

            public DictationTestParamsWithTranslations(List<DictationTestPage.WordTranslationPair> wordList, bool randomOrder = false, bool readTranslation = false, bool isPaperMode = false)
            {
                WordList = wordList;
                RandomOrder = randomOrder;
                ReadTranslation = readTranslation;
                IsPaperMode = isPaperMode;
            }
        }
    }
}
