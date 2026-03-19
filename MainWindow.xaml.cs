using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Views;

namespace English_Listen_WinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel? _viewModel;
        private bool _isNavigating = false;

        public MainWindow()
        {
            this.InitializeComponent();
            
            try
            {
                _viewModel = App.SharedViewModel ?? throw new InvalidOperationException("SharedViewModel is null");
                
                // Set up navigation
                MainNavigationView.SelectionChanged += NavigationView_SelectionChanged;
                MainNavigationView.ItemInvoked += NavigationView_ItemInvoked;
                
                // Navigate to home page initially
                NavigateToPage(typeof(HomePage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow initialization failed: {ex.Message}");
                ShowErrorDialog("初始化失败", $"程序初始化失败: {ex.Message}");
            }
        }

        private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            // Handle item invoked to prevent double navigation
            if (args.IsSettingsInvoked)
            {
                NavigateToSettings();
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (_isNavigating) return;

            try
            {
                if (args.IsSettingsSelected)
                {
                    NavigateToSettings();
                    return;
                }

                if (args.SelectedItem is NavigationViewItem selectedItem)
                {
                    string pageName = selectedItem.Tag?.ToString() ?? "";
                    NavigateToPageByName(pageName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigation failed: {ex.Message}");
                ShowErrorDialog("导航失败", $"页面导航失败: {ex.Message}");
            }
        }

        private void NavigateToSettings()
        {
            try
            {
                NavigateToPage(typeof(SettingsPage));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings navigation failed: {ex.Message}");
                ShowErrorDialog("设置错误", $"无法打开设置页面: {ex.Message}");
            }
        }

        private void NavigateToPageByName(string pageName)
        {
            if (string.IsNullOrEmpty(pageName)) return;

            // Handle ProgressPage access control
            if (pageName == "ProgressPage" && !IsUserLoggedIn())
            {
                ShowLoginPrompt();
                return;
            }

            Type pageType = pageName switch
            {
                "HomePage" => typeof(HomePage),
                "ModernDictationPage" => typeof(DictationTestPage),
                "WordsPage" => typeof(WordsPage),
                "UserPage" => typeof(UserPage),
                "ProgressPage" => typeof(ProgressPage),
                "HelpPage" => typeof(HelpPage),
                "AboutPage" => typeof(AboutPage),
                _ => typeof(HomePage)
            };

            NavigateToPage(pageType);
        }

        private bool IsUserLoggedIn()
        {
            return !string.IsNullOrEmpty(_viewModel?.Settings?.Settings?.CurrentUser) && _viewModel != null;
        }

        private void ShowLoginPrompt()
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "请先登录",
                    Content = "请先登录才能查看学习进度。",
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };

                _ = dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login prompt failed: {ex.Message}");
            }
        }

        private void NavigateToPage(Type pageType)
        {
            if (pageType == null) return;

            try
            {
                _isNavigating = true;
                
                if (ContentFrame?.Content?.GetType() == pageType)
                {
                    // Already on this page, do nothing
                    return;
                }

                var navigationResult = ContentFrame?.Navigate(pageType) ?? false;
                
                if (!navigationResult)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to navigate to {pageType.Name}");
                    ShowErrorDialog("导航失败", $"无法导航到页面: {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Page navigation failed: {ex.Message}");
                ShowErrorDialog("导航错误", $"页面导航出错: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }

        private async void ShowErrorDialog(string title, string message)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error dialog failed: {ex.Message}");
            }
        }

        // About button click handler
        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var scrollViewer = new ScrollViewer();
                var textBlock = new TextBlock
                {
                    Text = @"English Listen v2.7.0

一个帮助学习英语的听写练习工具

基于 WinUI3 框架开发，使用 Flite 语音引擎。

功能特点：
- 支持自定义词库
- 可调节朗读时间间隔
- 支持深色/浅色主题切换
- 提供测试历史记录查看
- 界面简洁易用

版权 © 2026 JetCPP。本软件使用 MIT 许可证发布。",
                    TextWrapping = TextWrapping.Wrap
                };
                scrollViewer.Content = textBlock;
                scrollViewer.MaxHeight = 400;

                var dialog = new ContentDialog
                {
                    Title = "关于 English Listen",
                    Content = scrollViewer,
                    CloseButtonText = "关闭",
                    XamlRoot = this.Content.XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"About dialog failed: {ex.Message}");
            }
        }

        public void SetSidebarVisibility(bool isVisible)
        {
            try
            {
                MainNavigationView.IsPaneVisible = isVisible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set sidebar visibility: {ex.Message}");
            }
        }

        public void NavigateToHome()
        {
            NavigateToPage(typeof(HomePage));
        }
    }
}