using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Views;
using Microsoft.UI.Dispatching;

namespace English_Listen_WinUI
{
    public sealed partial class MainWindow : Window
    {
        private readonly MainViewModel? _viewModel;
        private bool _isNavigating = false;
        private static DispatcherTimer? _notificationTimer;
        private static MainWindow? _currentInstance;
        private static Storyboard? _currentToastAnimation;

        public MainWindow()
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Constructor started");

            try
            {
                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent completed");

                _viewModel = App.SharedViewModel ?? throw new InvalidOperationException("SharedViewModel is null");
                System.Diagnostics.Debug.WriteLine("[MainWindow] ViewModel assigned");

                _currentInstance = this;

                _notificationTimer = new DispatcherTimer();
                _notificationTimer.Interval = TimeSpan.FromSeconds(5);
                _notificationTimer.Tick += (s, e) => HideNotification();

                // Set up navigation
                MainNavigationView.SelectionChanged += NavigationView_SelectionChanged;
                MainNavigationView.ItemInvoked += NavigationView_ItemInvoked;
                MainNavigationView.BackRequested += NavigationView_BackRequested;
                ContentFrame.Navigated += ContentFrame_Navigated;
                System.Diagnostics.Debug.WriteLine("[MainWindow] Navigation handlers attached");
                
                // 设置返回按钮状态
                UpdateBackButtonState();

                // Navigate to home page initially
                NavigateToPage(typeof(HomePage));
                System.Diagnostics.Debug.WriteLine("[MainWindow] Initial navigation to HomePage completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] FATAL initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");

                // Write error to log file
                try
                {
                    var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "window_error.log");
                    System.IO.File.WriteAllText(logPath, $"[{DateTime.Now}] WINDOW ERROR:\n{ex}\n\nStack:\n{ex.StackTrace}");
                }
                catch { }
            }

            System.Diagnostics.Debug.WriteLine("[MainWindow] Constructor completed");
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
                try
                {
                    ShowErrorDialog("导航失败", $"页面导航失败: {ex.Message}");
                }
                catch
                {
                    // Ignore dialog errors
                }
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
                "WordsPage" => typeof(WordsPage),
                "MemorizePage" => typeof(MemorizePage),
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
                // Check if we can show a dialog
                if (this.Content?.XamlRoot != null)
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login prompt failed: {ex.Message}");
            }
        }



        private async void ShowErrorDialog(string title, string message)
        {
            try
            {
                // Ensure we have a valid XamlRoot
                if (this.Content?.XamlRoot == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Dialog] Cannot show dialog - XamlRoot is null");
                    return;
                }

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
                    Text = @"English Listen v1.0.0

一个帮助学习英语的听写练习工具

基于 WinUI3 框架开发，使用 Flite 语音引擎。

功能特点：
- 支持自定义词库
- 可调节朗读时间间隔
- 支持深色/浅色主题切换
- 提供测试历史记录查看
- 界面简洁易用

版权 © 2026 dongle。本软件使用 MIT 许可证发布。",
                    TextWrapping = TextWrapping.Wrap
                };
                scrollViewer.Content = textBlock;
                scrollViewer.MaxHeight = 400;

                if (this.Content?.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "关于 English Listen",
                        Content = scrollViewer,
                        CloseButtonText = "关闭",
                        XamlRoot = this.Content.XamlRoot
                    };

                    await dialog.ShowAsync();
                }
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

        // 返回按钮点击事件处理
        private void NavigationView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (ContentFrame != null && ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
                UpdateBackButtonState();
            }
        }

        // 内容框架导航完成事件处理
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content != null)
            {
                var currentPageType = e.Content.GetType();
                UpdateNavigationViewSelection(currentPageType);
                UpdateBackButtonState();
            }
        }

        // 根据页面类型更新导航栏选中项
        private void UpdateNavigationViewSelection(Type pageType)
        {
            if (pageType == null) return;

            string pageName = pageType.Name;
            
            // 查找对应的NavigationViewItem
            NavigationViewItem? selectedItem = null;
            
            // 检查菜单项
            foreach (var item in MainNavigationView.MenuItems)
            {
                if (item is NavigationViewItem navItem)
                {
                    string itemTag = navItem.Tag?.ToString() ?? "";
                    if (itemTag == pageName)
                    {
                        selectedItem = navItem;
                        break;
                    }
                }
            }
            
            // 检查底部菜单项
            if (selectedItem == null)
            {
                foreach (var item in MainNavigationView.FooterMenuItems)
                {
                    if (item is NavigationViewItem navItem)
                    {
                        string itemTag = navItem.Tag?.ToString() ?? "";
                        if (itemTag == pageName)
                        {
                            selectedItem = navItem;
                            break;
                        }
                    }
                }
            }
            
            // 更新选中项
            if (selectedItem != null)
            {
                _isNavigating = true;
                MainNavigationView.SelectedItem = selectedItem;
                _isNavigating = false;
            }
            // 设置页面不需要特殊处理，因为它通过IsSettingsInvoked处理
        }

        // 更新返回按钮状态
        private void UpdateBackButtonState()
        {
            if (ContentFrame != null)
            {
                MainNavigationView.IsBackEnabled = ContentFrame.CanGoBack;
            }
        }

        // 重写导航方法以更新返回按钮状态
        private void NavigateToPage(Type pageType)
        {
            if (pageType == null) return;

            try
            {
                _isNavigating = true;
                System.Diagnostics.Debug.WriteLine($"[Navigation] Navigating to {pageType.Name}");

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
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[Navigation] Successfully navigated to {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Page navigation failed: {ex.Message}");
                try
                {
                    ShowErrorDialog("导航错误", $"页面导航出错: {ex.Message}");
                }
                catch
                {
                    // Ignore dialog errors
                }
            }
            finally
            {
                _isNavigating = false;
                // 更新返回按钮状态
                UpdateBackButtonState();
            }
        }

        public static void ShowNotification(string message, string? title = null)
        {
            _currentInstance?.DispatcherQueue.TryEnqueue(() =>
            {
                _currentInstance?.ShowNotificationInternal(message, title);
            });
        }

        private void ShowNotificationInternal(string message, string? title)
        {
            _notificationTimer?.Stop();
            _currentToastAnimation?.Stop();

            ToastTitle.Text = title ?? "";
            ToastTitle.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
            ToastMessage.Text = message;

            // Reset initial state for animation
            ToastBorder.Opacity = 0;
            ToastTranslate.X = 100;
            ToastBorder.Visibility = Visibility.Visible;

            // Slide-in + fade-in animation
            var storyboard = new Storyboard();

            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EnableDependentAnimation = true
            };
            Storyboard.SetTarget(fadeIn, ToastBorder);
            Storyboard.SetTargetProperty(fadeIn, "Opacity");
            storyboard.Children.Add(fadeIn);

            var slideIn = new DoubleAnimation
            {
                From = 100,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EnableDependentAnimation = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(slideIn, ToastTranslate);
            Storyboard.SetTargetProperty(slideIn, "X");
            storyboard.Children.Add(slideIn);

            _currentToastAnimation = storyboard;
            storyboard.Begin();

            _notificationTimer!.Interval = TimeSpan.FromSeconds(5);
            _notificationTimer!.Start();
        }

        private static void HideNotification()
        {
            _currentInstance?.DispatcherQueue.TryEnqueue(() =>
            {
                _currentInstance?.HideNotificationInternal();
            });
        }

        private void HideNotificationInternal()
        {
            _notificationTimer?.Stop();
            _currentToastAnimation?.Stop();
            ToastBorder.Visibility = Visibility.Collapsed;
        }
    }
}
