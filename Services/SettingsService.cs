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
        private const int MaxGroupCount = 1000;
        private const int MaxWordsPerGroup = 1000;
        private const int MaxGroupItemLength = 128;
        private const int MaxUserCount = 1000;

        private readonly string AppDataPath;
        private readonly string ConfigPath;
        private readonly string UserDataPath;
        private readonly string SettingsFilePath;
        private readonly string WordlistGroupsFilePath;
        private readonly SemaphoreSlim _fileLock = new(1, 1);

        private AppSettings _settings = new();
        private string? _currentPassword;

        public AppSettings Settings => _settings;
        public string? CurrentPassword
        {
            get => _currentPassword;
            set => _currentPassword = value;
        }

        public SettingsService()
        {
            try
            {
                AppDataPath = ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                AppDataPath = AppContext.BaseDirectory;
            }

            ConfigPath = Path.Combine(AppDataPath, "config");
            UserDataPath = Path.Combine(ConfigPath, "users");
            SettingsFilePath = Path.Combine(ConfigPath, "settings.json");
            WordlistGroupsFilePath = Path.Combine(ConfigPath, "wordlist_groups.ini");
            EnsureDirectoryExists();
        }

        public async Task InitializeAsync()
        {
            await LoadSettingsAsync();
            await MigrateOldDataAsync();
        }

        private void EnsureDirectoryExists()
        {
            Directory.CreateDirectory(ConfigPath);
            if (IsReparsePoint(ConfigPath))
                throw new IOException("拒绝使用重解析点配置目录。");

            Directory.CreateDirectory(UserDataPath);
            if (IsReparsePoint(UserDataPath))
                throw new IOException("拒绝使用重解析点用户目录。");

            var wordlistPath = Path.Combine(AppDataPath, "wordlist");
            Directory.CreateDirectory(wordlistPath);
            if (IsReparsePoint(wordlistPath))
                throw new IOException("拒绝使用重解析点词库目录。");
        }

        private static bool IsSafeUsername(string? username)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length > MaxUsernameLength)
                return false;

            if (username == "." || username == ".." || username.EndsWith(' ') || username.EndsWith('.'))
                return false;

            if (username.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            if (username.Any(char.IsControl))
                return false;

            var baseName = username.TrimEnd('.', ' ').ToUpperInvariant();
            return baseName is not ("CON" or "PRN" or "AUX" or "NUL") &&
                   !baseName.StartsWith("COM", StringComparison.Ordinal) &&
                   !baseName.StartsWith("LPT", StringComparison.Ordinal);
        }

        private static void EnsureUsername(string username)
        {
            if (!IsSafeUsername(username))
                throw new ArgumentException("用户名包含非法路径字符或长度超限。", nameof(username));
        }

        private static async Task<string?> ReadJsonFileAsync(string path)
        {
            if (!File.Exists(path) || IsReparsePoint(path))
                return null;

            var info = new FileInfo(path);
            if (info.Length > MaxJsonFileBytes)
                throw new InvalidDataException($"数据文件过大: {path}");

            return await File.ReadAllTextAsync(path);
        }

        private string WordlistRoot => Path.GetFullPath(Path.Combine(AppDataPath, "wordlist"));

        private static bool IsSafeWordlistFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 128)
                return false;

            if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return false;

            if (fileName == "." || fileName == ".." ||
                fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fileName.Any(char.IsControl))
                return false;

            return string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal);
        }

        private string GetValidatedWordlistPath(string fileName)
        {
            if (!IsSafeWordlistFileName(fileName))
                throw new ArgumentException("词库文件名非法。", nameof(fileName));

            var root = WordlistRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (IsReparsePoint(WordlistRoot))
                throw new IOException("拒绝使用重解析点词库目录。");

            var fullPath = Path.GetFullPath(Path.Combine(root, fileName));
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("词库路径越界。", nameof(fileName));

            return fullPath;
        }

        private static bool IsReparsePoint(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    return false;

                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
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
                var json = await ReadJsonFileAsync(SettingsFilePath);
                _settings = string.IsNullOrWhiteSpace(json)
                    ? new AppSettings()
                    : JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                _settings = new AppSettings();
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
                if (IsReparsePoint(SettingsFilePath))
                    throw new IOException("拒绝覆盖重解析点设置文件。");

                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = SettingsFilePath + $".{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, SettingsFilePath, true);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<WordListGroup>> LoadWordlistGroupsAsync()
        {
            var groups = new List<WordListGroup>();
            try
            {
                if (!File.Exists(WordlistGroupsFilePath) || IsReparsePoint(WordlistGroupsFilePath))
                    return groups;

                var info = new FileInfo(WordlistGroupsFilePath);
                if (info.Length > MaxWordlistGroupsFileBytes)
                    return groups;

                using var reader = new StreamReader(WordlistGroupsFilePath);
                WordListGroup? current = null;
                var wordsInCurrentGroup = 0;

                while (!reader.EndOfStream && groups.Count < MaxGroupCount)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var trimmed = line.Trim();
                    if (trimmed.Length > MaxGroupItemLength)
                        continue;

                    if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                    {
                        if (current != null)
                            groups.Add(current);

                        current = new WordListGroup { Name = trimmed[1..^1] };
                        wordsInCurrentGroup = 0;
                    }
                    else if (current != null && wordsInCurrentGroup < MaxWordsPerGroup && IsSafeWordlistFileName(trimmed))
                    {
                        current.WordListNames.Add(trimmed);
                        wordsInCurrentGroup++;
                    }
                }

                if (current != null && groups.Count < MaxGroupCount)
                    groups.Add(current);
            }
            catch
            {
            }

            return groups;
        }

        public async Task SaveWordlistGroupsAsync(List<WordListGroup> groups)
        {
            if (groups == null)
                throw new ArgumentNullException(nameof(groups));

            await _fileLock.WaitAsync();
            try
            {
                if (IsReparsePoint(WordlistGroupsFilePath))
                    throw new IOException("拒绝覆盖重解析点分组文件。");

                var lines = new List<string>();
                foreach (var group in groups.Take(MaxGroupCount))
                {
                    var groupName = new string((group.Name ?? string.Empty)
                        .Where(c => c != '\r' && c != '\n' && c != '[' && c != ']' && !char.IsControl(c))
                        .Take(MaxGroupItemLength)
                        .ToArray());
                    lines.Add($"[{groupName}]");

                    foreach (var name in group.WordListNames.Take(MaxWordsPerGroup))
                    {
                        var safeName = name?.Trim() ?? string.Empty;
                        if (IsSafeWordlistFileName(safeName))
                            lines.Add(safeName);
                    }

                    lines.Add(string.Empty);
                }

                var content = string.Join(Environment.NewLine, lines);
                if (Encoding.UTF8.GetByteCount(content) > MaxWordlistGroupsFileBytes)
                    throw new InvalidDataException("词库分组数据过大。");

                var tempPath = WordlistGroupsFilePath + $".{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8);
                File.Move(tempPath, WordlistGroupsFilePath, true);
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
                var json = await ReadJsonFileAsync(GetUserTestHistoryPath(username));
                if (!string.IsNullOrWhiteSpace(json))
                    history = JsonSerializer.Deserialize<List<TestResult>>(json) ?? new List<TestResult>();
            }
            catch
            {
            }

            return history;
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

            await _fileLock.WaitAsync();
            try
            {
                var userDir = GetUserDataPath(username);
                Directory.CreateDirectory(userDir);
                if (IsReparsePoint(userDir) || IsReparsePoint(GetUserTestHistoryPath(username)))
                    throw new IOException("拒绝使用重解析点用户数据路径。");

                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                if (Encoding.UTF8.GetByteCount(json) > MaxJsonFileBytes)
                    throw new InvalidDataException("测试历史数据过大。");

                var tempPath = GetUserTestHistoryPath(username) + $".{Guid.NewGuid():N}.tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, GetUserTestHistoryPath(username), true);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> SaveTestHistoryForCurrentUserAsync(string currentUser, string targetUser, List<TestResult> history)
        {
            if (!VerifyOwnership(currentUser, targetUser, "保存测试历史"))
                return false;

            await SaveTestHistoryAsync(targetUser, history);
            return true;
        }

        public async Task<List<string>> LoadWordsFromFileAsync(string filePath)
        {
            try
            {
                var root = WordlistRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (IsReparsePoint(WordlistRoot))
                    return new List<string>();

                var fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || IsReparsePoint(fullPath))
                    return new List<string>();

                if (!File.Exists(fullPath))
                    return new List<string>();

                var info = new FileInfo(fullPath);
                if (info.Length > MaxWordlistFileBytes)
                    return new List<string>();

                return (await File.ReadAllLinesAsync(fullPath))
                    .Select(line => line.Trim())
                    .Where(line => line.Length > 0 && line.Length <= MaxWordLength)
                    .Take(MaxWordCount)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public async Task SaveWordsToFileAsync(string filePath, List<string> words)
        {
            if (words == null)
                throw new ArgumentNullException(nameof(words));

            var root = WordlistRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (IsReparsePoint(WordlistRoot))
                throw new IOException("拒绝使用重解析点词库目录。");

            var fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("词库路径越界。", nameof(filePath));

            var normalizedWords = words
                .Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word.Trim())
                .Where(word => word.Length <= MaxWordLength)
                .Take(MaxWordCount)
                .ToList();

            await _fileLock.WaitAsync();
            try
            {
                var directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory))
                    throw new ArgumentException("词库目录无效。", nameof(filePath));

                Directory.CreateDirectory(directory);
                if (IsReparsePoint(directory) || IsReparsePoint(fullPath))
                    throw new IOException("拒绝操作重解析点路径。");

                var tempPath = fullPath + $".{Guid.NewGuid():N}.tmp";
                await File.WriteAllLinesAsync(tempPath, normalizedWords);
                File.Move(tempPath, fullPath, true);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<List<string>> GetWordlistFilesAsync()
        {
            try
            {
                var wordlistDir = GetWordlistDirectory();
                if (!Directory.Exists(wordlistDir) || IsReparsePoint(wordlistDir))
                    return new List<string>();

                return Directory.GetFiles(wordlistDir, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(path => !IsReparsePoint(path))
                    .Where(path => IsSafeWordlistFileName(Path.GetFileName(path)))
                    .Take(1000)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public string GetWordlistDirectory() => WordlistRoot;
        public string GetWordlistFilePath(string fileName) => GetValidatedWordlistPath(fileName);

        public string GetUserDataPath(string username)
        {
            EnsureUsername(username);
            return Path.Combine(UserDataPath, username);
        }

        public string GetUserSettingsPath(string username) => Path.Combine(GetUserDataPath(username), "settings.json");
        public string GetUserTestHistoryPath(string username) => Path.Combine(GetUserDataPath(username), "test_history.json");

        public async Task<List<UserData>> LoadUsersAsync()
        {
            var users = new List<UserData>();
            try
            {
                if (!Directory.Exists(UserDataPath) || IsReparsePoint(UserDataPath))
                    return users;

                foreach (var userDir in Directory.GetDirectories(UserDataPath).Take(MaxUserCount))
                {
                    if (IsReparsePoint(userDir))
                        continue;

                    var username = Path.GetFileName(userDir);
                    if (!IsSafeUsername(username))
                        continue;

                    var json = await ReadJsonFileAsync(GetUserSettingsPath(username));
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var user = JsonSerializer.Deserialize<UserData>(json);
                        if (user != null && user.Username == username)
                            users.Add(user);
                    }
                }
            }
            catch
            {
            }

            return users;
        }

        public async Task SaveUsersAsync(List<UserData> users)
        {
            if (users == null)
                throw new ArgumentNullException(nameof(users));

            await _fileLock.WaitAsync();
            try
            {
                foreach (var user in users.Take(MaxUserCount))
                {
                    EnsureUsername(user.Username);
                    var userDir = GetUserDataPath(user.Username);
                    Directory.CreateDirectory(userDir);
                    if (IsReparsePoint(userDir) || IsReparsePoint(GetUserSettingsPath(user.Username)))
                        throw new IOException("拒绝使用重解析点用户数据路径。");

                    var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });
                    if (Encoding.UTF8.GetByteCount(json) > MaxJsonFileBytes)
                        throw new InvalidDataException("用户数据过大。");

                    var path = GetUserSettingsPath(user.Username);
                    var tempPath = path + $".{Guid.NewGuid():N}.tmp";
                    await File.WriteAllTextAsync(tempPath, json);
                    File.Move(tempPath, path, true);
                }
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public async Task<bool> CreateUserAsync(string username, string nickname, string password)
        {
            if (!IsSafeUsername(username))
                return false;

            var users = await LoadUsersAsync();
            if (users.Any(u => string.Equals(u.Username, username, StringComparison.Ordinal)))
                return false;

            users.Add(new UserData
            {
                Username = username,
                Nickname = nickname,
                PasswordHash = PasswordService.HashPassword(password),
                CreatedTime = DateTime.Now,
                LastLoginTime = DateTime.Now,
                IsActive = true
            });

            await SaveUsersAsync(users);
            return true;
        }

        private async Task<bool> DeleteUserAsync(string username)
        {
            if (!IsSafeUsername(username))
                return false;

            try
            {
                var users = await LoadUsersAsync();
                var user = users.FirstOrDefault(u => u.Username == username);
                if (user == null)
                    return false;

                var userDir = GetUserDataPath(username);
                if (IsReparsePoint(userDir))
                    return false;

                users.Remove(user);
                await SaveUsersAsync(users);

                if (Directory.Exists(userDir))
                    Directory.Delete(userDir, true);

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> DeleteUserForCurrentUserAsync(string currentUser, string targetUser)
        {
            if (!VerifyOwnership(currentUser, targetUser, "删除用户"))
                return false;

            return await DeleteUserAsync(targetUser);
        }

        private static bool VerifyOwnership(string? currentUser, string targetUser, string operation)
        {
            if (!IsSafeUsername(currentUser) || !IsSafeUsername(targetUser))
                return false;

            return string.Equals(currentUser, targetUser, StringComparison.Ordinal);
        }

        public async Task<bool> VerifyUserPasswordAsync(string username, string password)
        {
            if (!IsSafeUsername(username))
                return false;

            var user = (await LoadUsersAsync()).FirstOrDefault(u => u.Username == username);
            return user != null && user.IsActive && PasswordService.VerifyPassword(password, user.PasswordHash);
        }

        public async Task MigrateOldDataAsync()
        {
            try
            {
                var oldSettingsPath = Path.Combine(AppDataPath, "settings.json");
                if (File.Exists(oldSettingsPath) && !File.Exists(SettingsFilePath))
                    File.Copy(oldSettingsPath, SettingsFilePath);

                var oldGroupsPath = Path.Combine(AppDataPath, "wordlist_groups.ini");
                if (File.Exists(oldGroupsPath) && !File.Exists(WordlistGroupsFilePath))
                    File.Copy(oldGroupsPath, WordlistGroupsFilePath);

                var oldUsersPath = Path.Combine(AppDataPath, "users.json");
                if (!File.Exists(oldUsersPath))
                    return;

                var usersJson = await ReadJsonFileAsync(oldUsersPath);
                var oldUsers = string.IsNullOrWhiteSpace(usersJson)
                    ? null
                    : JsonSerializer.Deserialize<List<UserData>>(usersJson);

                if (oldUsers == null || oldUsers.Count == 0)
                    return;

                var safeUsers = oldUsers
                    .Where(user => IsSafeUsername(user.Username))
                    .Take(MaxUserCount)
                    .ToList();
                await SaveUsersAsync(safeUsers);

                var oldHistoryPath = Path.Combine(AppDataPath, "test_history.json");
                if (File.Exists(oldHistoryPath) && safeUsers.Count > 0)
                {
                    var historyJson = await ReadJsonFileAsync(oldHistoryPath);
                    var oldHistory = string.IsNullOrWhiteSpace(historyJson)
                        ? null
                        : JsonSerializer.Deserialize<List<TestResult>>(historyJson);
                    if (oldHistory != null)
                        await SaveTestHistoryAsync(safeUsers[0].Username, oldHistory);
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
        }
    }
}