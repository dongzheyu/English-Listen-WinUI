namespace English_Listen_WinUI
{
    public sealed partial class MainWindow
    {
        internal static void CleanupStaticState()
        {
            _notificationTimer?.Stop();
            _notificationTimer?.Dispose();
            _notificationTimer = null;
            _currentInstance = null;
            _isToastHovered = false;
        }
    }
}