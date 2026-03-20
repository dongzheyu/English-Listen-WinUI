using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using English_Listen_WinUI.ViewModels;

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
            // Set environment variable for single file publishing
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);

            // Reset temporary file on startup
            ClearTemporaryFile();

            SharedViewModel = new MainViewModel();
            await SharedViewModel.InitializeAsync();
            
            _window = new MainWindow();
            _window.Closed += OnWindowClosed;
            
            ApplyTheme(SharedViewModel.ThemeMode);
            
            _window.Activate();
        }

        public static void ApplyTheme(int themeMode)
        {
            if (MainWindow?.Content is FrameworkElement rootElement && rootElement.XamlRoot != null)
            {
                rootElement.RequestedTheme = themeMode switch
                {
                    0 => ElementTheme.Light,
                    1 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }

        private void OnWindowClosed(object sender, WindowEventArgs args)
        {
            // Clean up temporary files when application closes
            CleanupTempFiles();
        }

        private void CleanupTempFiles()
        {
            try
            {
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
            catch
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine("Failed to cleanup temp files");
            }
        }

        private void ClearTemporaryFile()
        {
            try
            {
                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "english_listen_temp.txt");
                System.IO.File.WriteAllText(tempPath, string.Empty);
            }
            catch
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine("Failed to clear temporary file");
            }
        }
    }
}