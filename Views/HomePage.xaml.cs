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
        private DispatcherTimer? _welcomeTimer;

        public HomePage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += async (s, e) => await HomePage_Loaded(s, e);
        }

        private async Task HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                // Ensure word list is loaded
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch (Exception ex)
                {
                    // Handle any loading errors silently
                }
                
                UpdateStats();
                UpdateWelcomeMessage();
                StartWelcomeAnimation();
                UpdateLoginStatus(); // 恢复登录状态
            }
        }

        private void UpdateLoginStatus()
        {
            // 从ViewModel中获取当前用户状态并更新UI
            string? currentUser = _viewModel?.Settings?.Settings?.CurrentUser;
            if (!string.IsNullOrEmpty(currentUser))
            {
                UserStatusLabel.Text = $"👤 {currentUser}";
                UserStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                LogoutButton.Visibility = Visibility.Visible;
                ProgressButton.IsEnabled = true;
            }
            else
            {
                UserStatusLabel.Text = "👤 未登录";
                UserStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                LogoutButton.Visibility = Visibility.Collapsed;
                ProgressButton.IsEnabled = false;
            }
        }

        private void UpdateWelcomeMessage()
        {
            var hour = DateTime.Now.Hour;
            string greeting;

            if (hour >= 5 && hour < 12)
            {
                greeting = "早上好";
            }
            else if (hour >= 12 && hour < 18)
            {
                greeting = "下午好";
            }
            else if (hour >= 18 && hour < 22)
            {
                greeting = "晚上好";
            }
            else
            {
                greeting = "夜深了";
            }

            WelcomeLabel.Text = $"{greeting}！欢迎使用英语听写练习系统";
            
            // 设置渐变颜色 - 使用和谐的蓝色到紫色渐变
            var gradientBrush = new Microsoft.UI.Xaml.Media.LinearGradientBrush();
            gradientBrush.StartPoint = new Windows.Foundation.Point(0, 0);
            gradientBrush.EndPoint = new Windows.Foundation.Point(1, 1);
            
            // 添加渐变停止点 - 蓝色到紫色的优雅渐变
            gradientBrush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { 
                Color = Windows.UI.Color.FromArgb(255, 79, 172, 254), // 亮蓝色
                Offset = 0.0 
            });
            gradientBrush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { 
                Color = Windows.UI.Color.FromArgb(255, 128, 90, 213), // 紫色
                Offset = 1.0 
            });
            
            WelcomeLabel.Foreground = gradientBrush;
        }

        private void StartWelcomeAnimation()
        {
            _welcomeTimer = new DispatcherTimer();
            _welcomeTimer.Interval = TimeSpan.FromMilliseconds(100);
            _welcomeTimer.Tick += OnUpdateWelcomeAnimation;
            _welcomeTimer.Start();
        }

        private void OnUpdateWelcomeAnimation(object? sender, object e)
        {
            // Create flowing gradient effect using current time
            var time = DateTime.Now.TimeOfDay.TotalMilliseconds / 1000.0;
            
            // Calculate smooth color transitions using sine waves
            var r1 = (byte)(128 + 127 * Math.Sin(time * 0.5));
            var g1 = (byte)(90 + 80 * Math.Sin(time * 0.7 + 1.0));
            var b1 = (byte)(213 + 41 * Math.Sin(time * 0.9 + 2.0));
            
            var r2 = (byte)(79 + 50 * Math.Sin(time * 0.6 + 0.5));
            var g2 = (byte)(172 + 50 * Math.Sin(time * 0.8 + 1.5));
            var b2 = (byte)(254 + 0 * Math.Sin(time * 1.0 + 2.5));
            
            // Ensure colors stay within valid range
            r1 = (byte)Math.Max(0, Math.Min(255, (int)r1));
            g1 = (byte)Math.Max(0, Math.Min(255, (int)g1));
            b1 = (byte)Math.Max(0, Math.Min(255, (int)b1));
            r2 = (byte)Math.Max(0, Math.Min(255, (int)r2));
            g2 = (byte)Math.Max(0, Math.Min(255, (int)g2));
            b2 = (byte)Math.Max(0, Math.Min(255, (int)b2));
            
            // Create gradient brush with flowing colors
            var gradientBrush = new Microsoft.UI.Xaml.Media.LinearGradientBrush();
            gradientBrush.StartPoint = new Windows.Foundation.Point(0, 0);
            gradientBrush.EndPoint = new Windows.Foundation.Point(1, 1);
            
            gradientBrush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { 
                Color = Windows.UI.Color.FromArgb(255, r1, g1, b1),
                Offset = 0.0 
            });
            gradientBrush.GradientStops.Add(new Microsoft.UI.Xaml.Media.GradientStop { 
                Color = Windows.UI.Color.FromArgb(255, r2, g2, b2),
                Offset = 1.0 
            });
            
            WelcomeLabel.Foreground = gradientBrush;
        }

        private async void UserStatusLabel_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            await ShowUserLoginDialog();
        }

        private async Task ShowUserLoginDialog()
        {
            var loginDialog = new ContentDialog
            {
                Title = "用户登录",
                PrimaryButtonText = "登录",
                SecondaryButtonText = "创建新用户",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            var userList = new ListView { Header = "请选择或创建用户", MinHeight = 200 };
            var passwordBox = new PasswordBox { Header = "密码", PlaceholderText = "输入密码" };
            
            // Load and display users
            try
            {
                var users = await _viewModel.Settings.LoadUsersAsync();
                foreach (var user in users)
                {
                    userList.Items.Add(user.Username);
                }
            }
            catch { }

            stackPanel.Children.Add(userList);
            stackPanel.Children.Add(passwordBox);
            loginDialog.Content = stackPanel;
            
            var result = await loginDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                // Handle login
                if (userList.SelectedItem != null)
                {
                    string username = userList.SelectedItem.ToString();
                    // TODO: Verify password and login
                    UpdateUserStatus(username);
                    LogoutButton.Visibility = Visibility.Visible;
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                // 创建新用户 - 保持对话框打开
                await ShowCreateUserDialog();
            }
        }

        private void UpdateUserStatus(string username)
        {
            UserStatusLabel.Text = $"👤 {username}";
            UserStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
            LogoutButton.Visibility = Visibility.Visible;
            ProgressButton.IsEnabled = true;
            
            // Save current user
            if (_viewModel?.Settings?.Settings != null)
            {
                _viewModel.Settings.Settings.CurrentUser = username;
                _ = _viewModel.Settings.SaveSettingsAsync();
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear user status
            UserStatusLabel.Text = "👤 未登录";
            UserStatusLabel.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            LogoutButton.Visibility = Visibility.Collapsed;
            
            // Clear current user in view model
            if (_viewModel?.Settings?.Settings != null)
            {
                _viewModel.Settings.Settings.CurrentUser = null;
                _ = _viewModel.Settings.SaveSettingsAsync();
            }
        }

        private async Task ShowCreateUserDialog()
        {
            var createDialog = new ContentDialog
            {
                Title = "创建新用户",
                PrimaryButtonText = "创建",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var usernameBox = new TextBox { Header = "用户名", PlaceholderText = "输入用户名" };
            var nicknameBox = new TextBox { Header = "昵称", PlaceholderText = "输入昵称" };
            var passwordBox = new PasswordBox { Header = "密码", PlaceholderText = "输入密码" };
            var confirmPasswordBox = new PasswordBox { Header = "确认密码", PlaceholderText = "再次输入密码" };
            
            stackPanel.Children.Add(usernameBox);
            stackPanel.Children.Add(nicknameBox);
            stackPanel.Children.Add(passwordBox);
            stackPanel.Children.Add(confirmPasswordBox);
            createDialog.Content = stackPanel;
            
            var result = await createDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(usernameBox.Text))
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "用户名不能为空",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    await ShowCreateUserDialog();
                    return;
                }

                if (passwordBox.Password != confirmPasswordBox.Password)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "密码不匹配",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    await ShowCreateUserDialog();
                    return;
                }
                
                // Create the user
                bool success = await _viewModel.Settings.CreateUserAsync(
                    usernameBox.Text.Trim(), 
                    nicknameBox.Text.Trim(), 
                    passwordBox.Password);
                
                if (success)
                {
                    var successDialog = new ContentDialog
                    {
                        Title = "成功",
                        Content = "用户创建成功！",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                    
                    // Refresh user list
                    await ShowUserLoginDialog();
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "用户名已存在",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                    await ShowCreateUserDialog();
                }
            }
        }

        private void UpdateStats()
        {
            if (_viewModel?.TestHistory != null && _viewModel.TestHistory.Count > 0)
            {
                StatsPanel.Visibility = Visibility.Visible;
                TotalTestsText.Text = $"总测试次数: {_viewModel.TestHistory.Count}";
                var avg = _viewModel.TestHistory.Average(t => t.Accuracy);
                AvgAccuracyText.Text = $"平均正确率: {avg:F1}%";
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ToggleThemeCommand.Execute(null);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(SettingsPage));
        }

        private void ViewWordsButton_Click(object sender, RoutedEventArgs e)
        {
            Frame?.Navigate(typeof(WordsPage));
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDictationModeDialog();
        }

        private async void ShowDictationModeDialog()
        {
            // 1. 基础 ViewModel 检查 
            if (_viewModel == null) return;

            // 2. 尝试确保加载了词库文件列表 
            if (_viewModel.WordListFiles == null || _viewModel.WordListFiles.Count == 0)
            {
                try
                {
                    await _viewModel.LoadWordListFilesAsync();
                }
                catch { /* 忽略加载错误 */ }
            }

            // 3. 核心检查：如果文件列表依然为空，或者单词总数为 0 
            // 假设 _viewModel.WordsCount 是实时反映当前加载单词数量的属性
            if (_viewModel.WordListFiles == null || _viewModel.WordListFiles.Count == 0 || _viewModel.WordsCount == 0)
            {
                ContentDialog noWordsDialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "当前词库为空，请先在\"查看单词\"页面添加或导入词库文件。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot // WinUI 3 必须设置 XamlRoot 否则会报错 
                };
                await noWordsDialog.ShowAsync();
                return; // 拦截逻辑，不再向下执行导航 
            }

            // 4. 只有通过检查后，才显示模式选择对话框 
            var dialog = new ContentDialog
            {
                Title = "选择听写模式",
                Content = "请选择听写模式：",
                PrimaryButtonText = "纸笔听写",
                SecondaryButtonText = "在线听写",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            
            // 5. 根据选择进行导航 
            if (result == ContentDialogResult.Primary)
            {
                _viewModel.DictationMode = 0;
                Frame?.Navigate(typeof(TestPage));
            }
            else if (result == ContentDialogResult.Secondary)
            {
                _viewModel.DictationMode = 1;
                Frame?.Navigate(typeof(TestPage));
            }
        }

        private void ProgressButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProgressButton.IsEnabled)
            {
                Frame?.Navigate(typeof(ProgressPage));
            }
        }

        private async void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer();
            var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = "English Listen v2.7.0\n\n一个帮助学习英语的听写练习工具\n\n该软件基于 WinUI3 框架开发，支持 Windows SAPI 语音引擎。\n\n功能特点：\n- 支持自定义词库\n- 可调节朗读时间间隔\n- 支持深色/浅色主题切换\n- 提供测试历史记录查看\n\n版权 © 2026 JetCPP。本软件使用 MIT 许可证发布。",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            scrollViewer.Content = textBlock;
            scrollViewer.MaxHeight = 300;

            var dialog = new ContentDialog
            {
                Title = "关于 English Listen",
                Content = scrollViewer,
                CloseButtonText = "关闭",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void GuideButton_Click(object sender, RoutedEventArgs e)
        {
            var scrollViewer = new Microsoft.UI.Xaml.Controls.ScrollViewer();
            var textBlock = new Microsoft.UI.Xaml.Controls.TextBlock
            {
                Text = @"1. 程序简介
English Listen 是一个基于 WinUI3 框架开发的英语听写练习工具。

2. 主要功能
- 词库管理：查看、添加、删除和保存单词列表
- 听写测试：自动朗读单词，支持调节朗读时间间隔
- 主题切换：支持浅色和深色主题
- 词库文件：支持从文件导入词库

3. 使用步骤
(1) 添加单词：点击'查看单词'按钮进入词库管理界面
(2) 开始测试：返回主界面后点击'开始听写测试'按钮
(3) 测试控制：
  - 点击'再读一遍'重新朗读当前单词
  - 点击'上一个'返回前一个单词
  - 点击'下一个'跳到下一个单词
  - 点击'暂停'/'继续'控制测试进程

4. 快捷键说明
- 空格键：重复朗读当前单词
- 左方向键：返回上一个单词
- 右方向键：跳转到下一个单词
- ESC键：暂停/继续测试

5. 注意事项
- 确保系统启用了TTS(text-to-speech)功能
- 测试过程中点击'退出测试'会有确认对话框",
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
            };
            scrollViewer.Content = textBlock;
            scrollViewer.MaxHeight = 400;

            var dialog = new ContentDialog
            {
                Title = "使用指南",
                Content = scrollViewer,
                CloseButtonText = "关闭",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}
