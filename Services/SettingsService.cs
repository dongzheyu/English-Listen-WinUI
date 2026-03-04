using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using English_Listen_WinUI.Models;
using English_Listen_WinUI.Services;

namespace English_Listen_WinUI.Services
{
    public class SettingsService
    {
        private static readonly string AppDataPath = AppDomain.CurrentDomain.BaseDirectory;

        private static readonly string SettingsFilePath = Path.Combine(AppDataPath, "settings.json");
        private static readonly string WordlistGroupsFilePath = Path.Combine(AppDataPath, "wordlist_groups.ini");
        private static readonly string TestHistoryFilePath = Path.Combine(AppDataPath, "test_history.json");
        private static readonly string UsersFilePath = Path.Combine(AppDataPath, "users.json");

        private AppSettings _settings = new();

        public AppSettings Settings => _settings;

        public SettingsService()
        {
            EnsureDirectoryExists();
        }

        private void EnsureDirectoryExists()
        {
            // Ensure main directory exists
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
            
            var wordlistDir = Path.Combine(AppDataPath, "wordlist");
            if (!Directory.Exists(wordlistDir))
            {
                Directory.CreateDirectory(wordlistDir);
            }
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = await File.ReadAllTextAsync(SettingsFilePath);
                    // Check if file is encrypted
                    if (json.StartsWith("ENCRYPTED:"))
                    {
                        // This is a placeholder - actual decryption would require password
                        _settings = new AppSettings();
                    }
                    else
                    {
                        _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                }
            }
            catch
            {
                _settings = new AppSettings();
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(SettingsFilePath, json);
            }
            catch
            {
            }
        }

        public async Task<string> LoadEncryptedSettingsAsync(string password)
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var encryptedData = await File.ReadAllTextAsync(SettingsFilePath);
                    if (encryptedData.StartsWith("ENCRYPTED:"))
                    {
                        var actualData = encryptedData.Substring(10);
                        var decryptedJson = await EncryptionService.DecryptAsync(actualData, password);
                        return decryptedJson;
                    }
                    else
                    {
                        // File is not encrypted, return as-is
                        return encryptedData;
                    }
                }
            }
            catch
            {
            }
            return "{}";
        }

        public async Task SaveEncryptedSettingsAsync(string jsonData, string password)
        {
            try
            {
                var encryptedData = await EncryptionService.EncryptAsync(jsonData, password);
                await File.WriteAllTextAsync(SettingsFilePath, $"ENCRYPTED:{encryptedData}");
            }
            catch
            {
            }
        }

        public async Task<List<WordListGroup>> LoadWordlistGroupsAsync()
        {
            var groups = new List<WordListGroup>();
            try
            {
                if (File.Exists(WordlistGroupsFilePath))
                {
                    var lines = await File.ReadAllLinesAsync(WordlistGroupsFilePath);
                    WordListGroup? currentGroup = null;

                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed)) continue;

                        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        {
                            if (currentGroup != null)
                            {
                                groups.Add(currentGroup);
                            }
                            currentGroup = new WordListGroup
                            {
                                Name = trimmed.Trim('[', ']')
                            };
                        }
                        else if (currentGroup != null)
                        {
                            currentGroup.WordListNames.Add(trimmed);
                        }
                    }

                    if (currentGroup != null)
                    {
                        groups.Add(currentGroup);
                    }
                }
            }
            catch { }
            return groups;
        }

        public async Task SaveWordlistGroupsAsync(List<WordListGroup> groups)
        {
            try
            {
                var lines = new List<string>();
                foreach (var group in groups)
                {
                    lines.Add($"[{group.Name}]");
                    foreach (var name in group.WordListNames)
                    {
                        lines.Add(name);
                    }
                    lines.Add("");
                }
                await File.WriteAllLinesAsync(WordlistGroupsFilePath, lines);
            }
            catch { }
        }

        public async Task<List<TestResult>> LoadTestHistoryAsync()
        {
            var history = new List<TestResult>();
            try
            {
                if (File.Exists(TestHistoryFilePath))
                {
                    var json = await File.ReadAllTextAsync(TestHistoryFilePath);
                    history = JsonSerializer.Deserialize<List<TestResult>>(json) ?? new List<TestResult>();
                }
            }
            catch { }
            return history;
        }

        public async Task SaveTestHistoryAsync(List<TestResult> history)
        {
            try
            {
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(TestHistoryFilePath, json);
            }
            catch { }
        }

        public async Task<List<string>> LoadWordsFromFileAsync(string filePath)
        {
            var words = new List<string>();
            try
            {
                if (File.Exists(filePath))
                {
                    var lines = await File.ReadAllLinesAsync(filePath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            words.Add(trimmed);
                        }
                    }
                }
            }
            catch { }
            return words;
        }

        public async Task SaveWordsToFileAsync(string filePath, List<string> words)
        {
            try
            {
                await File.WriteAllLinesAsync(filePath, words);
            }
            catch { }
        }

        public async Task<List<string>> GetWordlistFilesAsync()
        {
            var files = new List<string>();
            try
            {
                var wordlistDir = Path.Combine(AppDataPath, "wordlist");
                if (Directory.Exists(wordlistDir))
                {
                    var txtFiles = Directory.GetFiles(wordlistDir, "*.txt");
                    files.AddRange(txtFiles);
                }
            }
            catch { }
            return files;
        }

        public string GetWordlistDirectory()
        {
            return Path.Combine(AppDataPath, "wordlist");
        }

        public async Task<List<UserData>> LoadUsersAsync()
        {
            var users = new List<UserData>();
            try
            {
                if (File.Exists(UsersFilePath))
                {
                    var json = await File.ReadAllTextAsync(UsersFilePath);
                    users = JsonSerializer.Deserialize<List<UserData>>(json) ?? new List<UserData>();
                }
            }
            catch { }
            return users;
        }

        public async Task SaveUsersAsync(List<UserData> users)
        {
            try
            {
                var json = JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(UsersFilePath, json);
            }
            catch { }
        }

        public async Task<bool> CreateUserAsync(string username, string nickname, string password)
        {
            var users = await LoadUsersAsync();
            
            // Check if user already exists
            if (users.Any(u => u.Username == username))
            {
                return false;
            }

            var newUser = new UserData
            {
                Username = username,
                Nickname = nickname,
                CreatedTime = DateTime.Now,
                LastLoginTime = DateTime.Now,
                IsActive = true
            };

            // TODO: Hash password before storing
            users.Add(newUser);
            await SaveUsersAsync(users);
            return true;
        }
    }
}
