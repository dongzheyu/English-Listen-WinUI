using System;
using System.Diagnostics;
using System.IO;
using English_Listen_WinUI.Services;
using English_Listen_WinUI.ViewModels;
using Microsoft.UI.Xaml;

namespace English_Listen_WinUI
{
    public partial class App : Application
    {
        private Window? _window;

        public App()
        {
            InitializeComponent();
        }

        public static MainViewModel? SharedViewModel { get; private set; }
        public static Window? MainWindow => ((App)Current)._window;

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
                await TempFileHelper.ClearAsync();

                SharedViewModel = new MainViewModel();
                await SharedViewModel.InitializeAsync();

                _window = new MainWindow();
                _window.Closed += OnWindowClosed;
                ApplyTheme(SharedViewModel.ThemeMode);
                _window.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动失败: {ex}");
                try
                {
                    var logPath = Path.Combine(AppContext.BaseDirectory, "startup_error.log");
                    File.WriteAllText(logPath, $"[{DateTime.Now}] {ex}");
                }
                catch
                {
                }
            }
        }

        public static void ApplyTheme(int themeMode)
        {
            try
            {
                if (MainWindow?.Content is FrameworkElement rootElement && rootElement.XamlRoot != null)
                {
                    rootElement.RequestedTheme = themeMode switch
                    {
                        1 => ElementTheme.Light,
                        2 => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"主题应用失败: {ex.Message}");
            }
        }

        private async void OnWindowClosed(object sender, WindowEventArgs args)
        {
            try
            {
                if (sender is Window window)
                    window.Closed -= OnWindowClosed;

                SharedViewModel?.Cleanup();
                global::English_Listen_WinUI.MainWindow.CleanupStaticState();
                await TempFileHelper.ClearAsync();
                SharedViewModel = null;
                _window = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"退出清理失败: {ex.Message}");
            }
        }
    }
}