using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

namespace English_Listen_WinUI.Services
{
    /// <summary>
    /// 应用更新检查服务 - 从QT6版本完整移植
    /// 功能：检查更新、版本比较、下载更新
    /// </summary>
    public class UpdateService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string UPDATE_URL = "https://gitee.com/jetcpp/english_-listen/raw/master/update.txt";
        private const string USER_AGENT = "English-Listen-Updater/1.0";
        private const string CURRENT_VERSION = "2.7.0"; // WinUI3版本号

        static UpdateService()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", USER_AGENT);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// 检查是否有可用更新（完整实现）
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始检查更新...");
                
                var response = await _httpClient.GetAsync(UPDATE_URL);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"更新检查失败: HTTP {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length < 2)
                {
                    System.Diagnostics.Debug.WriteLine("更新文件格式错误");
                    return null;
                }

                var remoteVersion = lines[0].Trim();
                var downloadUrl = lines[1].Trim();

                System.Diagnostics.Debug.WriteLine($"远程版本: {remoteVersion}, 下载链接: {downloadUrl}");

                // 获取发布说明
                var releaseNotes = await GetReleaseNotesAsync();

                // 检查是否需要更新
                var comparisonResult = CompareVersions(CURRENT_VERSION, remoteVersion);
                
                if (comparisonResult < 0) // 远程版本更高
                {
                    return new UpdateInfo
                    {
                        CurrentVersion = CURRENT_VERSION,
                        NewVersion = remoteVersion,
                        DownloadUrl = downloadUrl,
                        ReleaseNotes = releaseNotes,
                        IsUpdateAvailable = true
                    };
                }
                else if (comparisonResult > 0) // 当前版本更高（开发版本）
                {
                    return new UpdateInfo
                    {
                        CurrentVersion = CURRENT_VERSION,
                        NewVersion = remoteVersion,
                        ReleaseNotes = releaseNotes,
                        IsDevelopmentVersion = true
                    };
                }

                System.Diagnostics.Debug.WriteLine("当前已是最新版本");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新检查异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取更新检查的简单结果描述
        /// </summary>
        public string GetUpdateCheckResult(UpdateInfo? updateInfo)
        {
            if (updateInfo == null)
                return "当前已是最新版本";
            
            if (updateInfo.IsDevelopmentVersion)
                return "您正在使用开发版本";
            
            if (updateInfo.IsUpdateAvailable)
                return $"发现新版本: {updateInfo.NewVersion}";
            
            return "检查完成";
        }

        /// <summary>
        /// 下载更新文件（带进度报告）
        /// </summary>
        public async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<int> progress)
        {
            try
            {
                var tempFolder = Path.GetTempPath();
                var fileName = $"EnglishListen_Update_{DateTime.Now:yyyyMMdd_HHmmss}.exe";
                var filePath = Path.Combine(tempFolder, fileName);
                
                System.Diagnostics.Debug.WriteLine($"开始下载更新: {fileName}");
                System.Diagnostics.Debug.WriteLine($"下载链接: {downloadUrl}");

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"下载失败: HTTP {response.StatusCode}");
                    return null;
                }

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                var buffer = new byte[8192];

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percentage = (int)((downloadedBytes * 100) / totalBytes);
                        progress.Report(percentage);
                    }
                }

                await fileStream.FlushAsync();
                
                System.Diagnostics.Debug.WriteLine($"更新下载完成: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"下载更新异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 启动安装程序
        /// </summary>
        public async Task<bool> LaunchInstallerAsync(string installerPath)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"启动安装程序: {installerPath}");

                if (!File.Exists(installerPath))
                {
                    System.Diagnostics.Debug.WriteLine("安装程序不存在");
                    return false;
                }

                // 启动安装程序
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = installerPath,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };

                process.Start();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"启动安装程序异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取发布说明
        /// </summary>
        private async Task<string> GetReleaseNotesAsync()
        {
            try
            {
                // 尝试获取update.md文件作为发布说明
                var mdUrl = UPDATE_URL.Replace("update.txt", "update.md");
                var response = await _httpClient.GetAsync(mdUrl);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // 限制长度，避免对话框过大
                    if (content.Length > 1000)
                    {
                        content = content.Substring(0, 1000) + "\n\n...（更多内容请查看完整更新日志）";
                    }
                    return content;
                }
                
                return "暂无详细更新说明";
            }
            catch
            {
                return "暂无详细更新说明";
            }
        }

        /// <summary>
        /// 版本比较（从QT6版本移植）
        /// </summary>
        private int CompareVersions(string currentVersion, string remoteVersion)
        {
            try
            {
                // 移除可能的v前缀
                currentVersion = currentVersion.Trim().TrimStart('v', 'V');
                remoteVersion = remoteVersion.Trim().TrimStart('v', 'V');

                var currentParts = currentVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var remoteParts = remoteVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);

                // 确保三部分版本号
                while (currentParts.Length < 3) currentParts = currentParts.Append("0").ToArray();
                while (remoteParts.Length < 3) remoteParts = remoteParts.Append("0").ToArray();

                // 逐部分比较
                for (int i = 0; i < 3; i++)
                {
                    if (!int.TryParse(currentParts[i], out int currentNum) || 
                        !int.TryParse(remoteParts[i], out int remoteNum))
                    {
                        continue; // 如果解析失败，跳过这部分比较
                    }

                    if (remoteNum > currentNum) return -1;  // 远程版本更高
                    if (remoteNum < currentNum) return 1;   // 当前版本更高
                }

                return 0; // 版本相同
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"版本比较异常: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 获取更新描述文本
        /// </summary>
        public string GetUpdateDescription(UpdateInfo updateInfo)
        {
            return $"发现新版本 {updateInfo.NewVersion} 🎉\n\n" +
                   $"当前版本: {updateInfo.CurrentVersion}\n" +
                   $"新版本: {updateInfo.NewVersion}\n\n" +
                   $"更新说明:\n{updateInfo.ReleaseNotes}\n\n" +
                   $"是否立即下载并安装更新？";
        }

        /// <summary>
        /// 获取当前版本号
        /// </summary>
        public string GetCurrentVersion()
        {
            return CURRENT_VERSION;
        }
    }

    /// <summary>
    /// 更新信息模型（增强版）
    /// </summary>
    public class UpdateInfo
    {
        public string CurrentVersion { get; set; } = string.Empty;
        public string NewVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public bool IsUpdateAvailable { get; set; }
        public bool IsDevelopmentVersion { get; set; }
    }
}