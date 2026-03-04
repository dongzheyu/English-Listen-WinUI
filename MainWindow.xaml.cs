using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace English_Listen_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Navigate(typeof(Views.HomePage));
        }
    }
}
