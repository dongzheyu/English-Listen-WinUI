using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Services;
using Windows.System;

namespace English_Listen_WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private MainViewModel? _viewModel;
        private bool _isInitializing = true;
        private BaiduTranslateService _translateService;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.Loaded += SettingsPage_Loaded;
            // 延迟初始化，避免构造函数中抛出异常导致页面无法加载
            _translateService = null!;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 延迟初始化翻译服务，避免构造函数异常导致页面崩溃
                if (_translateService == null)
                {
                    _translateService = new BaiduTranslateService();
                }
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

                // 加载百度翻译API设置
                LoadTranslateApiSettings();

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
                        // 使用DispatcherQueue在主线程执行UI更新
                        this.DispatcherQueue?.TryEnqueue(() =>
                        {
                            try
                            {
                                LoadWindowsTtsVoices();
                                UpdateVoiceModelVisibility();
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"加载语音设置延迟执行错误: {ex.Message}");
                            }
                        });
                    });
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
                        EngineStatusText.Text = $"Windows 11语音合成可用 - {recommended}";
                        EngineStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green);
                    }
                    else
                    {
                        EngineStatusText.Text = "使用SAPI语音引擎";
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
                if (_viewModel?.SpeechService != null)
                {
                    // 加载英文语音
                    if (WindowsTtsEnglishVoiceComboBox != null)
                    {
                        WindowsTtsEnglishVoiceComboBox.Items.Clear();

                        var voices = _viewModel.SpeechService.GetWindowsTtsVoices();
                        var savedVoice = _viewModel.Settings.Settings.WindowsTtsEnglishVoiceName;
                        var selectedIndex = -1;

                        for (int i = 0; i < voices.Length; i++)
                        {
                            var voice = voices[i];
                            // 只添加英文语音
                            if (voice.Culture?.StartsWith("en-", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var item = new ComboBoxItem
                                {
                                    Content = voice.DisplayName,
                                    Tag = voice.Name
                                };
                                WindowsTtsEnglishVoiceComboBox.Items.Add(item);

                                // 选择之前保存的语音
                                if (voice.Name == savedVoice)
                                {
                                    selectedIndex = WindowsTtsEnglishVoiceComboBox.Items.Count - 1;
                                }
                            }
                        }

                        // 如果没有找到保存的语音，选择第一个
                        if (selectedIndex == -1 && WindowsTtsEnglishVoiceComboBox.Items.Count > 0)
                        {
                            selectedIndex = 0;
                        }

                        WindowsTtsEnglishVoiceComboBox.SelectedIndex = selectedIndex;

                        // 应用保存的语音设置到SpeechService
                        if (selectedIndex >= 0 && !string.IsNullOrEmpty(savedVoice))
                        {
                            _viewModel.SpeechService.SetWindowsTtsEnglishVoice(savedVoice);
                        }
                    }

                    // 加载中文语音
                    if (WindowsTtsChineseVoiceComboBox != null)
                    {
                        WindowsTtsChineseVoiceComboBox.Items.Clear();

                        var voices = _viewModel.SpeechService.GetWindowsTtsVoices();
                        var savedVoice = _viewModel.Settings.Settings.WindowsTtsChineseVoiceName;
                        var selectedIndex = -1;

                        for (int i = 0; i < voices.Length; i++)
                        {
                            var voice = voices[i];
                            // 只添加中文语音
                            if (voice.Culture?.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var item = new ComboBoxItem
                                {
                                    Content = voice.DisplayName,
                                    Tag = voice.Name
                                };
                                WindowsTtsChineseVoiceComboBox.Items.Add(item);

                                // 选择之前保存的语音
                                if (voice.Name == savedVoice)
                                {
                                    selectedIndex = WindowsTtsChineseVoiceComboBox.Items.Count - 1;
                                }
                            }
                        }

                        // 如果没有找到保存的语音，选择第一个
                        if (selectedIndex == -1 && WindowsTtsChineseVoiceComboBox.Items.Count > 0)
                        {
                            selectedIndex = 0;
                        }

                        WindowsTtsChineseVoiceComboBox.SelectedIndex = selectedIndex;

                        // 应用保存的语音设置到SpeechService
                        if (selectedIndex >= 0 && !string.IsNullOrEmpty(savedVoice))
                        {
                            _viewModel.SpeechService.SetWindowsTtsChineseVoice(savedVoice);
                        }
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
                if (WindowsTtsVoicePanel != null && WindowsTtsChineseVoicePanel != null && EngineComboBox != null)
                {
                    var selectedEngine = EngineComboBox.SelectedItem as ComboBoxItem;
                    var engineType = selectedEngine?.Tag?.ToString() ?? "Auto";
                    
                    // 显示/隐藏Windows TTS语音面板
                    var showWindowsTtsModel = engineType == "WindowsTTS" || 
                                            (engineType == "Auto" && _viewModel?.SpeechService?.IsWindowsTtsAvailable == true);
                    WindowsTtsVoicePanel.Visibility = showWindowsTtsModel ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                    WindowsTtsChineseVoicePanel.Visibility = showWindowsTtsModel ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"语音模型显示更新错误: {ex.Message}");
            }
        }

        private void WindowsTtsEnglishVoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_viewModel?.Settings?.Settings == null) return;
                
                if (WindowsTtsEnglishVoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString();
                    if (string.IsNullOrEmpty(voiceName)) return;

                    // 保存到设置
                    _viewModel.Settings.Settings.WindowsTtsEnglishVoiceName = voiceName;
                    _ = _viewModel.Settings.SaveSettingsAsync();

                    // 通知SpeechService更新语音设置
                    _viewModel.SpeechService?.SetWindowsTtsEnglishVoice(voiceName);

                    System.Diagnostics.Debug.WriteLine($"Windows TTS英文语音切换为: {voiceName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS语音选择错误: {ex.Message}");
            }
        }

        private void WindowsTtsChineseVoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_viewModel?.Settings?.Settings == null) return;
                
                if (WindowsTtsChineseVoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString();
                    if (string.IsNullOrEmpty(voiceName)) return;

                    // 保存到设置
                    _viewModel.Settings.Settings.WindowsTtsChineseVoiceName = voiceName;
                    _ = _viewModel.Settings.SaveSettingsAsync();

                    // 通知SpeechService更新语音设置
                    _viewModel.SpeechService?.SetWindowsTtsChineseVoice(voiceName);

                    System.Diagnostics.Debug.WriteLine($"Windows TTS中文语音切换为: {voiceName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS语音选择错误: {ex.Message}");
            }
        }

        private async void WindowsTtsEnglishPreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.SpeechService == null) return;
                
                if (WindowsTtsEnglishVoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString();
                    if (string.IsNullOrEmpty(voiceName)) return;

                    await _viewModel.SpeechService.SpeakAsync("Hello, this is a test for English voice.", voiceName, true);
                }
            }
            catch (Exception ex)
            {
                ShowError("试听失败", $"Windows TTS试听失败: {ex.Message}");
            }
        }

        private async void WindowsTtsChinesePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.SpeechService == null) return;
                
                if (WindowsTtsChineseVoiceComboBox.SelectedItem is ComboBoxItem item && item.Tag != null)
                {
                    var voiceName = item.Tag.ToString();
                    if (string.IsNullOrEmpty(voiceName)) return;

                    await _viewModel.SpeechService.SpeakAsync("你好，这是中文语音的测试。", voiceName, false);
                }
            }
            catch (Exception ex)
            {
                ShowError("试听失败", $"Windows TTS试听失败: {ex.Message}");
            }
        }

        private void LoadTranslateApiSettings()
        {
            try
            {
                // 加载API模式
                if (TranslateApiModeComboBox != null)
                {
                    // 默认选择默认API
                TranslateApiModeComboBox.SelectedIndex = 0;
            }

            // 更新剩余限额显示
            UpdateTranslationLimit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"翻译API设置加载错误: {ex.Message}");
            }
        }

        private void UpdateTranslationLimit()
        {
            try
            {
                if (TranslationLimitText != null)
                {
                    var remaining = _translateService?.GetRemainingLimit() ?? 0;
                    TranslationLimitText.Text = $"{remaining}/{BaiduTranslateService.DAILY_LIMIT}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新翻译限额错误: {ex.Message}");
                if (TranslationLimitText != null)
                {
                    TranslationLimitText.Text = "--";
                }
            }
        }

        private void TranslateApiModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;

            try
            {
                if (TranslateApiModeComboBox.SelectedItem is ComboBoxItem item)
                {
                    var mode = item.Tag?.ToString();
                    
                    // 显示/隐藏自定义API面板
                    if (CustomApiPanel != null)
                    {
                        CustomApiPanel.Visibility = mode == "custom" ? Visibility.Visible : Visibility.Collapsed;
                    }

                    // 保存设置
                    if (_viewModel?.Settings?.Settings != null)
                    {
                        _viewModel.Settings.Settings.BaiduTranslateApiMode = mode;
                        _ = _viewModel.Settings.SaveSettingsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API模式切换错误: {ex.Message}");
            }
        }

        private async void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 这个方法不再使用，替换为CustomApiTextBox_TextChanged
        }

        private void CustomApiTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            // 文本框变化时不自动保存，等待用户点击确认按钮
        }

        private async void ConfirmCustomApiButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_viewModel?.Settings?.Settings == null) return;

                var appId = AppIdTextBox?.Text ?? string.Empty;
                var secretKey = SecretKeyBox?.Password ?? string.Empty;

                if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(secretKey))
                {
                    ShowError("错误", "请输入App ID和密钥");
                    return;
                }

                // Clear old plaintext field
                _viewModel.Settings.Settings.BaiduTranslateApiKey = null;
                await _viewModel.Settings.SaveSettingsAsync();

                // SetCustomApiKey now persists to DPAPI via SecretStorageService
                _translateService.SetCustomApiKey(appId, secretKey);

                // 更新限额显示
                UpdateTranslationLimit();

                ShowInfo("成功", "自定义API配置已加密保存");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存自定义API错误: {ex.Message}");
                ShowError("错误", $"保存自定义API失败: {ex.Message}");
            }
        }

        private async void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Launcher.LaunchUriAsync(new Uri("https://fanyi-api.baidu.com/"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开链接错误: {ex.Message}");
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
                    Content = "程序将初始化，所有数据将被删除！\n\n包括：\n- 所有词库文件\n- 用户配置文件\n- 设置配置\n- 词库分组配置\n\n此操作不可恢复，确定要继续吗？",
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    string appDataPath;
                    try
                    {
                        appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                    }
                    catch
                    {
                        appDataPath = AppDomain.CurrentDomain.BaseDirectory;
                    }
                    
                    var configPath = System.IO.Path.Combine(appDataPath, "config");
                    var userDataPath = System.IO.Path.Combine(configPath, "users");
                    
                    // 删除用户配置文件
                    if (System.IO.Directory.Exists(userDataPath))
                    {
                        try
                        {
                            System.IO.Directory.Delete(userDataPath, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除用户目录失败: {ex.Message}");
                        }
                    }
                    
                    // 删除设置配置文件
                    var settingsPath = System.IO.Path.Combine(configPath, "settings.json");
                    if (System.IO.File.Exists(settingsPath))
                    {
                        try
                        {
                            System.IO.File.Delete(settingsPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除设置文件失败: {ex.Message}");
                        }
                    }
                    
                    // 删除词库分组配置文件
                    var wordlistGroupsPath = System.IO.Path.Combine(configPath, "wordlist_groups.ini");
                    if (System.IO.File.Exists(wordlistGroupsPath))
                    {
                        try
                        {
                            System.IO.File.Delete(wordlistGroupsPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除词库分组文件失败: {ex.Message}");
                        }
                    }
                    
                    // 删除所有词库文件 - 直接删除wordlist文件夹然后重新创建
                    var wordlistPath = System.IO.Path.Combine(appDataPath, "wordlist");
                    if (System.IO.Directory.Exists(wordlistPath))
                    {
                        try
                        {
                            System.IO.Directory.Delete(wordlistPath, true);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"删除词库目录失败: {ex.Message}");
                        }
                    }
                    // 重新创建wordlist文件夹
                    try
                    {
                        System.IO.Directory.CreateDirectory(wordlistPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"创建词库目录失败: {ex.Message}");
                    }
                    
                    // 显示成功消息
                    MainWindow.ShowNotification("程序已成功初始化！\n\n所有数据已清除，程序将自动重启。");
                    
                    // 自动重启程序
                    try
                    {
                        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                        var mainModule = currentProcess.MainModule;
                        if (mainModule != null && !string.IsNullOrEmpty(mainModule.FileName))
                        {
                            System.Diagnostics.Process.Start(mainModule.FileName);
                            Application.Current.Exit();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"重启程序失败: {ex.Message}");
                        ShowError("重启失败", "程序已初始化，但自动重启失败，请手动重启程序");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化失败: {ex.Message}");
                ShowError("初始化错误", $"程序初始化失败: {ex.Message}");
            }
        }

        private async void ClaimLimitButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示输入兑换码的对话框
            var dialog = new ContentDialog
            {
                Title = "领取翻译限额",
                PrimaryButtonText = "领取",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot
            };

            var stackPanel = new StackPanel();
            stackPanel.Margin = new Microsoft.UI.Xaml.Thickness(20);
            
            var textBox = new TextBox
            {
                Header = "输入兑换码",
                PlaceholderText = "请输入兑换码",
                Width = 300
            };
            stackPanel.Children.Add(textBox);
            
            // 显示当前剩余限额
            var remainingLimit = _translateService?.GetRemainingLimit() ?? 0;
            var limitTextBlock = new TextBlock
            {
                Text = $"当前剩余翻译限额：{remainingLimit} 个单词",
                Margin = new Microsoft.UI.Xaml.Thickness(0, 10, 0, 0)
            };
            stackPanel.Children.Add(limitTextBlock);
            
            dialog.Content = stackPanel;
            
            // 处理主按钮点击事件以验证输入
            dialog.PrimaryButtonClick += (sender, args) =>
            {
                var code = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    args.Cancel = true;
                }
            };
            
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var code = textBox.Text?.Trim();
                if (string.IsNullOrEmpty(code))
                {
                    ShowError("错误", "兑换码不能为空");
                    return;
                }
                
                try
                {
                    // 验证兑换码
                    var redeemCode = ValidateRedeemCode(code);
                    
                    if (redeemCode != null)
                    {
                        // 领取成功，重置限额为配置的兑换码限额
                        int newLimit = redeemCode.Unlimited ? 1000000 : redeemCode.Limit;
                        
                        // 重置当日限额
                        _translateService?.ResetDailyLimit(newLimit);
                        
                        // 重新获取剩余限额
                        remainingLimit = _translateService?.GetRemainingLimit() ?? 0;
                        
                        // 更新UI显示
                        UpdateTranslationLimit();
                        
                        MainWindow.ShowNotification($"兑换成功！\n已重置翻译限额为 {newLimit} 个单词\n当前剩余限额：{remainingLimit} 个单词");
                    }
                    else
                    {
                        ShowError("错误", "兑换码无效，请检查输入");
                    }
                }
                catch (Exception ex)
                {
                    ShowError("错误", $"领取失败: {ex.Message}");
                }
            }
        }

        private RedeemCode? ValidateRedeemCode(string code)
        {
            try
            {
                var config = LoadRedeemCodes();
                if (config != null && config.RedeemCodes != null)
                {
                    return config.RedeemCodes.FirstOrDefault(c => c.Code == code);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private RedeemCodesConfig? LoadRedeemCodes()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "English_Listen_WinUI.Config.redeem_codes.json";
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            var json = reader.ReadToEnd();
                            return Newtonsoft.Json.JsonConvert.DeserializeObject<RedeemCodesConfig>(json);
                        }
                    }
                }
                
                string appDataPath;
                try
                {
                    appDataPath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
                }
                catch
                {
                    appDataPath = AppDomain.CurrentDomain.BaseDirectory;
                }
                
                var configPath = System.IO.Path.Combine(appDataPath, "config", "redeem_codes.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<RedeemCodesConfig>(json);
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        private class RedeemCodesConfig
        {
            public required List<RedeemCode> RedeemCodes { get; set; }
        }

        private class RedeemCode
        {
            public required string Code { get; set; }
            public required string Description { get; set; }
            public int Limit { get; set; }
            public bool Unlimited { get; set; }
        }

        private void ShowError(string title, string message)
        {
            try
            {
                MainWindow.ShowNotification($"{title}: {message}");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"{title}: {message}");
            }
        }

        private void ShowInfo(string title, string message)
        {
            try
            {
                MainWindow.ShowNotification($"{title}: {message}");
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"{title}: {message}");
            }
        }
    }
}