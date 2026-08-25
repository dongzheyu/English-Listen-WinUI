using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using English_Listen_WinUI.Models;
using Windows.Storage;

namespace English_Listen_WinUI.Services
{
    public class SettingsService : IDisposable
    {
        private const int MaxUsernameLength = 64;
        private const long MaxJsonFileBytes = 10 * 1024 * 1024;
        private const long MaxWordlistFileBytes = 2 * 1024 * 1024;
        private const long MaxWordlistGroupsFileBytes = 2 * 1024 * 1024;
        private const int MaxWordCount = 10000;
        private const int MaxWordLength = 256;
        private const int MaxHistoryCount = 1000;
        private readonly SemaphoreSlim _fileLock = new(1, 1);
        private readonly string _localFolderPath;
        private readonly string _settingsFilePath;
        private readonly string _usersFilePath;
        private readonly string _wordlistDirectory;
        private readonly string _wordlistGroupsFilePath;
        private bool _disposed;
        private bool _isSaving;
        private UserSettings _settings = new();

        public SettingsService()
        {
            _localFolderPath = GetLocalFolderPath();
            _settingsFilePath = Path.Combine(_localFolderPath, "settings.json");
            _usersFilePath = Path.Combine(_localFolderPath, "users.json");
            _wordlistDirectory = Path.Combine(_localFolderPath, "wordlist");
            _wordlistGroupsFilePath = Path.Combine(_localFolderPath, "wordlist_groups.json");
        }

        public UserSettings Settings => _settings;
        public string GetWordlistDirectory() => _wordlistDirectory;

        private static string GetLocalFolderPath()
        {
            try
            {
                return ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, "data");
            }
        }

        private static bool IsSafeUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
                return false;

