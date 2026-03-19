using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace English_Listen_WinUI.Converters
{
    public class FullScreenPaddingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isFullScreen && isFullScreen)
            {
                // Full-screen mode: minimal padding
                return new Thickness(20);
            }
            else
            {
                // Normal mode: normal padding
                return new Thickness(40);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}