using Windows.ApplicationModel;
using Microsoft.UI.Xaml.Controls;

namespace English_Listen_WinUI.Views
{
    public sealed partial class AboutPage : Page
    {
        public AboutPage()
        {
            this.InitializeComponent();
            Loaded += (_, _) =>
            {
                var v = Package.Current.Id.Version;
                VersionTextBlock.Text = $"English Listen v{v.Major}.{v.Minor}.{v.Build}";
            };
        }
    }
}