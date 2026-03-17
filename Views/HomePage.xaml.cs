using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class HomePage : Page
    {
        private readonly MainViewModel _viewModel;

        public HomePage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += HomePage_Loaded;
        }

        private void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel?.TestHistory != null && _viewModel.TestHistory.Count > 0)
            {
                StatsPanel.Visibility = Visibility.Visible;
                TotalTestsInfoBar.Title = $"总测试次数: {_viewModel.TestHistory.Count}";
                var avg = _viewModel.TestHistory.Average(t => t.Accuracy);
                AvgAccuracyInfoBar.Title = $"平均正确率: {avg:F1}%";
            }
            
            // Qt版本风格：专注于功能，静态渐变已足够美观
            // 不需要复杂动画，避免性能问题和颜色异常
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if word list is empty (temporary word list file is empty)
            if (string.IsNullOrEmpty(_viewModel.WordsText) || 
                _viewModel.CurrentWords == null || 
                _viewModel.CurrentWords.Count == 0)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "词库为空",
                    Content = "请先在'查看单词'页面添加单词，然后才能开始听写测试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }
            
            ShowDictationModeDialog();
        }

        private async void ShowDictationModeDialog()
        {
            if (_viewModel == null) return;
            
            // Create dialog matching QT6 exactly: 350x200px, simple mode selection
            var dialog = new ContentDialog
            {
                Title = "选择听写模式",
                Width = 350,
                Height = 200,
                XamlRoot = this.XamlRoot
            };

            // Main layout matching QT6 QVBoxLayout
            var mainStackPanel = new StackPanel();
            mainStackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            mainStackPanel.Spacing = 15;
            
            // Title label matching QT6 exactly
            var titleLabel = new TextBlock
            {
                Text = "请选择听写模式：",
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Microsoft YaHei"),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black)
            };
            mainStackPanel.Children.Add(titleLabel);
            
            // Paper dictation button (Primary style - blue theme)
            var paperButton = new Button
            {
                Content = "纸笔听写",
                Width = 120,
                Height = 35,
                Margin = new Microsoft.UI.Xaml.Thickness(0, 0, 0, 10),
                Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"]
            };
            paperButton.Click += (s, e) => 
            {
                dialog.Hide();
                StartPaperDictation();
            };
            mainStackPanel.Children.Add(paperButton);
            
            // Online dictation button (Success style - green theme)
            var onlineButton = new Button
            {
                Content = "在线听写",
                Width = 120,
                Height = 35,
                Style = (Microsoft.UI.Xaml.Style)Application.Current.Resources["AccentButtonStyle"]
            };
            onlineButton.Click += (s, e) => 
            {
                dialog.Hide();
                StartOnlineDictation();
            };
            mainStackPanel.Children.Add(onlineButton);
            
            dialog.Content = mainStackPanel;
            await dialog.ShowAsync();
        }

        private void StartPaperDictation()
        {
            if (_viewModel.CurrentWords == null || _viewModel.CurrentWords.Count == 0)
            {
                ShowWordEmptyWarning();
                return;
            }

            // Apply random order if enabled (from settings)
            if (_viewModel.IsRandomOrder)
            {
                _viewModel.CurrentWords = _viewModel.CurrentWords.OrderBy(x => Guid.NewGuid()).ToList();
            }

            _viewModel.IsOnlineDictationMode = false;
            Frame?.Navigate(typeof(TestPage));
        }

        private void StartOnlineDictation()
        {
            if (_viewModel.CurrentWords == null || _viewModel.CurrentWords.Count == 0)
            {
                ShowWordEmptyWarning();
                return;
            }

            // Save original word order and apply random if enabled
            _viewModel.OriginalWordsOrder = new List<string>(_viewModel.CurrentWords);
            
            if (_viewModel.IsRandomOrder)
            {
                _viewModel.CurrentWords = _viewModel.CurrentWords.OrderBy(x => Guid.NewGuid()).ToList();
            }

            _viewModel.IsOnlineDictationMode = true;
            _viewModel.UserInputs.Clear();
            _viewModel.UserInputs = new List<string>(_viewModel.CurrentWords.Count);
            
            Frame?.Navigate(typeof(TestPage));
        }

        private async void ShowWordEmptyWarning()
        {
            var dialog = new ContentDialog
            {
                Title = "警告",
                Content = "词库为空，请先添加单词",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}