using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class WordsPage : Page
    {
        private readonly MainViewModel _viewModel;
        private string _originalWordsText = "";
        private string _selectedFileName = "";
        private DispatcherTimer? _autoSaveTimer;

        public WordsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += WordsPage_Loaded;
            
            // 初始化自动保存计时器
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500); // 500ms延迟，避免频繁保存
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
            
            // 初始化时处理临时文件 - 保留现有内容，不再重置
            var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
            
            // 如果临时文件不存在，创建新的空文件
            if (!System.IO.File.Exists(tempPath))
            {
                System.IO.File.WriteAllText(tempPath, "");
            }
            
            // 从临时文件加载内容
            var tempContent = System.IO.File.ReadAllText(tempPath);
            WordsTextBox.Text = tempContent;
            _originalWordsText = tempContent;
            
            // 更新ViewModel - WordsText setter will automatically update CurrentWords
            _viewModel.WordsText = tempContent;
            _viewModel.CurrentWordListName = "临时词库";
            
            // Populate list
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
            WordsTextBox.Text = _viewModel.WordsText;
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
                
                // 显示到文本框
                WordsTextBox.Text = string.Join("\n", allWords);
                _originalWordsText = WordsTextBox.Text;
                
                // 更新ViewModel - WordsText setter will automatically update CurrentWords
                _viewModel.WordsText = WordsTextBox.Text;
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

        private async void StartDictationButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取临时文件路径
            string tempFilePath = Path.Combine(Path.GetTempPath(), "english_listen_temp.txt");

            // 检查临时文件是否存在
            if (!File.Exists(tempFilePath))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "临时词库不存在，请先添加单词。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            // 读取临时文件内容，检查是否有有效单词
            var lines = await File.ReadAllLinesAsync(tempFilePath);
            var validLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

            if (validLines.Count == 0)
            {
                var emptyDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "当前没有单词，请先添加单词。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await emptyDialog.ShowAsync();
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

            var stackPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var randomCheckBox = new CheckBox
            {
                Content = "随机顺序",
                IsChecked = false
            };
            stackPanel.Children.Add(randomCheckBox);
            dialog.Content = stackPanel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                bool randomOrder = randomCheckBox.IsChecked ?? false;
                var testParams = new DictationTestParams(tempFilePath, randomOrder);
                Frame?.Navigate(typeof(DictationTestPage), testParams);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WordsTextBox.Text = "";
        }

        private void AddWordButton_Click(object sender, RoutedEventArgs e)
        {
            var word = WordInput.Text.Trim();
            if (!string.IsNullOrEmpty(word))
            {
                var currentText = WordsTextBox.Text;
                if (!string.IsNullOrEmpty(currentText) && !currentText.EndsWith("\n"))
                {
                    WordsTextBox.Text = currentText + "\n" + word;
                }
                else
                {
                    WordsTextBox.Text = currentText + word;
                }
                WordInput.Text = "";
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
            var text = WordsTextBox.Text;
            var selectionStart = WordsTextBox.SelectionStart;
            var selectionLength = WordsTextBox.SelectionLength;
            
            if (selectionLength > 0)
            {
                WordsTextBox.Text = text.Remove(selectionStart, selectionLength);
            }
            else
            {
                string[] lines = text.Split('\n');
                var currentLineIndex = 0;
                var charCount = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    if (charCount + lines[i].Length >= selectionStart)
                    {
                        currentLineIndex = i;
                        break;
                    }
                    charCount += lines[i].Length + 1;
                }
                
                if (currentLineIndex < lines.Length)
                {
                    var newLines = lines.Where((line, index) => index != currentLineIndex).ToArray();
                    WordsTextBox.Text = string.Join("\n", newLines);
                }
            }
        }

        private void WordsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 每次文本变化时重启计时器（防抖）
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
            
            // 保存到临时文件并更新ViewModel
            await SaveToTempFileAsync();
            
            // 同步更新ViewModel - WordsText setter will automatically update CurrentWords
            _viewModel.WordsText = WordsTextBox.Text;
            _viewModel.CurrentWordListName = "临时词库";
        }
        
        private async Task SaveToTempFileAsync()
        {
            try
            {
                var words = WordsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.Trim())
                    .Where(w => !string.IsNullOrEmpty(w))
                    .ToList();
                
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
            WordsTextBox.Text = _originalWordsText;
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
                WordsTextBox.Text = _originalWordsText;
                return;
            }
            
            var lines = _originalWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines.AsEnumerable();
            
            // Apply search filter
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredLines = filteredLines.Where(line =>
                    line.Trim().Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }
            
            // Apply type filter
            switch (filterType)
            {
                case "以字母开头":
                    filteredLines = filteredLines.Where(line =>
                        !string.IsNullOrEmpty(line) && 
                        ((line[0] >= 'a' && line[0] <= 'z') || (line[0] >= 'A' && line[0] <= 'Z')));
                    break;
                case "以数字开头":
                    filteredLines = filteredLines.Where(line =>
                        !string.IsNullOrEmpty(line) && line[0] >= '0' && line[0] <= '9');
                    break;
                case "包含特殊字符":
                    filteredLines = filteredLines.Where(line =>
                        line.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)));
                    break;
            }
            
            WordsTextBox.Text = string.Join("\n", filteredLines);
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            // 让用户选择导入方式
            var importDialog = new ContentDialog
            {
                Title = "选择导入方式",
                Content = "请选择要导入词库的方式：",
                PrimaryButtonText = "默认词库目录",
                SecondaryButtonText = "外部目录",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await importDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 默认词库目录
                await ImportFromDefaultWordlistAsync();
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // 外部目录
                await ImportFromExternalDirectoryAsync();
            }
        }

        private async Task ImportFromDefaultWordlistAsync()
        {
            if (_viewModel == null) return;
            
            // 获取默认词库目录中的文件
            var wordlistDir = _viewModel.Settings.GetWordlistDirectory();
            
            // Debug: Check if directory exists and show path
            if (!System.IO.Directory.Exists(wordlistDir))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Directory Not Found",
                    Content = $"Wordlist directory does not exist: {wordlistDir}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            var files = System.IO.Directory.GetFiles(wordlistDir, "*.txt")
                .Select(f => System.IO.Path.GetFileName(f))
                .Where(f => !string.IsNullOrEmpty(f))
                .ToList();
            
            // Debug: Show found files
            if (files.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "No Files Found",
                    Content = $"No .txt files found in directory: {wordlistDir}\nDirectory exists: {System.IO.Directory.Exists(wordlistDir)}",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            // 让用户选择词库文件
            var fileListDialog = new ContentDialog
            {
                Title = "选择词库文件",
                PrimaryButtonText = "导入",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };
            
            var listView = new ListView
            {
                ItemsSource = files,
                SelectionMode = ListViewSelectionMode.Single,
                Height = 200
            };
            
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock { Text = "请选择要导入的词库文件：" });
            stackPanel.Children.Add(listView);
            fileListDialog.Content = stackPanel;
            
            var fileResult = await fileListDialog.ShowAsync();
            
            if (fileResult == ContentDialogResult.Primary && listView.SelectedItem != null)
            {
                var selectedFile = listView.SelectedItem.ToString();
                if (!string.IsNullOrEmpty(selectedFile))
                {
                    var filePath = System.IO.Path.Combine(wordlistDir, selectedFile);
                    var words = await _viewModel.Settings.LoadWordsFromFileAsync(filePath);
                    AppendWordsToTextBox(words);
                }
            }
        }

        private async Task ImportFromExternalDirectoryAsync()
        {
            if (_viewModel == null) return;
            
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".txt");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                var words = await _viewModel.Settings.LoadWordsFromFileAsync(file.Path);
                AppendWordsToTextBox(words);
            }
        }

        private void AppendWordsToTextBox(List<string> words)
        {
            var currentText = WordsTextBox.Text;
            if (!string.IsNullOrEmpty(currentText) && !currentText.EndsWith("\n"))
            {
                WordsTextBox.Text = currentText + "\n" + string.Join("\n", words);
            }
            else
            {
                WordsTextBox.Text = currentText + string.Join("\n", words);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "导出词库",
                Content = new TextBox { PlaceholderText = "请输入文件名（不含扩展名）" },
                PrimaryButtonText = "导出",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var textBox = dialog.Content as TextBox;
                if (textBox != null && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var fileName = textBox.Text.Trim() + ".txt";
                    var exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wordlist");
                    
                    if (!Directory.Exists(exportDir))
                    {
                        Directory.CreateDirectory(exportDir);
                    }
                    
                    var filePath = Path.Combine(exportDir, fileName);
                    await File.WriteAllTextAsync(filePath, WordsTextBox.Text);
                    
                    await _viewModel.LoadWordListFilesAsync();
                    PopulateWordList();
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "导出成功",
                        Content = $"词库已导出到 {filePath}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
            }
        }
    }
}
