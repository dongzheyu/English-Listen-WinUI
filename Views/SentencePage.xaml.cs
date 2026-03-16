using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using English_Listen_WinUI.Helpers;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class SentencePage : Page
    {
        private List<WordSentenceItem> _allSentences = new();
        private List<WordSentenceItem> _selectedSentences = new();
        private List<string> _categories = new();

        // 简单的数据结构
        private class WordSentenceItem
        {
            public string Word { get; set; } = "";
            public string Sentence { get; set; } = "";
            public string Category { get; set; } = "";
            public bool IsSelected { get; set; } = false;
        }

        public SentencePage()
        {
            this.InitializeComponent();
            Loaded += SentencePage_Loaded;
        }

        private void SentencePage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadSentences();
                SetupUI();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SentencePage加载失败: {ex.Message}");
            }
        }

        private void LoadSentences()
        {
            try
            {
                // 只加载内定的句子，不加载外部词库
                _allSentences.Clear();
                LoadSentencesFromData();
                _categories = _allSentences.Select(s => s.Category).Distinct().OrderBy(c => c).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSentences错误: {ex.Message}");
                LoadBasicSentences();
            }
        }

        private void LoadBasicSentences()
        {
            var basicData = new[]
            {
                new { Sentence = "Hello, how are you? - 你好，你怎么样？", Category = "交流问候" },
                new { Sentence = "What time is it? - 现在几点了？", Category = "日常生活" },
                new { Sentence = "I work every day. - 我每天都工作。", Category = "工作职场" },
                new { Sentence = "This is very good. - 这个非常好。", Category = "情感表达" }
            };

            foreach (var item in basicData)
            {
                _allSentences.Add(new WordSentenceItem
                {
                    Word = "",
                    Sentence = item.Sentence,
                    Category = item.Category
                });
            }

            _categories = new List<string> { "交流问候", "日常生活", "工作职场", "情感表达" };
        }

        private void LoadSentencesFromData()
        {
            try
            {
                // 从SentencesData类加载分类的常用句子
                var categorizedSentences = SentencesData.GetCategorizedSentences();
                
                foreach (var category in categorizedSentences)
                {
                    foreach (var sentenceWithTranslation in category.Value)
                    {
                        // 直接使用完整的句子（英文+中文翻译）
                        _allSentences.Add(new WordSentenceItem
                        {
                            Word = "", // 不显示单词
                            Sentence = sentenceWithTranslation,
                            Category = category.Key
                        });
                    }
                }
                
                // 如果没有加载到任何句子，使用基础句子
                if (_allSentences.Count == 0)
                {
                    LoadBasicSentences();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadSentencesFromData错误: {ex.Message}");
                LoadBasicSentences();
            }
        }

        private void SetupUI()
        {
            if (CategoryComboBox != null)
            {
                CategoryComboBox.Items.Clear();
                CategoryComboBox.Items.Add("所有分类");
                foreach (var category in _categories)
                {
                    CategoryComboBox.Items.Add(category);
                }
                CategoryComboBox.SelectedIndex = 0;
            }

            DisplaySentences(_allSentences);
            
            UpdateStatusInfo();
        }

        private void DisplaySentences(List<WordSentenceItem> sentences)
        {
            if (SentencesListView != null)
            {
                SentencesListView.Items.Clear();
                
                foreach (var item in sentences)
                {
                    var stackPanel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    // 复选框
                    var checkbox = new CheckBox
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    checkbox.Checked += (s, e) => 
                    {
                        item.IsSelected = true;
                        if (!_selectedSentences.Contains(item))
                        {
                            _selectedSentences.Add(item);
                        }
                        UpdateSelectionInfo();
                    };
                    checkbox.Unchecked += (s, e) => 
                    {
                        item.IsSelected = false;
                        _selectedSentences.Remove(item);
                        UpdateSelectionInfo();
                    };
                    stackPanel.Children.Add(checkbox);

                    // 例句（不显示单词）
                    var sentenceText = new TextBlock
                    {
                        Text = item.Sentence,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    stackPanel.Children.Add(sentenceText);

                    // 播放按钮
                    var playButton = new Button
                    {
                        Content = "🔊",
                        Width = 32,
                        Height = 32,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    playButton.Click += (s, e) => PlaySentence(item);
                    stackPanel.Children.Add(playButton);

                    var listItem = new ListViewItem
                    {
                        Content = stackPanel,
                        Tag = item,
                        Padding = new Thickness(10),
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    SentencesListView.Items.Add(listItem);
                }
            }
        }

        // ToggleSelection方法已不再需要，因为使用了复选框

        private void UpdateStatusInfo()
        {
            if (LoadedCountText != null)
            {
                LoadedCountText.Text = $"已加载 {_allSentences.Count} 条例句";
            }
            
            if (SelectedCountText != null)
            {
                SelectedCountText.Text = $"已选择 {_selectedSentences.Count} 条例句";
            }
        }
        
        private void UpdateSelectionInfo()
        {
            UpdateStatusInfo();
        }

        private async void PlaySentence(WordSentenceItem item)
        {
            var viewModel = App.SharedViewModel;
            if (viewModel?.SpeechService != null)
            {
                try
                {
                    await viewModel.SpeechService.SpeakAsync(item.Sentence, viewModel.Settings.Settings.FliteVoiceModel);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"播放失败: {ex.Message}");
                }
            }
        }

        private async void PlaySelectedSentences()
        {
            if (_selectedSentences.Count == 0) return;

            var viewModel = App.SharedViewModel;
            if (viewModel?.SpeechService != null)
            {
                try
                {
                    foreach (var item in _selectedSentences)
                    {
                        await viewModel.SpeechService.SpeakAsync(item.Sentence, viewModel.Settings.Settings.FliteVoiceModel);
                        await Task.Delay(1000); // 1秒间隔
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"批量播放失败: {ex.Message}");
                }
            }
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox?.SelectedItem == null) return;
            
            var selectedCategory = CategoryComboBox.SelectedItem.ToString();
            if (selectedCategory == "所有分类")
            {
                DisplaySentences(_allSentences);
            }
            else
            {
                var filteredSentences = _allSentences.Where(s => s.Category == selectedCategory).ToList();
                DisplaySentences(filteredSentences);
            }
            
            UpdateStatusInfo();
        }

        private void PlayAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSentences.Count > 0)
            {
                PlaySelectedSentences();
            }
            else
            {
                // 如果没有选择，播放当前显示的所有例句
                var currentSentences = new List<WordSentenceItem>();
                foreach (var item in SentencesListView.Items)
                {
                    if (item is ListViewItem listItem && listItem.Tag is WordSentenceItem sentenceItem)
                    {
                        currentSentences.Add(sentenceItem);
                    }
                }
                
                if (currentSentences.Count > 0)
                {
                    _selectedSentences = currentSentences;
                    PlaySelectedSentences();
                }
            }
        }

        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedSentences.Clear();
            
            // 重置所有选择状态
            foreach (var item in _allSentences)
            {
                item.IsSelected = false;
            }
            
            // 重置UI
            if (SentencesListView != null)
            {
                foreach (ListViewItem listItem in SentencesListView.Items)
                {
                    listItem.Background = null;
                }
            }
            
            UpdateSelectionInfo();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedSentences.Clear();
            _selectedSentences.AddRange(_allSentences);
            
            foreach (var item in _allSentences)
            {
                item.IsSelected = true;
            }
            
            // 更新UI
            if (SentencesListView != null)
            {
                foreach (ListViewItem listItem in SentencesListView.Items)
                {
                    listItem.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightBlue);
                }
            }
            
            UpdateSelectionInfo();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allSentences.Count == 0) return;

            var searchText = SearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(searchText))
            {
                DisplaySentences(_allSentences);
                UpdateStatusInfo();
                return;
            }

            // 搜索单词、例句或分类
            var filteredSentences = _allSentences.Where(s => 
                s.Sentence.ToLower().Contains(searchText) ||
                s.Category.ToLower().Contains(searchText)
            ).ToList();
            
            DisplaySentences(filteredSentences);
            UpdateStatusInfo();
        }
    }
}