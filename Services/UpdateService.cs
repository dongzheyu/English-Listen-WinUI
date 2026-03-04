using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace English_Listen_WinUI.Services
{
    public class UpdateService
    {
        private const string UpdateUrl = "https://gitee.com/jetcpp/english_-listen/raw/master/update.txt";
        private const string CurrentVersion = "2.7.0";

        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "English-Listen-Updater/1.0");
        }

        public async Task CheckForUpdatesAsync(ContentDialog dialog)
        {
            try
            {
                var response = await _httpClient.GetStringAsync(UpdateUrl);
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length >= 2)
                {
                    var remoteVersion = lines[0].Trim();
                    var downloadUrl = lines[1].Trim();

                    int versionComparison = CompareVersions(CurrentVersion, remoteVersion);
                    
                    if (versionComparison < 0) // Remote version is higher
                    {
                        // New version available
                        var updateDialog = new ContentDialog
                        {
                            Title = "发现新版本",
                            Content = $"发现新版本 {remoteVersion}，当前版本为 {CurrentVersion}。\n\n是否现在下载更新？",
                            PrimaryButtonText = "是",
                            CloseButtonText = "否",
                            XamlRoot = dialog.XamlRoot
                        };

                        var result = await updateDialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            await DownloadUpdateAsync(downloadUrl);
                        }
                    }
                    else if (versionComparison > 0) // Current version is higher (development version)
                    {
                        // Using development version
                        var devDialog = new ContentDialog
                        {
                            Title = "开发版本",
                            Content = $"您正在使用开发版本 {CurrentVersion}，比最新稳定版 {remoteVersion} 更高。\n\n开发版本可能不稳定，请谨慎使用。",
                            CloseButtonText = "确定",
                            XamlRoot = dialog.XamlRoot
                        };
                        await devDialog.ShowAsync();
                    }
                    else
                    {
                        // Already up to date
                        var infoDialog = new ContentDialog
                        {
                            Title = "检查更新",
                            Content = "当前已是最新版本",
                            CloseButtonText = "确定",
                            XamlRoot = dialog.XamlRoot
                        };
                        await infoDialog.ShowAsync();
                    }
                }
                else
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "检查更新失败",
                        Content = $"服务器返回的数据格式不正确\n收到的数据:\n{response}",
                        CloseButtonText = "确定",
                        XamlRoot = dialog.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "检查更新失败",
                    Content = $"无法连接到更新服务器: {ex.Message}",
                    CloseButtonText = "确定",
                    XamlRoot = dialog.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private async Task DownloadUpdateAsync(string downloadUrl)
        {
            try
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);

                if (response.IsSuccessStatusCode)
                {
                    var tempPath = Windows.Storage.ApplicationData.Current.TemporaryFolder.Path;
                    var fileName = $"EnglishListen_Update_{DateTime.Now:yyyyMMdd_HHmmss}.exe";
                    var filePath = System.IO.Path.Combine(tempPath, fileName);

                    using (var fileStream = System.IO.File.Create(filePath))
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                        var canReportProgress = totalBytes != -1L;
                        var totalBytesRead = 0L;
                        var buffer = new byte[8192];
                        var isMoreToRead = true;

                        do
                        {
                            var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                            if (bytesRead == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                if (canReportProgress)
                                {
                                    // Progress reporting could be added here if needed
                                }
                            }
                        }
                        while (isMoreToRead);
                    }

                    var successDialog = new ContentDialog
                    {
                        Title = "下载完成",
                        Content = "更新文件已下载完成，即将启动安装程序。",
                        CloseButtonText = "确定"
                    };

                    // Launch the installer
                    await Windows.System.Launcher.LaunchFileAsync(
                        await Windows.Storage.StorageFile.GetFileFromPathAsync(filePath));

                    // Close current application
                    var app = Microsoft.UI.Xaml.Application.Current;
                    if (app is App winuiApp)
                    {
                        winuiApp.Exit();
                    }
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "下载失败",
                    Content = $"下载更新文件失败: {ex.Message}",
                    CloseButtonText = "确定"
                };
                await errorDialog.ShowAsync();
            }
        }

        private int CompareVersions(string currentVersion, string remoteVersion)
        {
            if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(remoteVersion))
                return 0;

            var currentParts = currentVersion.Split('.');
            var remoteParts = remoteVersion.Split('.');

            // Ensure both have 3 parts
            while (currentParts.Length < 3)
                currentParts = AddElement(currentParts, "0");
            while (remoteParts.Length < 3)
                remoteParts = AddElement(remoteParts, "0");

            for (int i = 0; i < 3; i++)
            {
                int currentNum = int.TryParse(currentParts[i], out var c) ? c : 0;
                int remoteNum = int.TryParse(remoteParts[i], out var r) ? r : 0;

                if (remoteNum > currentNum)
                    return -1; // Remote version is higher
                else if (remoteNum < currentNum)
                    return 1; // Current version is higher
            }

            return 0; // Versions are equal
        }

        private string[] AddElement(string[] array, string element)
        {
            var newArray = new string[array.Length + 1];
            Array.Copy(array, newArray, array.Length);
            newArray[array.Length] = element;
            return newArray;
        }
    }
}