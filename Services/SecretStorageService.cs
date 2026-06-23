using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace English_Listen_WinUI.Services
{
    public static class SecretStorageService
    {
        private const string SECRET_DIR_NAME = "secrets";
        private const string SECRET_FILE_NAME = "baidu_api.dat";
        private const string LEGACY_SECRET_FILE_NAME = "secret.json";
        private const string EMBEDDED_ENCRYPTED_RESOURCE = "English_Listen_WinUI.Config.encrypted_secret.dat";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("EnglishListenWinUI_BaiduAPI_v1");

        private static string GetSecretDirectory()
        {
            try
            {
                return Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, SECRET_DIR_NAME);
            }
            catch
            {
                return Path.Combine(AppContext.BaseDirectory, SECRET_DIR_NAME);
            }
        }

        private static string GetSecretFilePath()
        {
            return Path.Combine(GetSecretDirectory(), SECRET_FILE_NAME);
        }

        public static BaiduSecretConfig? LoadSecret()
        {
            // Priority 1: User-local DPAPI encrypted file
            var localSecret = LoadLocalEncryptedSecret();
            if (localSecret != null)
            {
                System.Diagnostics.Debug.WriteLine("[SecretStorage] 从本地加密文件加载密钥");
                return localSecret;
            }

            // Priority 2: Legacy plaintext JSON in config folder
            var legacySecret = LoadLegacyPlaintextSecret();
            if (legacySecret != null)
            {
                System.Diagnostics.Debug.WriteLine("[SecretStorage] 从旧版明文文件迁移密钥");
                MigrateToEncrypted(legacySecret);
                return legacySecret;
            }

            // Priority 3: Embedded encrypted resource (CI/CD built with LocalMachine DPAPI)
            var embeddedSecret = LoadEmbeddedEncryptedSecret();
            if (embeddedSecret != null)
            {
                System.Diagnostics.Debug.WriteLine("[SecretStorage] 从嵌入加密资源迁移密钥");
                MigrateToEncrypted(embeddedSecret);
                return embeddedSecret;
            }

            System.Diagnostics.Debug.WriteLine("[SecretStorage] 未找到任何密钥配置");
            return null;
        }

        private static BaiduSecretConfig? LoadLocalEncryptedSecret()
        {
            try
            {
                var path = GetSecretFilePath();
                if (!File.Exists(path)) return null;

                var encrypted = File.ReadAllBytes(path);
                if (encrypted.Length == 0) return null;

                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(decrypted);
                return JsonSerializer.Deserialize<BaiduSecretConfig>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecretStorage] 本地解密失败: {ex.Message}");
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
                    configPath = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "config", LEGACY_SECRET_FILE_NAME);
                }
                catch
                {
                    configPath = Path.Combine(AppContext.BaseDirectory, "config", LEGACY_SECRET_FILE_NAME);
                }

                if (!File.Exists(configPath)) return null;

                var json = File.ReadAllText(configPath);
                var secretConfig = JsonSerializer.Deserialize<SecretConfig>(json);
                if (secretConfig?.BaiduTranslate == null) return null;

                return new BaiduSecretConfig
                {
                    AppId = secretConfig.BaiduTranslate.AppId,
                    ApiKey = secretConfig.BaiduTranslate.ApiKey
                };
            }
            catch
            {
                return null;
            }
        }

        private static BaiduSecretConfig? LoadEmbeddedEncryptedSecret()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(EMBEDDED_ENCRYPTED_RESOURCE);
                if (stream == null) return null;

                var encrypted = new byte[stream.Length];
                stream.Read(encrypted, 0, encrypted.Length);

                // Decrypt with LocalMachine scope (CI/CD encrypted for any user on this machine)
                var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
                var json = Encoding.UTF8.GetString(decrypted);

                // Try as SecretConfig (legacy format) first
                var secretConfig = JsonSerializer.Deserialize<SecretConfig>(json);
                if (secretConfig?.BaiduTranslate != null)
                {
                    return new BaiduSecretConfig
                    {
                        AppId = secretConfig.BaiduTranslate.AppId,
                        ApiKey = secretConfig.BaiduTranslate.ApiKey
                    };
                }

                // Try as BaiduSecretConfig directly
                return JsonSerializer.Deserialize<BaiduSecretConfig>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecretStorage] 嵌入加密资源解密失败: {ex.Message}");
                return null;
            }
        }

        public static void SaveSecret(BaiduSecretConfig config)
        {
            try
            {
                var dir = GetSecretDirectory();
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(config);
                var bytes = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);

                // Atomic write: write to temp then move
                var path = GetSecretFilePath();
                var tempPath = path + ".tmp";
                File.WriteAllBytes(tempPath, encrypted);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(tempPath, path);

                System.Diagnostics.Debug.WriteLine($"[SecretStorage] 密钥已加密保存: AppId={config.AppId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecretStorage] 保存密钥失败: {ex.Message}");
                throw;
            }
        }

        private static void MigrateToEncrypted(BaiduSecretConfig config)
        {
            try
            {
                SaveSecret(config);
                System.Diagnostics.Debug.WriteLine("[SecretStorage] 密钥已迁移至 DPAPI 加密存储");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SecretStorage] 密钥迁移失败: {ex.Message}");
            }
        }
    }

    public class BaiduSecretConfig
    {
        public string AppId { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
    }
}
