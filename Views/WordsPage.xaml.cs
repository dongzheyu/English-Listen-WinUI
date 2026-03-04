using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class WordsPage : Page
    {
        private readonly MainViewModel _viewModel;
        private string _originalWordsText = "";
        private bool _hasShownLoadDialog = false;
        private Dictionary<string, string> _fileToGroupMap = new();
        private List<string> _unloadedFiles = new();

        public WordsPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += WordsPage_Loaded;
        }

        private async void WordsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            // 确保词库文件列表已加载
            if (_viewModel.WordListFiles.Count == 0)
            {
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch { /* 忽略加载错误 */ }
            }
            
            WordsTextBox.Text = _viewModel.WordsText;
            _originalWordsText = _viewModel.WordsText;
            
            // 仅在首次访问时显示加载对话框
            if (!_hasShownLoadDialog && (_viewModel.WordListFiles.Count > 0 || _viewModel.WordsCount > 0))
            {
                _hasShownLoadDialog = true;
                await ShowLoadFileDialog();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(HomePage));
        }

        private async Task ShowLoadFileDialog()
        {
            if (_viewModel.WordListFiles.Count == 0)
            {
                // 如果没有词库文件，直接进入管理界面
                return;
            }
            
            // 构建分组映射（从配置文件读取）
            await LoadFileGroupMapping();
            
            // 创建选择对话框
            var dialog = new ContentDialog
            {
                Title = "选择要加载的词库",
                PrimaryButtonText = "加载",
                SecondaryButtonText = "跳过",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };
            
            var stackPanel = new StackPanel { Margin = new Thickness(20) };
            
            // 分组显示文件
            var groupedFiles = GroupFilesByCategory(_viewModel.WordListFiles.ToList());
            
            foreach (var group in groupedFiles)
            {
                if (group.Key != "未分组" || group.Value.Count > 0)
                {
                var groupHeader = new TextBlock 
                { 
                    Text = group.Key, 
                    Margin = new Thickness(0, 10, 0, 5)
                };
                    stackPanel.Children.Add(groupHeader);
                    
                    var listView = new ListView { MinHeight = 100, SelectionMode = ListViewSelectionMode.Multiple };
                    foreach (var file in group.Value)
                    {
                        listView.Items.Add(file);
                    }
                    stackPanel.Children.Add(listView);
                }
            }
            
            dialog.Content = stackPanel;
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // 获取选中的文件并加载
                var selectedFiles = new List<string>();
                foreach (var child in stackPanel.Children)
                {
                    if (child is ListView listView)
                    {
                        foreach (var selectedItem in listView.SelectedItems)
                        {
                            selectedFiles.Add(selectedItem.ToString());
                        }
                    }
                }
                
                if (selectedFiles.Count > 0)
                {
                    await LoadSelectedFiles(selectedFiles);
                }
            }
            // Secondary (跳过) 或 Cancel: 直接进入管理界面
        }
        
        private Dictionary<string, List<string>> GroupFilesByCategory(List<string> files)
        {
            var grouped = new Dictionary<string, List<string>>();
            grouped["未分组"] = new List<string>();
            
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (_fileToGroupMap.ContainsKey(fileName))
                {
                    var groupName = _fileToGroupMap[fileName];
                    if (!grouped.ContainsKey(groupName))
                    {
                        grouped[groupName] = new List<string>();
                    }
                    grouped[groupName].Add(fileName);
                }
                else
                {
                    grouped["未分组"].Add(fileName);
                }
            }
            
            // 移除空的"未分组"
            if (grouped["未分组"].Count == 0)
            {
                grouped.Remove("未分组");
            }
            
            return grouped;
        }
        
        private async Task LoadFileGroupMapping()
        {
            try
            {
                // 从配置文件加载分组映射
                var groups = await _viewModel.Settings.LoadWordlistGroupsAsync();
                _fileToGroupMap.Clear();
                
                foreach (var group in groups)
                {
                    foreach (var fileName in group.WordListNames)
                    {
                        _fileToGroupMap[fileName] = group.Name;
                    }
                }
            }
            catch { /* 忽略错误 */ }
        }
        
        private async Task LoadSelectedFiles(List<string> selectedFiles)
        {
            var allWords = new List<string>();
            
            foreach (var fileName in selectedFiles)
            {
                var filePath = Path.Combine(_viewModel.Settings.GetWordlistDirectory(), fileName);
                var words = await _viewModel.Settings.LoadWordsFromFileAsync(filePath);
                allWords.AddRange(words);
            }
            
            // 去重并排序
            allWords = allWords.Distinct().OrderBy(w => w).ToList();
            
            _viewModel.WordsText = string.Join(Environment.NewLine, allWords);
            WordsTextBox.Text = _viewModel.WordsText;
            _originalWordsText = _viewModel.WordsText;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            _viewModel.WordsText = WordsTextBox.Text;
            await _viewModel.SaveWordsAsync();
            
            var dialog = new ContentDialog
            {
                Title = "保存成功",
                Content = $"词库已成功保存",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WordsTextBox.Text = "";
        }

        private async void NewWordListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            var dialog = new ContentDialog
            {
                Title = "新建词库",
                Content = new TextBox { PlaceholderText = "请输入词库名称" },
                PrimaryButtonText = "创建",
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
                    // Create empty file
                    var filePath = Path.Combine(
                        _viewModel.Settings.GetWordlistDirectory(),
                        fileName);
                    await File.WriteAllTextAsync(filePath, "");
                    
                    await _viewModel.LoadWordListFilesAsync();
                }
            }
        }

        private async void DeleteWordListButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除当前词库吗？",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // 清空当前内容
                WordsTextBox.Text = "";
                _viewModel.WordsText = "";
                _originalWordsText = "";
            }
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
            // 获取选中的文本并删除
            var text = WordsTextBox.Text;
            var selectionStart = WordsTextBox.SelectionStart;
            var selectionLength = WordsTextBox.SelectionLength;
            
            if (selectionLength > 0)
            {
                // 删除选中的文本
                WordsTextBox.Text = text.Remove(selectionStart, selectionLength);
            }
            else
            {
                // 删除当前行
                var lines = text.Split('\n');
                var currentLineIndex = 0;
                var charCount = 0;
                
                for (int i = 0; i < lines.Length; i++)
                {
                    if (charCount + lines[i].Length >= selectionStart)
                    {
                        currentLineIndex = i;
                        break;
                    }
                    charCount += lines[i].Length + 1; // +1 for newline
                }
                
                // 删除当前行
                if (currentLineIndex < lines.Length)
                {
                    var newLines = lines.Where((line, index) => index != currentLineIndex).ToArray();
                    WordsTextBox.Text = string.Join("\n", newLines);
                }
            }
        }

        private void WordsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 触发搜索/筛选
            if (_viewModel != null)
            {
                ApplyFilter();
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
            if (_viewModel == null) return;
            
            var searchText = SearchTextBox.Text;
            var filterType = (FilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "全部单词";
            
            if (string.IsNullOrEmpty(searchText) && filterType == "全部单词")
            {
                WordsTextBox.Text = _originalWordsText;
                return;
            }
            
            var lines = _originalWordsText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var filteredLines = lines.Where(line =>
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) return false;
                
                // 应用搜索
                if (!string.IsNullOrEmpty(searchText) && !trimmedLine.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                
                // 应用筛选
                switch (filterType)
                {
                    case "以字母开头":
                        return !string.IsNullOrEmpty(trimmedLine) && char.IsLetter(trimmedLine[0]);
                    case "以数字开头":
                        return !string.IsNullOrEmpty(trimmedLine) && char.IsDigit(trimmedLine[0]);
                    default:
                        return true;
                }
            }).ToArray();
            
            WordsTextBox.Text = string.Join("\n", filteredLines);
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;
            
            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".csv");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await _viewModel.ImportWordsFromFileAsync(file.Path);
                await _viewModel.LoadWordListFilesAsync();
                
                // 更新原始文本
                if (_viewModel.WordListFiles.Count > 0)
                {
                    await _viewModel.LoadWordsAsync(_viewModel.WordListFiles[0]);
                    _originalWordsText = _viewModel.WordsText;
                    WordsTextBox.Text = _originalWordsText;
                }
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "wordlist_export"
            };
            picker.FileTypeChoices.Add("文本文件", new[] { ".txt" });
            picker.FileTypeChoices.Add("CSV文件", new[] { ".csv" });

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                var lines = WordsTextBox.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                await Windows.Storage.FileIO.WriteTextAsync(file, string.Join(Environment.NewLine, lines));
                
                var dialog = new ContentDialog
                {
                    Title = "导出成功",
                    Content = $"词库已导出到 {file.Name}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
    }
}