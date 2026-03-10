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
        private static readonly string ConfigPath = Path.Combine(AppDataPath, "config");
        private static readonly string UserDataPath = Path.Combine(ConfigPath, "users");

        private static readonly string SettingsFilePath = Path.Combine(ConfigPath, "settings.json");
        private static readonly string WordlistGroupsFilePath = Path.Combine(ConfigPath, "wordlist_groups.ini");


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
            EnsureDirectoryExists();
            _ = InitializeAsync(); // Start initialization in background
        }

        private async Task InitializeAsync()
        {
            await LoadSettingsAsync(); // Load settings first
            await MigrateOldDataAsync(); // Then migrate old data
        }

        private void EnsureDirectoryExists()
        {
            // Ensure main directory exists
            if (!Directory.Exists(AppDataPath))
            {
                Directory.CreateDirectory(AppDataPath);
            }
            
            // Ensure config directory exists
            if (!Directory.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigPath);
            }
            
            // Ensure user data directory exists
            if (!Directory.Exists(UserDataPath))
            {
                Directory.CreateDirectory(UserDataPath);
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
                    _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // Log error and create default settings
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
                throw;
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

        public async Task<List<TestResult>> LoadTestHistoryAsync(string username)
        {
            var history = new List<TestResult>();
            try
            {
                if (string.IsNullOrEmpty(username)) return history;
                
                var testHistoryPath = GetUserTestHistoryPath(username);
                if (File.Exists(testHistoryPath))
                {
                    var json = await File.ReadAllTextAsync(testHistoryPath);
                    history = JsonSerializer.Deserialize<List<TestResult>>(json) ?? new List<TestResult>();
                }
            }
            catch { }
            return history;
        }

        public async Task SaveTestHistoryAsync(string username, List<TestResult> history)
        {
            try
            {
                if (string.IsNullOrEmpty(username)) return;
                
                var userDir = GetUserDataPath(username);
                if (!Directory.Exists(userDir))
                {
                    Directory.CreateDirectory(userDir);
                }
                
                var testHistoryPath = GetUserTestHistoryPath(username);
                var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(testHistoryPath, json);
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

        public string GetUserDataPath(string username)
        {
            return Path.Combine(UserDataPath, username);
        }

        public string GetUserSettingsPath(string username)
        {
            return Path.Combine(GetUserDataPath(username), "settings.json");
        }

        public string GetUserTestHistoryPath(string username)
        {
            return Path.Combine(GetUserDataPath(username), "test_history.json");
        }



        public async Task<List<UserData>> LoadUsersAsync()
        {
            var users = new List<UserData>();
            try
            {
                if (!Directory.Exists(UserDataPath)) return users;
                
                var userDirs = Directory.GetDirectories(UserDataPath);
                foreach (var userDir in userDirs)
                {
                    var username = Path.GetFileName(userDir);
                    var userSettingsPath = GetUserSettingsPath(username);
                    
                    if (File.Exists(userSettingsPath))
                    {
                        var json = await File.ReadAllTextAsync(userSettingsPath);
                        var userData = JsonSerializer.Deserialize<UserData>(json);
                        if (userData != null)
                        {
                            users.Add(userData);
                        }
                    }
                }
            }
            catch { }
            return users;
        }

        public async Task SaveUsersAsync(List<UserData> users)
        {
            try
            {
                foreach (var user in users)
                {
                    var userDir = GetUserDataPath(user.Username);
                    if (!Directory.Exists(userDir))
                    {
                        Directory.CreateDirectory(userDir);
                    }
                    
                    var userSettingsPath = GetUserSettingsPath(user.Username);
                    var json = JsonSerializer.Serialize(user, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(userSettingsPath, json);
                }
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
                PasswordHash = PasswordService.HashPassword(password),
                CreatedTime = DateTime.Now,
                LastLoginTime = DateTime.Now,
                IsActive = true
            };

            users.Add(newUser);
            await SaveUsersAsync(users);
            return true;
        }



        public async Task<bool> VerifyUserPasswordAsync(string username, string password)
        {
            var users = await LoadUsersAsync();
            var user = users.FirstOrDefault(u => u.Username == username);
            
            if (user == null || !user.IsActive)
            {
                return false;
            }

            return PasswordService.VerifyPassword(password, user.PasswordHash);
        }





        public async Task MigrateOldDataAsync()
        {
            try
            {
                // Migrate old settings.json if it exists in app root
                var oldSettingsPath = Path.Combine(AppDataPath, "settings.json");
                if (File.Exists(oldSettingsPath) && !File.Exists(SettingsFilePath))
                {
                    var settingsJson = await File.ReadAllTextAsync(oldSettingsPath);
                    await File.WriteAllTextAsync(SettingsFilePath, settingsJson);
                    // Optionally delete old file after migration
                    // File.Delete(oldSettingsPath);
                }

                // Migrate old wordlist_groups.ini if it exists in app root
                var oldGroupsPath = Path.Combine(AppDataPath, "wordlist_groups.ini");
                if (File.Exists(oldGroupsPath) && !File.Exists(WordlistGroupsFilePath))
                {
                    var groupsContent = await File.ReadAllTextAsync(oldGroupsPath);
                    await File.WriteAllTextAsync(WordlistGroupsFilePath, groupsContent);
                    // File.Delete(oldGroupsPath);
                }

                // Migrate old users.json if it exists
                var oldUsersPath = Path.Combine(AppDataPath, "users.json");
                if (File.Exists(oldUsersPath))
                {
                    var usersJson = await File.ReadAllTextAsync(oldUsersPath);
                    var oldUsers = JsonSerializer.Deserialize<List<UserData>>(usersJson);
                    if (oldUsers != null && oldUsers.Count > 0)
                    {
                        await SaveUsersAsync(oldUsers);
                        // Migrate test history for each user
                        var oldHistoryPath = Path.Combine(AppDataPath, "test_history.json");
                        if (File.Exists(oldHistoryPath))
                        {
                            var historyJson = await File.ReadAllTextAsync(oldHistoryPath);
                            var oldHistory = JsonSerializer.Deserialize<List<TestResult>>(historyJson);
                            if (oldHistory != null && oldHistory.Count > 0)
                            {
                                // For simplicity, assign all old history to the first user
                                var firstUser = oldUsers.First();
                                await SaveTestHistoryAsync(firstUser.Username, oldHistory);
                            }
                            // File.Delete(oldHistoryPath);
                        }
                        // File.Delete(oldUsersPath);
                    }
                }
            }
            catch { }
        }


    }
}
