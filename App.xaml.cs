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
            Debug.WriteLine("[STARTUP] 1. OnLaunched called");

            try
            {
                // Set environment variable for single file publishing
                Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",
                    AppContext.BaseDirectory);
                Debug.WriteLine("[STARTUP] 2. Environment variable set");

                // Reset temporary file on startup
                ClearTemporaryFile();
                Debug.WriteLine("[STARTUP] 3. Temporary file cleared");

                SharedViewModel = new MainViewModel();
                Debug.WriteLine("[STARTUP] 4. ViewModel created");

                await SharedViewModel.InitializeAsync();
                Debug.WriteLine("[STARTUP] 5. ViewModel initialized");

                // Create window
                _window = new MainWindow();
                Debug.WriteLine("[STARTUP] 6. Window created");

                _window.Closed += OnWindowClosed;
                Debug.WriteLine("[STARTUP] 7. Closed handler attached");

                ApplyTheme(SharedViewModel.ThemeMode);
                Debug.WriteLine("[STARTUP] 8. Theme applied");

                // CRITICAL: Activate the window to bring it to foreground
                // Activate() is the correct WinUI3 method to show and focus the window
                _window.Activate();

                Debug.WriteLine("[STARTUP] 9. Window activated - should now be visible");

                Debug.WriteLine("[STARTUP] 10. COMPLETE - Window activation attempted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STARTUP] FATAL ERROR: {ex}");
                Debug.WriteLine($"[STARTUP] Stack trace: {ex.StackTrace}");

                // Write to a log file as last resort
                try
                {
                    var logPath = Path.Combine(AppContext.BaseDirectory, "startup_error.log");
                    File.WriteAllText(logPath, $"[{DateTime.Now}] FATAL ERROR:\n{ex}\n\nStack:\n{ex.StackTrace}");
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
                        0 => ElementTheme.Default,
                        1 => ElementTheme.Light,
                        2 => ElementTheme.Dark,
                        _ => ElementTheme.Default
                    };
                    Debug.WriteLine($"[THEME] Applied theme mode: {themeMode}");
                }
                else
                {
                    Debug.WriteLine($"[THEME] Could not apply theme - MainWindow or Content is null");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[THEME] Failed to apply theme: {ex.Message}");
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            Debug.WriteLine("[APP] Window closed - cleaning up");
            CleanupTempFiles();
        }

        private async void CleanupTempFiles()
        {
            try
            {
                await TempFileHelper.ClearAsync();
                Debug.WriteLine("[APP] Temp file deleted");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to cleanup temp files: {ex.Message}");
            }
        }

        private async void ClearTemporaryFile()
        {
            try
            {
                await TempFileHelper.ClearAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[APP] Failed to clear temporary file: {ex.Message}");
            }
        }
    }
}