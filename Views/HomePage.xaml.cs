using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            
            var dialog = new ContentDialog
            {
                Title = "选择听写模式",
                PrimaryButtonText = "顺序听写",
                SecondaryButtonText = "随机听写",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var modeComboBox = new ComboBox
            {
                Header = "听写模式",
                ItemsSource = new[] { "顺序听写", "随机听写" },
                SelectedIndex = 0
            };
            stackPanel.Children.Add(modeComboBox);
            
            var intervalSlider = new Slider
            {
                Header = "朗读间隔 (秒)",
                Minimum = 2,
                Maximum = 10,
                Value = _viewModel.Settings.Settings.ReadInterval,
                StepFrequency = 1
            };
            stackPanel.Children.Add(intervalSlider);
            
            dialog.Content = stackPanel;
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary)
            {
                bool isRandom = modeComboBox.SelectedIndex == 1;
                int interval = (int)intervalSlider.Value;
                
                // Update settings
                _viewModel.Settings.Settings.ReadInterval = interval;
                await _viewModel.Settings.SaveSettingsAsync();
                
                // Navigate to test page
                _viewModel.IsRandomOrder = isRandom;
                Frame?.Navigate(typeof(TestPage));
            }
        }
    }
}