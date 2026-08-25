using System;
using System.Diagnostics;
using System.IO;
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
                return localSecret;

            var legacySecret = LoadLegacyPlaintextSecret();
            if (legacySecret == null)
                return null;

            try
            {
                SaveSecret(legacySecret);
                DeleteLegacySecretFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 旧版密钥迁移失败: {ex.Message}");
            }

            return legacySecret;
        }

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
                var json = Encoding.UTF8.GetString(decrypted);
                var secret = JsonSerializer.Deserialize<BaiduSecretConfig>(json);

                if (secret == null || string.IsNullOrWhiteSpace(secret.AppId) || string.IsNullOrWhiteSpace(secret.ApiKey))
                    return null;

                return secret;
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
                    string.IsNullOrWhiteSpace(secretConfig.BaiduTranslate.AppId) ||
                    string.IsNullOrWhiteSpace(secretConfig.BaiduTranslate.ApiKey))
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

        public static void SaveSecret(BaiduSecretConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrWhiteSpace(config.AppId) || string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("API 凭据不能为空。", nameof(config));

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
            var tempPath = path + ".tmp";
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

            Debug.WriteLine("[SecretStorage] API 密钥已保存到当前用户 DPAPI 加密存储");
        }
    }

    public class BaiduSecretConfig
    {
        public string AppId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}