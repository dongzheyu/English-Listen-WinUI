using System;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace English_Listen_WinUI.Services
{
    public class NotificationService
    {
        public static async Task ShowToastAsync(string title, string message)
        {
            try
            {
                // Create toast XML content using native Windows API
                string toastXmlString = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{title}</text>
                            <text>{message}</text>
                        </binding>
                    </visual>
                </toast>";

                XmlDocument toastXml = new XmlDocument();
                toastXml.LoadXml(toastXmlString);
                var toast = new ToastNotification(toastXml);
                ToastNotificationManager.CreateToastNotifier().Show(toast);
            }
            catch
            {
                // Handle notification errors silently
            }
        }

        public static async Task ShowTestCompletedNotification(int correctCount, int totalCount)
        {
            var accuracy = (double)correctCount / totalCount * 100;
            await ShowToastAsync("测试完成", 
                $"正确率: {accuracy:F1}% ({correctCount}/{totalCount})");
        }

        public static async Task ShowUpdateAvailableNotification(string version)
        {
            await ShowToastAsync("更新可用", $"新版本 {version} 已发布！");
        }

        public static async Task ShowErrorNotification(string errorMessage)
        {
            await ShowToastAsync("错误", errorMessage);
        }
    }
}