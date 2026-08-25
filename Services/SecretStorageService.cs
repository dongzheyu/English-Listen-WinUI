using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Windows.Storage;

namespace English_Listen_WinUI.Services
{
    public static class SecretStorageService
    {
        private const string SecretDirName = "secrets";
        private const string SecretFileName = "baidu_api.dat";
        private const string LegacySecretFileName = "secret.json";
        private const int MaxSecretFileBytes = 64 * 1024;
        private const int MaxCredentialLength = 256;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EnglishListenWinUI_BaiduAPI_v2");

        private static string GetSecretDirectory()
        {
            try
            {
                return Path.Combine(ApplicationData.Current.LocalFolder.Path, SecretDirName);
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, SecretDirName);
            }
        }

        private static string GetSecretFilePath() => Path.Combine(GetSecretDirectory(), SecretFileName);

        public static BaiduSecretConfig? LoadSecret()
        {
            var localSecret = LoadLocalEncryptedSecret();
            if (localSecret != null)
            {
                DeleteLegacySettingsApiKey();
                return localSecret;
            }

            var legacySecret = LoadLegacyPlaintextSecret();
            if (legacySecret == null)
                return null;

            try
            {
                SaveSecret(legacySecret);
                DeleteLegacySecretFile();
                DeleteLegacySettingsApiKey();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 旧版密钥迁移失败: {ex.Message}");
            }

            return legacySecret;
        }

        private static bool IsValidCredential(string? value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxCredentialLength && !value.Any(char.IsControl);

        private static BaiduSecretConfig? LoadLocalEncryptedSecret()
        {
            try
            {
                var path = GetSecretFilePath();
                if (!File.Exists(path))
                    return null;

                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > MaxSecretFileBytes)
                    return null;

                var encrypted = File.ReadAllBytes(path);
                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                if (decrypted.Length > MaxSecretFileBytes)
                    return null;

                var json = Encoding.UTF8.GetString(decrypted);
                var secret = JsonSerializer.Deserialize<BaiduSecretConfig>(json);
                if (secret == null || !IsValidCredential(secret.AppId) || !IsValidCredential(secret.ApiKey))
                    return null;

                return new BaiduSecretConfig
                {
                    AppId = secret.AppId.Trim(),
                    ApiKey = secret.ApiKey.Trim()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 本地解密失败: {ex.Message}");
                return null;
            }
        }

        private static BaiduSecretConfig? LoadLegacyPlaintextSecret()
        {
            try
            {
                string configPath;
                try
                {
                    configPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "config", LegacySecretFileName);
                }
                catch
                {
                    configPath = Path.Combine(AppContext.BaseDirectory, "config", LegacySecretFileName);
                }

                if (!File.Exists(configPath))
                    return null;

                var info = new FileInfo(configPath);
                if (info.Length <= 0 || info.Length > MaxSecretFileBytes)
                    return null;

                var json = File.ReadAllText(configPath);
                var secretConfig = JsonSerializer.Deserialize<SecretConfig>(json);
                if (secretConfig?.BaiduTranslate == null ||
                    !IsValidCredential(secretConfig.BaiduTranslate.AppId) ||
                    !IsValidCredential(secretConfig.BaiduTranslate.ApiKey))
                    return null;

                return new BaiduSecretConfig
                {
                    AppId = secretConfig.BaiduTranslate.AppId.Trim(),
                    ApiKey = secretConfig.BaiduTranslate.ApiKey.Trim()
                };
            }
            catch
            {
                return null;
            }
        }

        private static void DeleteLegacySecretFile()
        {
            try
            {
                string configPath;
                try
                {
                    configPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "config", LegacySecretFileName);
                }
                catch
                {
                    configPath = Path.Combine(AppContext.BaseDirectory, "config", LegacySecretFileName);
                }

                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 删除旧版明文密钥失败: {ex.Message}");
            }
        }

        private static void DeleteLegacySettingsApiKey()
        {
            try
            {
                string settingsPath;
                try
                {
                    settingsPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "config", "settings.json");
                }
                catch
                {
                    settingsPath = Path.Combine(AppContext.BaseDirectory, "config", "settings.json");
                }

                if (!File.Exists(settingsPath))
                    return;

                var info = new FileInfo(settingsPath);
                if (info.Length <= 0 || info.Length > 2 * 1024 * 1024)
                    return;

                using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (!document.RootElement.TryGetProperty("BaiduTranslateApiKey", out _))
                    return;

                var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!string.Equals(property.Name, "BaiduTranslateApiKey", StringComparison.OrdinalIgnoreCase))
                        values[property.Name] = property.Value.Clone();
                }

                var sanitized = JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true });
                var tempPath = settingsPath + $".{Guid.NewGuid():N}.tmp";
                File.WriteAllText(tempPath, sanitized, Encoding.UTF8);
                File.Move(tempPath, settingsPath, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 清理 settings.json 明文密钥失败: {ex.Message}");
            }
        }

        public static void SaveSecret(BaiduSecretConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!IsValidCredential(config.AppId) || !IsValidCredential(config.ApiKey))
                throw new ArgumentException("API 凭据无效或长度超限。", nameof(config));

            var dir = GetSecretDirectory();
            Directory.CreateDirectory(dir);

            var normalized = new BaiduSecretConfig
            {
                AppId = config.AppId.Trim(),
                ApiKey = config.ApiKey.Trim()
            };
            var json = JsonSerializer.Serialize(normalized);
            var bytes = Encoding.UTF8.GetBytes(json);
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);

            var path = GetSecretFilePath();
            var tempPath = path + $".{Guid.NewGuid():N}.tmp";
            File.WriteAllBytes(tempPath, encrypted);

            try
            {
                File.Move(tempPath, path, true);
            }
            catch
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
                throw;
            }
        }
    }

    public class BaiduSecretConfig
    {
        public string AppId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}