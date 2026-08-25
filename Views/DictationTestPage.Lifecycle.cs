using Microsoft.UI.Xaml.Navigation;

namespace English_Listen_WinUI.Views
{
    public sealed partial class DictationTestPage
    {
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Dispose();
            base.OnNavigatedFrom(e);
        }
    }
}