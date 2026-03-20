using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private MainViewModel? _viewModel;
        private bool _isInitializing = true;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel = App.SharedViewModel ?? throw new InvalidOperationException("SharedViewModel is null");
                if (_viewModel?.Settings?.Settings == null)
                {
                    ShowError("设置加载失败", "无法加载设置数据，请重启应用");
                    return;
                }

                this.DataContext = _viewModel;
                LoadSettings();
                _isInitializing = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsPage加载失败: {ex.Message}");
                ShowError("页面加载失败", $"设置页面加载失败: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                // 加载主题设置
                if (ThemeComboBox != null && _viewModel != null)
                    ThemeComboBox.SelectedIndex = Math.Max(0, Math.Min(2, _viewModel.ThemeMode));

                // 加载朗读间隔
                if (IntervalSlider != null && IntervalText != null && _viewModel != null)
                {
                    IntervalSlider.Value = Math.Max(1, Math.Min(30, _viewModel.ReadInterval));
                    IntervalText.Text = $"{_viewModel.ReadInterval}秒";
                }

                // 加载随机顺序设置
                if (RandomOrderCheckBox != null && _viewModel?.Settings?.Settings != null)
                    RandomOrderCheckBox.IsChecked = _viewModel.Settings.Settings.IsRandomOrder;

                // 加载语音引擎
                LoadEngineSettings();

                // 设置版本号
                if (CurrentVersionLabel != null)
                    CurrentVersionLabel.Text = $"当前版本: v{GetCurrentVersion()}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置加载错误: {ex.Message}");
            }
        }

        private void LoadEngineSettings()
        {
            try
            {
                // 加载语音引擎设置
                if (EngineComboBox != null)
                {
                    var engineType = _viewModel?.Settings?.Settings?.SpeechEngineType ?? "Auto";
                    for (int i = 0; i < EngineComboBox.Items.Count; i++)
                    {
                        if (EngineComboBox.Items[i] is ComboBoxItem item && 
                            item.Tag?.ToString() == engineType)
                        {
                            EngineComboBox.SelectedIndex = i;
                            break;
                        }
                    }
                    
                    UpdateEngineStatus();
                    
                    // 延迟加载Windows TTS语音，确保引擎状态已更新
                    _ = Task.Delay(100).ContinueWith(_ =>
                    {
                        // 简单的延迟执行，避免复杂的线程调度
                        LoadWindowsTtsVoices();
                        UpdateVoiceModelVisibility();
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }

                // 语音模型设置（SAPI only）
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引擎设置加载错误: {ex.Message}");
            }
        }

        private void UpdateEngineStatus()
        {
            try
            {
                if (EngineStatusText != null && _viewModel?.SpeechService != null)
                {
                    var availableEngines = _viewModel.SpeechService.GetAvailableEngines();
                    var recommended = _viewModel.SpeechService.GetRecommendedEngine();
                    
                    if (availableEngines.ContainsKey("WindowsTTS"))
                    {
                        EngineStatusText.Text = $"✅ Windows 11语音合成可用 - {recommended}";
                        EngineStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    }
                    else
                    {
                        EngineStatusText.Text = "ℹ️ 使用SAPI语音引擎";
                        EngineStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引擎状态更新错误: {ex.Message}");
            }
        }



        // 主题切换
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel?.Settings?.Settings == null) return;

            try
            {
                if (ThemeComboBox.SelectedItem is ComboBoxItem item && 
                    int.TryParse(item.Tag?.ToString(), out var themeMode))
                {
                    _viewModel.ThemeMode = themeMode;
                    App.ApplyTheme(themeMode);
                    _ = _viewModel.Settings.SaveSettingsAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"主题切换错误: {ex.Message}");
            }
        }

        // 朗读间隔调整
        private void IntervalSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_isInitializing || _viewModel?.Settings?.Settings == null || IntervalText == null) return;

            try
            {
                var value = Math.Max(1, Math.Min(30, (int)e.NewValue));
                IntervalText.Text = $"{value}秒";
                _viewModel.ReadInterval = value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"间隔调整错误: {ex.Message}");
            }
        }



        // 随机顺序切换
        private void RandomOrderCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _viewModel?.Settings?.Settings == null) return;

            try
            {
                _viewModel.Settings.Settings.IsRandomOrder = RandomOrderCheckBox.IsChecked ?? false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"随机顺序设置错误: {ex.Message}");
            }
        }

        // 引擎选择
        private void EngineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel?.SpeechService == null || _viewModel?.Settings?.Settings == null) return;

            try
            {
                if (EngineComboBox.SelectedItem is ComboBoxItem item)
                {
                    var engineType = item.Tag?.ToString() ?? "Auto";
                    _viewModel.SpeechService.EngineType = engineType;
                    
                    // 更新UI显示
                    UpdateEngineStatus();
                    UpdateVoiceModelVisibility();
                    
                    // 保存设置
                    _viewModel.Settings.Settings.SpeechEngineType = engineType;
                    _ = _viewModel.Settings.SaveSettingsAsync();
                    
                    System.Diagnostics.Debug.WriteLine($"语音引擎切换为: {engineType}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"引擎选择错误: {ex.Message}");
            }
        }

        private void LoadWindowsTtsVoices()
        {
            try
            {
                if (WindowsTtsVoiceComboBox != null && _viewModel?.SpeechService != null)
                {
                    WindowsTtsVoiceComboBox.Items.Clear();

                    var voices = _viewModel.SpeechService.GetWindowsTtsVoices();
                    var savedVoice = _viewModel.Settings.Settings.WindowsTtsVoiceName;
                    var selectedIndex = -1;

                    for (int i = 0; i < voices.Length; i++)
                    {
                        var voice = voices[i];
                        var item = new ComboBoxItem
                        {
                            Content = voice.DisplayName,
                            Tag = voice.Name
                        };
                        WindowsTtsVoiceComboBox.Items.Add(item);

                        // 选择之前保存的语音
                        if (voice.Name == savedVoice)
                        {
                            selectedIndex = i;
                        }
                    }

                    // 如果没有找到保存的语音，选择第一个
                    if (selectedIndex == -1 && voices.Length > 0)
                    {
                        selectedIndex = 0;
                    }

                    WindowsTtsVoiceComboBox.SelectedIndex = selectedIndex;

                    // 应用保存的语音设置到SpeechService
                    if (selectedIndex >= 0 && !string.IsNullOrEmpty(savedVoice))
                    {
                        _viewModel.SpeechService.SetWindowsTtsVoice(savedVoice);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS语音加载错误: {ex.Message}");
            }
        }

        private void UpdateVoiceModelVisibility()
        {
            try
            {
                if (WindowsTtsVoicePanel != null && EngineComboBox != null)
                {
                    var selectedEngine = EngineComboBox.SelectedItem as ComboBoxItem;
                    var engineType = selectedEngine?.Tag?.ToString() ?? "Auto";
                    
                    // 显示/隐藏Windows TTS语音面板
                    var showWindowsTtsModel = engineType == "WindowsTTS" || 
                                            (engineType == "Auto" && _viewModel?.SpeechService?.IsWindowsTtsAvailable == true);
                    WindowsTtsVoicePanel.Visibility = showWindowsTtsModel ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语音模型显示更新错误: {ex.Message}");
            }
        }



        // Windows TTS试听按钮点击事件
        private async void WindowsTtsPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.SpeechService == null) return;
                
                // 获取当前选择的Windows TTS语音
                if (WindowsTtsVoiceComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString() ?? "";
                    
                    // 使用指定语音直接播放测试文本
                    await _viewModel.SpeechService.SpeakWithWindowsTtsVoiceAsync(
                        "这是一段语音测试，您正在试听Windows系统语音引擎。", 
                        voiceName);
                    
                    System.Diagnostics.Debug.WriteLine($"Windows TTS试听完成: {voiceName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS试听失败: {ex.Message}");
                ShowError("试听失败", $"Windows TTS试听失败: {ex.Message}");
            }
        }

        // Windows TTS语音选择变化事件
        private void WindowsTtsVoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _viewModel?.Settings?.Settings == null) return;

            try
            {
                if (WindowsTtsVoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString();
                    if (string.IsNullOrEmpty(voiceName)) return;

                    // 保存到设置
                    _viewModel.Settings.Settings.WindowsTtsVoiceName = voiceName;
                    _ = _viewModel.Settings.SaveSettingsAsync();

                    // 通知SpeechService更新语音设置
                    _viewModel.SpeechService?.SetWindowsTtsVoice(voiceName);

                    System.Diagnostics.Debug.WriteLine($"Windows TTS语音切换为: {voiceName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS语音选择错误: {ex.Message}");
            }
        }

        // 检查更新按钮点击事件
        private async void CheckUpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UpdateStatusText != null)
                {
                    UpdateStatusText.Text = "正在检查更新...";
                    UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
                }

                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = false;
                }

                var updateService = new Services.UpdateService();
                var updateInfo = await updateService.CheckForUpdatesAsync();

                if (UpdateStatusText != null)
                {
                    if (updateInfo != null && updateInfo.IsUpdateAvailable)
                    {
                        UpdateStatusText.Text = $"发现新版本: {updateInfo.NewVersion}";
                        UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    }
                    else if (updateInfo != null && updateInfo.IsDevelopmentVersion)
                    {
                        UpdateStatusText.Text = "您正在使用开发版本";
                        UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    }
                    else
                    {
                        UpdateStatusText.Text = "当前已是最新版本";
                        UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    }
                }

                // 显示详细的更新信息对话框（简化版本，避免UI依赖）
                if (updateInfo != null)
                {
                    // 如果是开发版本，只显示提示信息，不提供下载
                    if (updateInfo.IsDevelopmentVersion)
                    {
                        var devDialog = new ContentDialog
                        {
                            Title = "开发版本",
                            Content = $"您正在使用开发版本 {updateInfo.CurrentVersion}，比最新稳定版 {updateInfo.NewVersion} 更高。\n\n开发版本可能不稳定，请谨慎使用。",
                            CloseButtonText = "确定",
                            XamlRoot = this.XamlRoot
                        };
                        await devDialog.ShowAsync();
                    }
                    else if (updateInfo.IsUpdateAvailable)
                    {
                        // 只有真正需要更新时才显示下载选项
                        var resultDialog = new ContentDialog
                        {
                            Title = "发现新版本",
                            Content = updateService.GetUpdateDescription(updateInfo),
                            PrimaryButtonText = "下载更新",
                            CloseButtonText = "取消",
                            XamlRoot = this.XamlRoot
                        };

                        var result = await resultDialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            // 开始下载更新
                            await DownloadAndInstallUpdateAsync(updateInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查更新失败: {ex.Message}");
                
                if (UpdateStatusText != null)
                {
                    UpdateStatusText.Text = $"检查更新失败: {ex.Message}";
                    UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                }

                var errorDialog = new ContentDialog
                {
                    Title = "检查更新失败",
                    Content = $"无法连接到更新服务器: {ex.Message}\n\n请检查网络连接后重试。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
            finally
            {
                if (CheckUpdateButton != null)
                {
                    CheckUpdateButton.IsEnabled = true;
                }
            }
        }

        // 下载并安装更新
        private async Task DownloadAndInstallUpdateAsync(Services.UpdateInfo updateInfo)
        {
            try
            {
                var updateService = new Services.UpdateService();
                
                // 显示进度对话框
                var progressDialog = new ContentDialog
                {
                    Title = "下载更新",
                    Content = "正在下载更新文件，请稍候...",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var progressBar = new Microsoft.UI.Xaml.Controls.ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    Width = 300,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
                };

                var progressText = new TextBlock
                {
                    Text = "准备下载...",
                    HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Center,
                    Margin = new Microsoft.UI.Xaml.Thickness(0, 5, 0, 0)
                };

                var stackPanel = new StackPanel();
                stackPanel.Children.Add(new TextBlock { Text = $"新版本: {updateInfo.NewVersion}", TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap });
                stackPanel.Children.Add(progressBar);
                stackPanel.Children.Add(progressText);

                progressDialog.Content = stackPanel;

                // 创建进度报告器
                var progress = new Progress<int>(value =>
                {
                    progressBar.Value = value;
                    progressText.Text = $"下载进度: {value}%";
                });

                // 显示对话框并开始下载
                var dialogTask = progressDialog.ShowAsync();
                var downloadTask = updateService.DownloadUpdateAsync(updateInfo.DownloadUrl, progress);

                // 等待下载完成
                var downloadedFile = await downloadTask;
                
                // 关闭进度对话框
                progressDialog.Hide();

                if (!string.IsNullOrEmpty(downloadedFile))
                {
                    // 下载完成，启动安装程序
                    await LaunchInstallerAsync(downloadedFile, updateInfo);
                }
                else
                {
                    // 下载失败
                    var errorDialog = new ContentDialog
                    {
                        Title = "下载失败",
                        Content = "下载更新文件失败，请检查网络连接后重试。",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"下载更新异常: {ex.Message}");
                
                var errorDialog = new ContentDialog
                {
                    Title = "下载失败",
                    Content = $"下载更新时发生错误: {ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        // 启动安装程序
        private async Task LaunchInstallerAsync(string installerPath, Services.UpdateInfo updateInfo)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"启动安装程序: {installerPath}");

                if (!System.IO.File.Exists(installerPath))
                {
                    System.Diagnostics.Debug.WriteLine("安装程序不存在");
                    return;
                }

                // 显示安装确认对话框
                var confirmDialog = new ContentDialog
                {
                    Title = "下载完成",
                    Content = $"更新文件已下载完成（版本 {updateInfo.NewVersion}），即将启动安装程序。\n\n安装程序启动后，当前应用将自动关闭。",
                    PrimaryButtonText = "立即安装",
                    CloseButtonText = "稍后安装",
                    XamlRoot = this.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // 启动安装程序
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = installerPath,
                            UseShellExecute = true,
                            CreateNoWindow = false
                        }
                    };

                    process.Start();
                    
                    // 关闭当前应用
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动安装程序异常: {ex.Message}");
                
                var errorDialog = new ContentDialog
                {
                    Title = "启动安装程序失败",
                    Content = $"无法启动安装程序: {ex.Message}\n\n请手动运行下载的安装程序。",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        // 获取当前版本号
        private string GetCurrentVersion()
        {
            try
            {
                var updateService = new Services.UpdateService();
                return updateService.GetCurrentVersion();
            }
            catch
            {
                return "2.7.0"; // 默认版本号
            }
        }

        // 初始化程序
        private async void InitializeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "初始化程序",
                    Content = "程序将初始化，所有数据将被删除！\n\n此操作不可恢复，确定要继续吗？",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var appDataPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "EnglishListen");
                    
                    if (System.IO.Directory.Exists(appDataPath))
                    {
                        System.IO.Directory.Delete(appDataPath, true);
                    }
                    
                    // 返回主页
                    if (this.Frame != null)
                        this.Frame.Navigate(typeof(HomePage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化失败: {ex.Message}");
                ShowError("初始化错误", $"程序初始化失败: {ex.Message}");
            }
        }

        private void ShowError(string title, string message)
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };

                _ = dialog.ShowAsync();
            }
            catch
            {
                // 如果对话框也失败，就记录到调试输出
                System.Diagnostics.Debug.WriteLine($"{title}: {message}");
            }
        }
    }
}