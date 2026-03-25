using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using English_Listen_WinUI.ViewModels;
using English_Listen_WinUI.Helpers;
using Microsoft.Windows.AppLifecycle;

namespace English_Listen_WinUI
{
    public partial class App : Application
    {
        private Window? _window;
        public static MainViewModel? SharedViewModel { get; private set; }
        public static Window? MainWindow => ((App)Current)._window;

        public App()
        {
            InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("[STARTUP] 1. OnLaunched called");

            try
            {
                // Set environment variable for single file publishing
                Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
                System.Diagnostics.Debug.WriteLine("[STARTUP] 2. Environment variable set");

                // Reset temporary file on startup
                ClearTemporaryFile();
                System.Diagnostics.Debug.WriteLine("[STARTUP] 3. Temporary file cleared");

                SharedViewModel = new MainViewModel();
                System.Diagnostics.Debug.WriteLine("[STARTUP] 4. ViewModel created");

                await SharedViewModel.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("[STARTUP] 5. ViewModel initialized");

                // Create window
                _window = new MainWindow();
                System.Diagnostics.Debug.WriteLine("[STARTUP] 6. Window created");

                _window.Closed += OnWindowClosed;
                System.Diagnostics.Debug.WriteLine("[STARTUP] 7. Closed handler attached");

                ApplyTheme(SharedViewModel.ThemeMode);
                System.Diagnostics.Debug.WriteLine("[STARTUP] 8. Theme applied");

                // CRITICAL: Activate the window to bring it to foreground
                // Activate() is the correct WinUI3 method to show and focus the window
                _window.Activate();

                System.Diagnostics.Debug.WriteLine("[STARTUP] 9. Window activated - should now be visible");

                System.Diagnostics.Debug.WriteLine("[STARTUP] 10. COMPLETE - Window activation attempted");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STARTUP] FATAL ERROR: {ex}");
                System.Diagnostics.Debug.WriteLine($"[STARTUP] Stack trace: {ex.StackTrace}");

                // Write to a log file as last resort
                try
                {
                    var logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "startup_error.log");
                    System.IO.File.WriteAllText(logPath, $"[{DateTime.Now}] FATAL ERROR:\n{ex}\n\nStack:\n{ex.StackTrace}");
                }
                catch { }
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
                        0 => ElementTheme.Default,
                        1 => ElementTheme.Light,
                        2 => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                    System.Diagnostics.Debug.WriteLine($"[THEME] Applied theme mode: {themeMode}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[THEME] Could not apply theme - MainWindow or Content is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[THEME] Failed to apply theme: {ex.Message}");
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine("[APP] Window closed - cleaning up");
            CleanupTempFiles();
        }

        private void CleanupTempFiles()
        {
            try
            {
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                if (System.IO.File.Exists(tempPath))
                {
                    try
                    {
                        System.IO.File.Delete(tempPath);
                        System.Diagnostics.Debug.WriteLine("[APP] Temp file deleted");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[APP] Failed to delete temp file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] Failed to cleanup temp files: {ex.Message}");
            }
        }

        private void ClearTemporaryFile()
        {
            try
            {
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                try
                {
                    System.IO.File.WriteAllText(tempPath, string.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[APP] Failed to write to temp file: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] Failed to clear temporary file: {ex.Message}");
            }
        }
    }
}
