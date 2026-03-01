using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI
{
    public partial class App : Application
    {
        private Window? _window;
        public static MainViewModel? SharedViewModel { get; private set; }

        public App()
        {
            InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            SharedViewModel = new MainViewModel();
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
