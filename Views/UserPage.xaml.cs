using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace English_Listen_WinUI.Views
{
    public sealed partial class UserPage : Page
    {
        private readonly MainViewModel _viewModel;

        public MainViewModel ViewModel => _viewModel;

        public UserPage()
        {
            // 1. 先确保 ViewModel 不为 null
            _viewModel = App.SharedViewModel ?? new MainViewModel();
            
            if (App.SharedViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("CRITICAL: SharedViewModel is null!");
            }

            this.InitializeComponent();
            this.DataContext = _viewModel;
            Loaded += UserPage_Loaded;
        }

        private void UserPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUserStatus();
        }

        private void UpdateUserStatus()
        {
            try
            {
                var currentUser = _viewModel?.Settings?.Settings?.CurrentUser;
                if (string.IsNullOrEmpty(currentUser))
                {
                    UserStatusText.Text = "当前未登录";
                    LoginPanel.Visibility = Visibility.Visible;
                    LogoutPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var user = _viewModel?.Users?.FirstOrDefault(u => u.Username == currentUser);
                    if (user != null)
                    {
                        UserStatusText.Text = $"当前用户: {user.Nickname} ({user.Username})";
                    }
                    else
                    {
                        UserStatusText.Text = $"当前用户: {currentUser}";
                    }
                    LoginPanel.Visibility = Visibility.Collapsed;
                    LogoutPanel.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateUserStatus error: {ex.Message}");
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UsernameBox == null || PasswordBox == null)
                {
                    await ShowMessage("控件未初始化，请重试");
                    return;
                }

                var selectedUser = UsernameBox.SelectedItem as UserData;
                if (selectedUser == null)
                {
                    await ShowMessage("请选择用户");
                    return;
                }
                
                var username = selectedUser.Username;
                var password = PasswordBox.Password;

                if (string.IsNullOrEmpty(password))
                {
                    await ShowMessage("请输入密码");
                    return;
                }

                // Find the user
                var user = _viewModel.Users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                {
                    // Verify password
                    if (PasswordService.VerifyPassword(password, user.PasswordHash))
                    {
                        if (_viewModel?.Settings?.Settings != null)
                        {
                            _viewModel.Settings.Settings.CurrentUser = username;
                            await _viewModel.Settings.SaveSettingsAsync();
                            UpdateUserStatus();
                            await ShowMessage("登录成功!");
                        }
                        else
                        {
                            await ShowMessage("设置服务不可用");
                        }
                    }
                    else
                    {
                        await ShowMessage("密码错误");
                    }
                }
                else
                {
                    await ShowMessage("用户不存在");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoginButton_Click error: {ex.Message}");
                await ShowMessage("登录过程中发生错误");
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.Settings?.Settings != null)
                {
                    _viewModel.Settings.Settings.CurrentUser = null;
                    await _viewModel.Settings.SaveSettingsAsync();
                    UpdateUserStatus();
                    await ShowMessage("已退出登录");
                }
                else
                {
                    await ShowMessage("设置服务不可用");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogoutButton_Click error: {ex.Message}");
                await ShowMessage("退出登录过程中发生错误");
            }
        }

        private async void CreateUserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 创建动态对话框
                var dialog = new ContentDialog
                {
                    Title = "创建新用户",
                    PrimaryButtonText = "创建",
                    SecondaryButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                // 创建对话框内容
                var stackPanel = new StackPanel { Spacing = 15 };
                var newUsernameBox = new TextBox { Header = "用户名", PlaceholderText = "请输入新用户名" };
                var newNicknameBox = new TextBox { Header = "昵称", PlaceholderText = "请输入昵称 (可选)" };
                var newPasswordBox = new PasswordBox { Header = "密码", PlaceholderText = "请输入密码" };
                var confirmPasswordBox = new PasswordBox { Header = "确认密码", PlaceholderText = "请再次输入密码" };

                stackPanel.Children.Add(newUsernameBox);
                stackPanel.Children.Add(newNicknameBox);
                stackPanel.Children.Add(newPasswordBox);
                stackPanel.Children.Add(confirmPasswordBox);
                dialog.Content = stackPanel;

                // 处理主按钮点击
                dialog.PrimaryButtonClick += async (s, args) =>
                {
                    try
                    {
                        var username = newUsernameBox.Text.Trim();
                        var nickname = newNicknameBox.Text.Trim();
                        var password = newPasswordBox.Password;
                        var confirmPassword = confirmPasswordBox.Password;

                        if (string.IsNullOrEmpty(username))
                        {
                            ShowMessageNoAwait("请输入用户名");
                            args.Cancel = true; // Cancel the dialog closing
                            return;
                        }

                        if (string.IsNullOrEmpty(password))
                        {
                            ShowMessageNoAwait("请输入密码");
                            args.Cancel = true; // Cancel the dialog closing
                            return;
                        }

                        if (password != confirmPassword)
                        {
                            ShowMessageNoAwait("两次输入的密码不一致");
                            args.Cancel = true; // Cancel the dialog closing
                            return;
                        }

                        if (_viewModel.Users.Any(u => u.Username == username))
                        {
                            ShowMessageNoAwait("用户名已存在");
                            args.Cancel = true; // Cancel the dialog closing
                            return;
                        }

                        var newUser = new UserData
                        {
                            Username = username,
                            Nickname = string.IsNullOrEmpty(nickname) ? username : nickname,
                            PasswordHash = PasswordService.HashPassword(password),
                            CreatedTime = DateTime.Now,
                            LastLoginTime = DateTime.Now,
                            IsActive = true
                        };

                        _viewModel.Users.Add(newUser);
                        await _viewModel.Settings.SaveUsersAsync(_viewModel.Users.ToList());

                        // 显示成功消息
                        ShowMessageNoAwait("用户创建成功!");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"CreateUserButton_Click dialog error: {ex.Message}");
                        ShowMessageNoAwait("创建用户过程中发生错误");
                        args.Cancel = true;
                    }
                };

                // 显示对话框
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateUserButton_Click error: {ex.Message}");
                await ShowMessage("创建用户对话框过程中发生错误");
            }
        }

        private void UsernameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle user selection in the combo box
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (UsersListView.SelectedItem is UserData selectedUser)
                {
                    // Select the user in the combo box for login
                    UsernameBox.SelectedItem = selectedUser;
                    UsersListView.SelectedItem = null; // Clear selection
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UsersListView_SelectionChanged error: {ex.Message}");
            }
        }

        private async void DeleteUserButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is UserData userToDelete)
                {
                    // 显示确认对话框
                    var confirmDialog = new ContentDialog
                    {
                        Title = "确认删除",
                        Content = $"确定要删除用户 '{userToDelete.Nickname}' 吗？此操作不可恢复。",
                        PrimaryButtonText = "确定",
                        CloseButtonText = "取消",
                        XamlRoot = this.XamlRoot
                    };

                    var confirmResult = await confirmDialog.ShowAsync();
                    if (confirmResult == ContentDialogResult.Primary)
                    {
                        // 显示密码输入对话框
                        var passwordDialog = new ContentDialog
                        {
                            Title = "验证密码",
                            PrimaryButtonText = "确认",
                            CloseButtonText = "取消",
                            XamlRoot = this.XamlRoot
                        };

                        var stackPanel = new StackPanel { Spacing = 15 };
                        stackPanel.Children.Add(new TextBlock { Text = $"请输入用户 '{userToDelete.Nickname}' 的密码以确认删除" });
                        var passwordBox = new PasswordBox { PlaceholderText = "请输入密码" };
                        stackPanel.Children.Add(passwordBox);
                        passwordDialog.Content = stackPanel;

                        var passwordResult = await passwordDialog.ShowAsync();
                        if (passwordResult == ContentDialogResult.Primary)
                        {
                            try
                            {
                                var password = passwordBox.Password;
                                if (string.IsNullOrEmpty(password))
                                {
                                    await ShowMessage("请输入密码");
                                    return;
                                }

                                // 验证密码
                                if (PasswordService.VerifyPassword(password, userToDelete.PasswordHash))
                                {
                                    // 检查是否是当前登录用户
                                    var currentUser = _viewModel.Settings.Settings.CurrentUser;
                                    if (currentUser == userToDelete.Username)
                                    {
                                        // 先退出登录
                                        _viewModel.Settings.Settings.CurrentUser = null;
                                        await _viewModel.Settings.SaveSettingsAsync();
                                        UpdateUserStatus();
                                    }

                                    // 删除用户（带归属校验）
                                    var deleteResult = await _viewModel.Settings.DeleteUserForCurrentUserAsync(currentUser ?? "", userToDelete.Username);
                                    if (deleteResult)
                                    {
                                        // 从内存中移除用户
                                        _viewModel.Users.Remove(userToDelete);
                                        await ShowMessage("用户删除成功!");
                                    }
                                    else
                                    {
                                        await ShowMessage("用户删除失败，请重试");
                                    }
                                }
                                else
                                {
                                    await ShowMessage("密码错误，删除失败");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"DeleteUserButton_Click password dialog error: {ex.Message}");
                                await ShowMessage("删除用户过程中发生错误");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteUserButton_Click error: {ex.Message}");
                await ShowMessage("删除用户过程中发生错误");
            }
        }

        private void ShowMessageNoAwait(string message)
        {
            MainWindow.ShowNotification(message);
        }

        private async Task ShowMessage(string message)
        {
            MainWindow.ShowNotification(message);
        }

    }
}