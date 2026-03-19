using System;
using System.Collections.Generic;
using System.IO;
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
            // 获取临时词库文件的完整路径
            string tempFilePath = Path.Combine(Path.GetTempPath(), "english_listen_temp.txt");
            
            // 检查词库文件是否存在且不为空
            if (!File.Exists(tempFilePath))
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
            
            var lines = await File.ReadAllLinesAsync(tempFilePath);
            var validLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            
            if (validLines.Count == 0)
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
            
            // 从全局设置获取随机顺序选项
            bool isRandomOrder = _viewModel?.Settings?.Settings?.IsRandomOrder ?? false;
            
            // 传递临时词库文件路径和随机顺序设置到听写测试页面
            var testParams = new DictationTestParams(tempFilePath, isRandomOrder);
            Frame?.Navigate(typeof(DictationTestPage), testParams);
        }


    }
}