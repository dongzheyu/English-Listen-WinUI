using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Models;
using System;
using System.Linq;

namespace English_Listen_WinUI.Views
{
    public sealed partial class UserPage : Page
    {
        private readonly MainViewModel _viewModel;

        public MainViewModel ViewModel => _viewModel;

        public UserPage()
        {
            this.InitializeComponent();
            _viewModel = App.SharedViewModel!;
            this.DataContext = _viewModel;
            Loaded += UserPage_Loaded;
        }

        private void UserPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateUserStatus();
        }

        private void UpdateUserStatus()
        {
            var currentUser = _viewModel.Settings.Settings.CurrentUser;
            if (string.IsNullOrEmpty(currentUser))
            {
                UserStatusText.Text = "当前未登录";
                LoginPanel.Visibility = Visibility.Visible;
                LogoutPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                var user = _viewModel.Users.FirstOrDefault(u => u.Username == currentUser);
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

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedUser = UsernameBox.SelectedItem as UserData;
            if (selectedUser == null)
            {
                ShowMessage("请选择用户");
                return;
            }
            
            var username = selectedUser.Username;
            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("请输入密码");
                return;
            }

            // In a real app, you would verify the password hash
            // For now, we'll just check if the user exists
            var user = _viewModel.Users.FirstOrDefault(u => u.Username == username);
            if (user != null)
            {
                // For simplicity, we're not implementing password verification
                // In a real app, you would verify the password hash
                _viewModel.Settings.Settings.CurrentUser = username;
                await _viewModel.Settings.SaveSettingsAsync();
                UpdateUserStatus();
                ShowMessage("登录成功!");
            }
            else
            {
                ShowMessage("用户不存在");
            }
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Settings.Settings.CurrentUser = null;
            await _viewModel.Settings.SaveSettingsAsync();
            UpdateUserStatus();
            ShowMessage("已退出登录");
        }

        private async void CreateUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Show the create user dialog
            await CreateUserDialog.ShowAsync();
        }

        private async void CreateUserDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var username = NewUsernameBox.Text.Trim();
            var nickname = NewNicknameBox.Text.Trim();
            var password = NewPasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            if (string.IsNullOrEmpty(username))
            {
                ShowMessage("请输入用户名");
                args.Cancel = true; // Cancel the dialog closing
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("请输入密码");
                args.Cancel = true; // Cancel the dialog closing
                return;
            }

            if (password != confirmPassword)
            {
                ShowMessage("两次输入的密码不一致");
                args.Cancel = true; // Cancel the dialog closing
                return;
            }

            if (_viewModel.Users.Any(u => u.Username == username))
            {
                ShowMessage("用户名已存在");
                args.Cancel = true; // Cancel the dialog closing
                return;
            }

            var newUser = new UserData
            {
                Username = username,
                Nickname = string.IsNullOrEmpty(nickname) ? username : nickname,
                PasswordHash = "", // In a real app, you would hash the password
                CreatedTime = DateTime.Now,
                LastLoginTime = DateTime.Now,
                IsActive = true
            };

            _viewModel.Users.Add(newUser);
            await _viewModel.Settings.SaveUsersAsync(_viewModel.Users.ToList());

            // Clear input fields
            NewUsernameBox.Text = "";
            NewNicknameBox.Text = "";
            NewPasswordBox.Password = "";
            ConfirmPasswordBox.Password = "";

            ShowMessage("用户创建成功!");
        }

        private void CreateUserDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Clear input fields when dialog is cancelled
            NewUsernameBox.Text = "";
            NewNicknameBox.Text = "";
            NewPasswordBox.Password = "";
            ConfirmPasswordBox.Password = "";
        }

        private void UsernameBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handle user selection in the combo box
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersListView.SelectedItem is UserData selectedUser)
            {
                // Select the user in the combo box for login
                UsernameBox.SelectedItem = selectedUser;
                UsersListView.SelectedItem = null; // Clear selection
            }
        }

        private async void ShowMessage(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "提示",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
}