            return username.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
                   username != "." && username != ".." &&
                   !username.Any(char.IsControl);
        }

        private static bool IsSafeWordlistFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 128)
                return false;
            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return false;
            if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;
            return Path.GetFileName(fileName) == fileName && !fileName.Equals(".", StringComparison.Ordinal) && !fileName.Equals("..", StringComparison.Ordinal);
        }

        private string GetUserTestHistoryPath(string username)
        {
            EnsureUsername(username);
            return Path.Combine(_localFolderPath, $"history_{username}.json");
        }

        private static void EnsureUsername(string username)
        {
            if (!IsSafeUsername(username))
                throw new ArgumentException("用户名无效。", nameof(username));
        }

        private static void EnsureSize(long length, long maxBytes, string description)
        {
            if (length > maxBytes)
                throw new InvalidDataException($"{description}过大。");
        }

        private string GetWordlistFilePathUnchecked(string fileName)
        {
            if (!IsSafeWordlistFileName(fileName))
                throw new ArgumentException("词库文件名无效。", nameof(fileName));

            var root = _wordlistDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (IsReparsePoint(_wordlistDirectory))
                throw new IOException("拒绝使用重解析点词库目录。");

            var fullPath = Path.GetFullPath(Path.Combine(root, fileName));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("词库路径越界。", nameof(fileName));

            if (File.Exists(fullPath) && IsReparsePoint(fullPath))
                throw new IOException("拒绝访问重解析点词库文件。");

            return fullPath;
        }

        public string GetWordlistFilePath(string fileName) => GetWordlistFilePathUnchecked(fileName);

        private static bool IsReparsePoint(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return false;

                return (File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0;
            }
            catch
            {
                return true;
            }
        }

        public async Task LoadSettingsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(_localFolderPath);
                if (File.Exists(_settingsFilePath) && !IsReparsePoint(_settingsFilePath))
                {
                    var info = new FileInfo(_settingsFilePath);
                    EnsureSize(info.Length, MaxJsonFileBytes, "设置文件");
                    var json = await File.ReadAllTextAsync(_settingsFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                        _settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                }
            }
            catch
            {
                _settings = new UserSettings();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveSettingsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(_localFolderPath);
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                EnsureSize(Encoding.UTF8.GetByteCount(json), MaxJsonFileBytes, "设置文件");
                await WriteAtomicAsync(_settingsFilePath, json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private static async Task WriteAtomicAsync(string path, string content)
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("无效的目标目录。");
            Directory.CreateDirectory(directory);
            if (IsReparsePoint(directory) || (File.Exists(path) && IsReparsePoint(path)))
                throw new IOException("拒绝写入重解析点路径。");

            var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8);
            File.Move(tempPath, path, true);
        }

        public async Task<List<string>> GetWordlistFilesAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(_wordlistDirectory);
                if (IsReparsePoint(_wordlistDirectory))
                    return new List<string>();

                return Directory.EnumerateFiles(_wordlistDirectory, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(path => !IsReparsePoint(path))
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name) && IsSafeWordlistFileName(name))
                    .Take(1000)
                    .ToList()!;
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<WordListGroup>> LoadWordlistGroupsAsync()
        {
            await _fileLock.WaitAsync();
            try
            {
                if (!File.Exists(_wordlistGroupsFilePath) || IsReparsePoint(_wordlistGroupsFilePath))
                    return new List<WordListGroup>();
                var info = new FileInfo(_wordlistGroupsFilePath);
                EnsureSize(info.Length, MaxWordlistGroupsFileBytes, "词库分组文件");
                var json = await File.ReadAllTextAsync(_wordlistGroupsFilePath);
                var groups = JsonSerializer.Deserialize<List<WordListGroup>>(json) ?? new List<WordListGroup>();
                foreach (var group in groups)
                    group.WordListNames = group.WordListNames.Where(IsSafeWordlistFileName).Take(1000).ToList();
                return groups.Take(100).ToList();
            }
            catch
            {
                return new List<WordListGroup>();
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task SaveWordlistGroupsAsync(List<WordListGroup> groups)
        {
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            var safeGroups = groups.Take(100).Select(group =>
            {
                group.WordListNames = group.WordListNames.Where(IsSafeWordlistFileName).Take(1000).ToList();
                return group;
            }).ToList();

            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(safeGroups, new JsonSerializerOptions { WriteIndented = true });
                EnsureSize(Encoding.UTF8.GetByteCount(json), MaxWordlistGroupsFileBytes, "词库分组文件");
                await WriteAtomicAsync(_wordlistGroupsFilePath, json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<TestResult>> LoadTestHistoryAsync(string username)
        {
            var history = new List<TestResult>();
            if (!IsSafeUsername(username))
                return history;

            try
            {
                var path = GetUserTestHistoryPath(username);
                var json = await ReadJsonFileAsync(path);
                if (!string.IsNullOrWhiteSpace(json))
                    history = JsonSerializer.Deserialize<List<TestResult>>(json) ?? new List<TestResult>();
            }
            catch
            {
            }

            return history.Take(MaxHistoryCount).ToList();
        }

        private static async Task<string> ReadJsonFileAsync(string path)
        {
            if (!File.Exists(path) || IsReparsePoint(path))
                return string.Empty;
            var info = new FileInfo(path);
            EnsureSize(info.Length, MaxJsonFileBytes, "JSON 文件");
            return await File.ReadAllTextAsync(path);
        }

        public async Task<bool> LoadTestHistoryForCurrentUserAsync(string currentUser, string targetUser, Action<List<TestResult>> onSuccess)
        {
            if (!VerifyOwnership(currentUser, targetUser, "查看测试历史"))
                return false;

            onSuccess(await LoadTestHistoryAsync(targetUser));
            return true;
        }

        public async Task SaveTestHistoryAsync(string username, List<TestResult> history)
        {
            EnsureUsername(username);
            if (history == null)
                throw new ArgumentNullException(nameof(history));

            var safeHistory = history.Take(MaxHistoryCount).ToList();
            var json = JsonSerializer.Serialize(safeHistory, new JsonSerializerOptions { WriteIndented = true });
            EnsureSize(Encoding.UTF8.GetByteCount(json), MaxJsonFileBytes, "测试历史文件");

            await _fileLock.WaitAsync();
            try
            {
                await WriteAtomicAsync(GetUserTestHistoryPath(username), json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        private bool VerifyOwnership(string currentUser, string targetUser, string operation)
        {
            if (!IsSafeUsername(currentUser) || !IsSafeUsername(targetUser) || !string.Equals(currentUser, targetUser, StringComparison.Ordinal))
                return false;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
        }
    }
}