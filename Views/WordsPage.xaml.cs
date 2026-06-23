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
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Services;
using WinRT.Interop;

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
        private bool _isTranslating = false;

        public WordsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Enabled;
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
            
            // 如果已有单词数据，不再重新加载（NavigationCacheMode 保持状态）
            if (_wordList.Count > 0)
            {
                UpdateWordsListBox();
                PopulateWordList();
                return;
            }
            
            ShowLoading("正在加载词库...");
            try
            {
                var tempWords = await TempFileHelper.ReadWordsAsync();
                var loadedText = string.Join("\n", tempWords);
                
                var wordItems = tempWords
                    .Select(w => new WordItem 
                    { 
                        Word = w, 
                        Translation = _translationLibrary.GetTranslation(w) ?? "" 
                    })
                    .ToList();
                
                _wordList = wordItems;
                _originalWordsText = loadedText;
                UpdateWordsListBox();
                _viewModel.WordsText = loadedText;
                _viewModel.CurrentWordListName = "临时词库";
            }
            finally
            {
                HideLoading();
            }
            
            PopulateWordList();
        }

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // 重建 auto-save timer（OnNavigatedFrom 销毁了它）
            if (_autoSaveTimer == null)
            {
                _autoSaveTimer = new DispatcherTimer();
                _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
                _autoSaveTimer.Tick += AutoSaveTimer_Tick;
            }
        }

        protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Stop and cleanup timer to prevent leaked event handlers
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Tick -= AutoSaveTimer_Tick;
                _autoSaveTimer = null;
            }
            // Save current word state before leaving
            SaveCurrentState();
        }

        private async void SaveCurrentState()
        {
            if (_wordList.Count == 0) return;
            try
            {
                var words = _wordList.Select(w => w.Word).Where(w => !string.IsNullOrEmpty(w)).ToList();
                await TempFileHelper.WriteWordsAsync(words);
                _viewModel.WordsText = string.Join(Environment.NewLine, words);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存状态失败: {ex.Message}");
            }
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

            ShowLoading("正在加载词库...");
            try
            {
                await _viewModel.LoadWordsAsync(_selectedFileName);
                var wordsText = _viewModel.WordsText;
                
                var wordItems = await Task.Run(() =>
                {
                    return wordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(w => w.Trim())
                        .Where(w => !string.IsNullOrEmpty(w))
                        .Select(w => new WordItem { Word = w, Translation = _translationLibrary.GetTranslation(w) ?? "" })
                        .ToList();
                });
                
                _wordList = wordItems;
                UpdateWordsListBox();
                _originalWordsText = wordsText;
                MainWindow.ShowNotification($"已加载 {_wordList.Count} 个单词");
            }
            finally
            {
                HideLoading();
            }
        }

        private async void LoadGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                MainWindow.ShowNotification("没有可加载的分组");
                return;
            }
            
            var dialog = new ContentDialog
            {
                Title = "加载分组",
                PrimaryButtonText = "加载",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 400 };
            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var groupComboBox = new ComboBox
            {
                Header = "选择要加载的分组",
                ItemsSource = groups.Select(g => g.Name).ToList(),
                SelectedIndex = 0
            };
            stackPanel.Children.Add(groupComboBox);
            
            // 词库文件多选列表
            var wordlistListView = new ListView
            {
                Header = "选择要加载的词库文件",
                SelectionMode = ListViewSelectionMode.Multiple,
                Height = 200,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            };
            stackPanel.Children.Add(wordlistListView);
            
            // 全选和反选按钮
            var selectButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 0), Spacing = 10 };
            
            var selectAllButton = new Button { Content = "全选", Width = 80, Height = 30 };
            selectAllButton.Click += (s, args) =>
            {
                wordlistListView.SelectAll();
            };
            selectButtonsPanel.Children.Add(selectAllButton);
            
            var invertSelectButton = new Button { Content = "反选", Width = 80, Height = 30 };
            invertSelectButton.Click += (s, args) =>
            {
                var itemsToDeselect = wordlistListView.SelectedItems.Cast<object>().ToList();
                wordlistListView.SelectAll();
                foreach (var item in itemsToDeselect)
                {
                    wordlistListView.SelectedItems.Remove(item);
                }
            };
            selectButtonsPanel.Children.Add(invertSelectButton);
            
            stackPanel.Children.Add(selectButtonsPanel);
            
            // 切换分组时，更新词库文件列表并默认全选
            void UpdateWordlistForGroup(string? groupName)
            {
                wordlistListView.SelectedItems.Clear();
                var group = groups.FirstOrDefault(g => g.Name == groupName);
                if (group != null && group.WordListNames.Count > 0)
                {
                    wordlistListView.ItemsSource = group.WordListNames;
                    foreach (var fileName in group.WordListNames)
                    {
                        wordlistListView.SelectedItems.Add(fileName);
                    }
                }
                else
                {
                    wordlistListView.ItemsSource = null;
                }
            }
            
            groupComboBox.SelectionChanged += (s, args) =>
            {
                UpdateWordlistForGroup(groupComboBox.SelectedItem?.ToString());
            };
            
            // 初始加载第一个分组的词库文件
            if (groups.Count > 0)
            {
                UpdateWordlistForGroup(groups[0].Name);
            }
            
            scrollViewer.Content = stackPanel;
            dialog.Content = scrollViewer;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedGroupName = groupComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedGroupName)) return;
                
                var selectedGroup = groups.FirstOrDefault(g => g.Name == selectedGroupName);
                if (selectedGroup == null) return;
                
                // 获取选中的词库文件
                var selectedFileNames = wordlistListView.SelectedItems.Cast<string>().ToList();
                if (selectedFileNames.Count == 0)
                {
                    MainWindow.ShowNotification("请至少选择一个词库文件");
                    return;
                }
                
                // 加载选中的词库文件
                ShowLoading("正在加载词库...");
                try
                {
                    var vm = _viewModel;
                    var wordDir = vm.Settings.GetWordlistDirectory();
                    var (groupWordItems, groupOriginalText) = await Task.Run(() =>
                    {
                        var allWords = new List<string>();
                        foreach (var fileName in selectedFileNames)
                        {
                            var filePath = System.IO.Path.Combine(wordDir, fileName);
                            if (System.IO.File.Exists(filePath))
                            {
                                var lines = System.IO.File.ReadAllLines(filePath);
                                allWords.AddRange(lines
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .Select(line => line.Trim()));
                            }
                        }
                        var wordItems = allWords
                            .Where(w => !string.IsNullOrEmpty(w))
                            .Select(w => new WordItem { Word = w, Translation = _translationLibrary.GetTranslation(w) ?? "" })
                            .ToList();
                        return (wordItems, string.Join("\n", allWords));
                    });
                    
                    _wordList = groupWordItems;
                    _originalWordsText = groupOriginalText;
                    UpdateWordsListBox();
                    _viewModel.WordsText = groupOriginalText;
                    _viewModel.CurrentWordListName = $"分组: {selectedGroupName}";
                    MainWindow.ShowNotification($"已加载分组 '{selectedGroupName}'，共 {_wordList.Count} 个单词");
                }
                finally
                {
                    HideLoading();
                }
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
                    MainWindow.ShowNotification("分组名称不能为空");
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
                
                MainWindow.ShowNotification($"分组 '{nameBox.Text}' 创建成功");
            }
        }

        private async void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                MainWindow.ShowNotification("没有可编辑的分组");
                return;
            }
            
            var dialog = new ContentDialog
            {
                Title = "编辑分组",
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 400 };
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
            
            // 全选和反选按钮
            var selectButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0), Spacing = 10 };
            
            var selectAllButton = new Button { Content = "全选", Width = 80, Height = 30 };
            selectAllButton.Click += (s, args) =>
            {
                fileListView.SelectAll();
            };
            selectButtonsPanel.Children.Add(selectAllButton);
            
            var invertSelectButton = new Button { Content = "反选", Width = 80, Height = 30 };
            invertSelectButton.Click += (s, args) =>
            {
                var itemsToDeselect = fileListView.SelectedItems.Cast<object>().ToList();
                fileListView.SelectAll();
                foreach (var item in itemsToDeselect)
                {
                    fileListView.SelectedItems.Remove(item);
                }
            };
            selectButtonsPanel.Children.Add(invertSelectButton);
            
            stackPanel.Children.Add(selectButtonsPanel);
            scrollViewer.Content = stackPanel;
            
            // 切换分组时，预选该分组已包含的词库文件
            groupComboBox.SelectionChanged += (s, args) =>
            {
                fileListView.SelectedItems.Clear();
                var selectedGroupName = groupComboBox.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(selectedGroupName))
                {
                    var selectedGroup = groups.FirstOrDefault(g => g.Name == selectedGroupName);
                    if (selectedGroup != null)
                    {
                        foreach (var fileName in _viewModel.WordListFiles)
                        {
                            if (selectedGroup.WordListNames.Contains(fileName))
                            {
                                fileListView.SelectedItems.Add(fileName);
                            }
                        }
                    }
                }
            };
            
            // 初始预选第一个分组的词库文件
            if (groups.Count > 0)
            {
                var firstGroup = groups[0];
                foreach (var fileName in _viewModel.WordListFiles)
                {
                    if (firstGroup.WordListNames.Contains(fileName))
                    {
                        fileListView.SelectedItems.Add(fileName);
                    }
                }
            }
            
            dialog.Content = scrollViewer;
            
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
                
                MainWindow.ShowNotification($"分组 '{selectedGroupName}' 编辑成功");
            }
        }

        private async void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
            
            if (groups.Count == 0)
            {
                MainWindow.ShowNotification("没有可删除的分组");
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
                    
                    MainWindow.ShowNotification($"分组 '{selectedGroupName}' 删除成功");
                }
            }
        }

        private async void WordlistManagementButton_Click(object sender, RoutedEventArgs e)
        {
            // 确保词库文件列表已加载
            if (_viewModel != null && _viewModel.WordListFiles.Count == 0)
            {
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch { }
            }

            var dialog = new ContentDialog
            {
                Title = "词库管理",
                XamlRoot = this.XamlRoot,
                PrimaryButtonText = "关闭"
            };

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0), Spacing = 10 };
            
            // 词库文件列表
            var fileListPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            fileListPanel.Children.Add(new TextBlock { Text = "词库文件(可多选):" });
            
            var wordlistListView = new ListView
            {
                Width = 300,
                Height = 150,
                SelectionMode = ListViewSelectionMode.Multiple,
                ItemsSource = _viewModel?.WordListFiles
            };
            wordlistListView.SelectionChanged += (s, args) => 
            {
                if (wordlistListView.SelectedItem != null)
                {
                    _selectedFileName = wordlistListView.SelectedItem?.ToString() ?? "";
                }
            };
            fileListPanel.Children.Add(wordlistListView);
            
            // 全选和反选按钮
            var selectButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 5), Spacing = 10 };
            
            var selectAllButton = new Button { Content = "全选", Width = 80, Height = 30 };
            selectAllButton.Click += (s, args) =>
            {
                wordlistListView.SelectAll();
            };
            selectButtonsPanel.Children.Add(selectAllButton);
            
            var invertSelectButton = new Button { Content = "反选", Width = 80, Height = 30 };
            invertSelectButton.Click += (s, args) =>
            {
                var itemsToDeselect = wordlistListView.SelectedItems.Cast<object>().ToList();
                wordlistListView.SelectAll();
                foreach (var item in itemsToDeselect)
                {
                    wordlistListView.SelectedItems.Remove(item);
                }
            };
            selectButtonsPanel.Children.Add(invertSelectButton);
            
            fileListPanel.Children.Add(selectButtonsPanel);
            stackPanel.Children.Add(fileListPanel);
            
            // 加载词库按钮
            var loadWordlistButton = new Button { Content = "加载选中的词库", Width = 150, Height = 35, Margin = new Thickness(0, 5, 0, 0) };
            loadWordlistButton.Click += async (s, args) => 
            {
                var selectedItems = wordlistListView.SelectedItems.Cast<string>().ToList();
                if (selectedItems.Count == 0)
                {
                    MainWindow.ShowNotification("请选择要加载的词库文件");
                    return;
                }
                
                dialog.Hide();
                await Task.Delay(100);
                
                // 加载选中的词库
                ShowLoading("正在加载词库...");
                try
                {
                    var wordDir = _viewModel!.Settings.GetWordlistDirectory();
                    var result = await Task.Run(() =>
                    {
                        var allWords = new List<WordItem>();
                        int fileCount = 0;
                        foreach (var fileName in selectedItems)
                        {
                            var filePath = System.IO.Path.Combine(wordDir, fileName);
                            if (System.IO.File.Exists(filePath))
                            {
                                var lines = System.IO.File.ReadAllLines(filePath);
                                var wordItems = lines
                                    .Where(line => !string.IsNullOrWhiteSpace(line))
                                    .Select(line => line.Trim())
                                    .Where(w => !string.IsNullOrEmpty(w))
                                    .Select(w => new WordItem { Word = w, Translation = _translationLibrary.GetTranslation(w) ?? "" })
                                    .ToList();
                                allWords.AddRange(wordItems);
                                fileCount++;
                            }
                        }
                        var originalText = string.Join(Environment.NewLine, allWords.Select(w => w.Word));
                        return (allWords, originalText, fileCount);
                    });
                    
                    _selectedFileName = selectedItems.LastOrDefault() ?? "";
                    _wordList = result.allWords;
                    _originalWordsText = result.originalText;
                    UpdateWordsListBox();
                    _viewModel!.WordsText = result.originalText;
                    MainWindow.ShowNotification($"已加载 {result.fileCount} 个词库，共 {_wordList.Count} 个单词");
                }
                finally
                {
                    HideLoading();
                }
            };
            stackPanel.Children.Add(loadWordlistButton);
            
            // 分隔线
            var separator = new Microsoft.UI.Xaml.Shapes.Rectangle 
            { 
                Height = 1, 
                Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 200)),
                Margin = new Thickness(0, 10, 0, 10)
            };
            stackPanel.Children.Add(separator);
            
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
            deleteWordlistButton.Click += async (s, args) => 
            {
                var firstSelected = wordlistListView.SelectedItems.Cast<string>().FirstOrDefault();
                if (firstSelected != null)
                {
                    _selectedFileName = firstSelected;
                    dialog.Hide();
                    await Task.Delay(100);
                    DeleteWordlistButton_Click(s, args);
                }
            };
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
            // 获取选中的单词
            var selectedWords = _wordList.Where(w => w.IsSelected).ToList();
            
            // 检查是否有选中的单词
            if (selectedWords.Count == 0)
            {
                MainWindow.ShowNotification("请先选择要听写的单词。");
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
                
                var wordListWithTranslations = selectedWords.Select(w => 
                    new DictationTestPage.WordTranslationPair 
                    { 
                        Word = w.Word, 
                        Translation = w.Translation 
                    }).ToList();
                
                var testParams = new DictationTestParamsWithTranslations(wordListWithTranslations, randomOrder, readTranslation, isPaperMode);
                Frame?.Navigate(typeof(DictationTestPage), testParams);
            }
        }

        // 将当前 _wordList 同步到 _originalWordsText，确保过滤/重置可看到最新数据
        private void SyncOriginalWordsText()
        {
            _originalWordsText = string.Join("\n", _wordList.Select(w => w.Word));
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _wordList.Clear();
            SyncOriginalWordsText();
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
                SyncOriginalWordsText();
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
            try
            {
                // 创建要删除的项的副本列表，避免在遍历时修改集合
                var selectedItems = _wordList.Where(w => w.IsSelected).ToList();
                
                // 使用RemoveAll方法一次性删除所有选中项，避免多次集合修改
                _wordList.RemoveAll(w => w.IsSelected);
                SyncOriginalWordsText();
                
                UpdateWordsListBox();
                if (_autoSaveTimer != null)
                {
                    _autoSaveTimer.Stop();
                    _autoSaveTimer.Start();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除选中项错误: {ex.Message}");
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
            _viewModel.WordsText = string.Join("\n", _wordList.Select(w => w.Word));
            _viewModel.CurrentWordListName = "临时词库";
        }
        
        private async Task SaveToTempFileAsync()
        {
            try
            {
                var words = _wordList.Select(w => w.Word).Where(w => !string.IsNullOrEmpty(w)).ToList();
                await TempFileHelper.WriteWordsAsync(words);
                // 同步 _originalWordsText，确保过滤/重置可看到最新数据
                SyncOriginalWordsText();
            }
            catch (Exception ex)
            {
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

        private void ShowLoading(string statusText)
        {
            if (LoadingProgressRing != null)
            {
                LoadingProgressRing.IsActive = true;
                LoadingProgressRing.Visibility = Visibility.Visible;
            }
            if (LoadingStatusText != null)
            {
                LoadingStatusText.Text = statusText;
                LoadingStatusText.Visibility = Visibility.Visible;
            }
        }

        private void HideLoading()
        {
            if (LoadingProgressRing != null)
            {
                LoadingProgressRing.IsActive = false;
                LoadingProgressRing.Visibility = Visibility.Collapsed;
            }
            if (LoadingStatusText != null)
            {
                LoadingStatusText.Text = "";
                LoadingStatusText.Visibility = Visibility.Collapsed;
            }
        }

        private async void StartMemorizeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedWords = _wordList.Where(w => w.IsSelected).ToList();

            if (selectedWords.Count == 0)
            {
                MainWindow.ShowNotification("请先选择要背诵的单词。");
                return;
            }

            var wordListWithTranslations = selectedWords.Select(w =>
                new DictationTestPage.WordTranslationPair
                {
                    Word = w.Word,
                    Translation = w.Translation
                }).ToList();

            Frame?.Navigate(typeof(MemorizePage), wordListWithTranslations);
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
                    filteredLines = filteredLines.Where(line => !string.IsNullOrEmpty(line) && char.IsLetter(line[0]));
                    break;
                case "以数字开头":
                    filteredLines = filteredLines.Where(line => !string.IsNullOrEmpty(line) && char.IsDigit(line[0]));
                    break;
                case "包含特殊字符":
                    filteredLines = filteredLines.Where(line => !string.IsNullOrEmpty(line) && line.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)));
                    break;
            }
            
            _wordList = filteredLines.Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w)).Select(w => new WordItem { Word = w, Translation = _translationLibrary.GetTranslation(w) ?? "" }).ToList();
            UpdateWordsListBox();
        }

        private async void BatchTranslateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isTranslating)
            {
                return;
            }
            
            _isTranslating = true;
            
            try
            {
                var selectedItems = _wordList.Where(item => item.IsSelected).Select(item => item.Word).ToList();
                if (selectedItems.Count == 0)
                {
                    MainWindow.ShowNotification("请选择要翻译的单词");
                    return;
                }
                
                ShowLoading("正在翻译...");
                
                var totalCount = selectedItems.Count;
                var result = await Task.Run(async () =>
                {
                    var toSave = new List<(string Word, string Translation)>();
                    var translated = 0;
                    var limitHit = false;
                    var skipped = 0;
                    
                    foreach (var word in selectedItems)
                    {
                        try
                        {
                            var translation = await _translateService.TranslateAsync(word);
                            toSave.Add((word, translation));
                            translated++;
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("限额"))
                            {
                                limitHit = true;
                                skipped = totalCount - translated;
                                break;
                            }
                        }
                        
                        // 每翻译10个单词或最后一批时更新进度
                        if (translated % 10 == 0 || translated + skipped == totalCount)
                        {
                            var progress = $"翻译: {translated}/{totalCount}";
                            _ = DispatcherQueue.TryEnqueue(() =>
                            {
                                LoadingStatusText.Text = progress;
                            });
                        }
                    }
                    
                    return (toSave, translated, limitHit, skipped);
                });
                
                // 回UI线程更新列表和翻译缓存
                if (result.toSave.Count > 0)
                {
                    _translationLibrary.SaveTranslations(result.toSave);
                    
                    foreach (var saved in result.toSave)
                    {
                        var item = _wordList.FirstOrDefault(w => w.Word == saved.Word);
                        if (item != null)
                            item.Translation = saved.Translation;
                    }
                }
                
                UpdateWordsListBox();
                
                if (result.limitHit)
                {
                    var remaining = _translateService.GetRemainingLimit();
                    MainWindow.ShowNotification($"翻译限额已用完（今日剩余 {remaining} 个），{result.skipped} 个单词被跳过。成功翻译 {result.translated} 个");
                }
                else
                {
                    MainWindow.ShowNotification($"成功翻译 {result.translated} 个单词，已保存到翻译库");
                }
            }
            finally
            {
                HideLoading();
                _isTranslating = false;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            try
            {
                // 打开文件选择器
                var picker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.List,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    FileTypeFilter = { ".txt" }
                };
                
                // 设置XamlRoot for WinUI3 compatibility
                var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
                InitializeWithWindow.Initialize(picker, hwnd);
                
                // 允许选择多个文件
                var files = await picker.PickMultipleFilesAsync();
                
                if (files.Count > 0)
                {
                    // 获取词库目录路径
                    var wordlistDir = _viewModel.Settings.GetWordlistDirectory();
                    
                    // 确保词库目录存在
                    if (!Directory.Exists(wordlistDir))
                    {
                        Directory.CreateDirectory(wordlistDir);
                    }
                    
                    var importedCount = 0;
                    
                    foreach (var file in files)
                    {
                        try
                        {
                            // 构建目标文件路径
                            var targetPath = Path.Combine(wordlistDir, file.Name);
                            
                            // 检查文件是否已存在
                            if (File.Exists(targetPath))
                            {
                                // 询问是否覆盖
                                var overwriteDialog = new ContentDialog
                                {
                                    Title = "文件已存在",
                                    Content = $"词库文件 '{file.Name}' 已存在，是否覆盖？",
                                    PrimaryButtonText = "覆盖",
                                    CloseButtonText = "跳过",
                                    XamlRoot = this.XamlRoot
                                };
                                
                                var overwriteResult = await overwriteDialog.ShowAsync();
                                if (overwriteResult != ContentDialogResult.Primary)
                                {
                                    continue;
                                }
                            }
                            
                            // 复制文件到词库目录
                            using (var sourceStream = await file.OpenReadAsync())
                            using (var targetStream = File.OpenWrite(targetPath))
                            {
                                await sourceStream.AsStreamForRead().CopyToAsync(targetStream);
                            }
                            importedCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"复制文件失败: {file.Name} - {ex.Message}");
                        }
                    }
                    
                    if (importedCount > 0)
                    {
                        // 重新加载词库文件列表
                        await _viewModel.LoadWordListFilesAsync();
                        PopulateWordList();
                        
                        MainWindow.ShowNotification($"成功导入 {importedCount} 个词库文件");
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.ShowNotification($"导入失败: {ex.Message}");
            }
        }

        private async void DeleteWordlistButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFileName = _selectedFileName;
            if (string.IsNullOrEmpty(selectedFileName))
            {
                MainWindow.ShowNotification("请先选择要删除的词库文件");
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
                        string appDataPath;
                        try
                        {
                            appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                        }
                        catch
                        {
                            appDataPath = AppDomain.CurrentDomain.BaseDirectory;
                        }
                        wordlistDir = Path.Combine(appDataPath, "wordlist");
                    }
                    
                    var filePath = Path.Combine(wordlistDir, selectedFileName);
                    
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        
                        // 重新加载词库文件列表
                        await _viewModel.LoadWordListFilesAsync();
                        PopulateWordList();
                        
                        MainWindow.ShowNotification($"词库 '{selectedFileName}' 删除成功");
                    }
                    else
                    {
                        MainWindow.ShowNotification("词库文件不存在");
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.ShowNotification($"删除失败: {ex.Message}");
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_wordList.Count == 0)
            {
                MainWindow.ShowNotification("当前没有单词可导出");
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
                    MainWindow.ShowNotification("文件名不能为空");
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
                        string appDataPath;
                        try
                        {
                            appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                        }
                        catch
                        {
                            appDataPath = AppDomain.CurrentDomain.BaseDirectory;
                        }
                        wordlistDir = Path.Combine(appDataPath, "wordlist");
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
                    
                    MainWindow.ShowNotification($"成功导出 {_wordList.Count} 个单词到词库目录\n文件名：{fileName}");
                }
                catch (Exception ex)
                {
                    MainWindow.ShowNotification($"导出失败: {ex.Message}");
                }
            }
        }

        private async void AppendToWordlistButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有选中的单词
            var selectedWords = _wordList.Where(w => w.IsSelected).ToList();
            if (selectedWords.Count == 0)
            {
                MainWindow.ShowNotification("请选择要追加的单词");
                return;
            }

            // 确保词库文件列表已加载
            if (_viewModel != null && _viewModel.WordListFiles.Count == 0)
            {
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch { }
            }

            // 显示词库选择窗口
            var dialog = new ContentDialog
            {
                Title = "选择词库",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20), Spacing = 10 };

            var wordlistComboBox = new ComboBox
            {
                Header = "选择要追加到的词库",
                ItemsSource = _viewModel?.WordListFiles,
                SelectedIndex = 0
            };
            stackPanel.Children.Add(wordlistComboBox);

            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var selectedWordlist = wordlistComboBox.SelectedItem?.ToString();
                if (string.IsNullOrEmpty(selectedWordlist))
                {
                    MainWindow.ShowNotification("请选择要追加到的词库");
                    return;
                }

                try
                {
                    // 获取词库文件路径
                    if (_viewModel?.Settings == null)
                    {
                        MainWindow.ShowNotification("无法访问设置服务");
                        return;
                    }
                    var wordlistPath = System.IO.Path.Combine(_viewModel.Settings.GetWordlistDirectory(), selectedWordlist);
                    
                    // 读取现有词库内容
                    var existingWords = new List<string>();
                    if (System.IO.File.Exists(wordlistPath))
                    {
                        existingWords = System.IO.File.ReadAllLines(wordlistPath)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Select(line => line.Trim())
                            .ToList();
                    }

                    // 添加选中的单词（去重）
                    var wordsToAdd = selectedWords.Select(w => w.Word.Trim())
                        .Where(word => !string.IsNullOrWhiteSpace(word) && !existingWords.Contains(word))
                        .ToList();

                    if (wordsToAdd.Count == 0)
                    {
                        MainWindow.ShowNotification("所选单词已存在于词库中");
                        return;
                    }

                    // 追加到词库末尾
                    existingWords.AddRange(wordsToAdd);
                    
                    // 保存词库文件
                    System.IO.File.WriteAllLines(wordlistPath, existingWords);

                    MainWindow.ShowNotification($"成功追加 {wordsToAdd.Count} 个单词到词库 '{selectedWordlist}'");
                }
                catch (Exception ex)
                {
                    MainWindow.ShowNotification($"追加单词失败: {ex.Message}");
                }
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            // 刷新词库文件列表
            if (_viewModel == null) return;
            
            try
            {
                await _viewModel.LoadWordListFilesAsync();
                PopulateWordList();
            }
            catch { }
            
            // 刷新单词的翻译
            foreach (var wordItem in _wordList)
            {
                wordItem.Translation = _translationLibrary.GetTranslation(wordItem.Word) ?? "";
            }
            
            // 更新UI
            UpdateWordsListBox();
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
