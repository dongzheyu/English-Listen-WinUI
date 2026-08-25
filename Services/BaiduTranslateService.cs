using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.Storage;

namespace English_Listen_WinUI.Services
{
    public class BaiduTranslateService : IDisposable
    {
        private const string API_URL = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        public const int DAILY_LIMIT = 1000;
        private const int MaxCacheEntries = 10000;
        private const int MaxDailyHistoryDays = 7;
        private const int MaxTextLength = 5000;
        private const int MaxBatchWords = 1000;
        private const int MaxCredentialLength = 256;
        private const int MaxLanguageLength = 20;
        private const int MaxResponseBytes = 1024 * 1024;

        private static readonly HttpClient SharedHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly object _cacheLock = new();
        private readonly string _limitCachePath;
        private readonly string _translationCachePath;
        private string _apiKey = string.Empty;
        private string _appId = string.Empty;
        private string _currentDate;
        private Dictionary<string, int> _dailyLimitCache;
        private Dictionary<string, string> _translationCache;

        public BaiduTranslateService()
        {
            string appDataPath;
            try
            {
                appDataPath = ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                appDataPath = AppContext.BaseDirectory;
            }

            var cacheDir = Path.Combine(appDataPath, "cache");
            Directory.CreateDirectory(cacheDir);

            _translationCachePath = Path.Combine(cacheDir, "translation_cache.json");
            _limitCachePath = Path.Combine(cacheDir, "translation_limit.json");
            _currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            _translationCache = new Dictionary<string, string>();
            _dailyLimitCache = new Dictionary<string, int> { [_currentDate] = 0 };

            LoadConfig();
            LoadCache();
        }

        public void Dispose()
        {
        }

        private static bool IsValidCredential(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxCredentialLength && !value.Any(char.IsControl);

        private static bool IsValidLanguage(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaxLanguageLength && value.All(c => char.IsLetter(c) || c == '-');

        private void LoadConfig()
        {
            try
            {
                var secret = SecretStorageService.LoadSecret();
                if (secret != null && IsValidCredential(secret.AppId) && IsValidCredential(secret.ApiKey))
                {
                    _appId = secret.AppId.Trim();
                    _apiKey = secret.ApiKey.Trim();
                    return;
                }

                var legacyConfig = LoadConfigFromFile();
                if (legacyConfig != null)
                {
                    _appId = legacyConfig.BaiduTranslate.AppId.Trim();
                    _apiKey = legacyConfig.BaiduTranslate.ApiKey.Trim();
                    SecretStorageService.SaveSecret(new BaiduSecretConfig { AppId = _appId, ApiKey = _apiKey });
                    return;
                }

                var settingsApiKey = LoadFromSettingsJson();
                if (settingsApiKey != null)
                {
                    _appId = settingsApiKey.AppId.Trim();
                    _apiKey = settingsApiKey.ApiKey.Trim();
                    SecretStorageService.SaveSecret(settingsApiKey);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载翻译配置失败: {ex.Message}");
                _appId = string.Empty;
                _apiKey = string.Empty;
            }
        }

        private BaiduSecretConfig? LoadFromSettingsJson()
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
                    return null;

                var info = new FileInfo(settingsPath);
                if (info.Length <= 0 || info.Length > 2 * 1024 * 1024)
                    return null;

                using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (!doc.RootElement.TryGetProperty("BaiduTranslateApiKey", out var element))
                    return null;

                var value = element.GetString();
                if (string.IsNullOrWhiteSpace(value) || value.Length > MaxCredentialLength * 2 + 1)
                    return null;

                var parts = value.Split(':', 2);
                if (parts.Length != 2 || !IsValidCredential(parts[0]) || !IsValidCredential(parts[1]))
                    return null;

                return new BaiduSecretConfig { AppId = parts[0].Trim(), ApiKey = parts[1].Trim() };
            }
            catch
            {
                return null;
            }
        }

        private SecretConfig? LoadConfigFromFile()
        {
            try
            {
                string appDataPath;
                try
                {
                    appDataPath = ApplicationData.Current.LocalFolder.Path;
                }
                catch
                {
                    appDataPath = AppContext.BaseDirectory;
                }

                var configPath = Path.Combine(appDataPath, "config", "secret.json");
                if (!File.Exists(configPath))
                    return null;

                var info = new FileInfo(configPath);
                if (info.Length <= 0 || info.Length > 2 * 1024 * 1024)
                    return null;

                var config = JsonSerializer.Deserialize<SecretConfig>(File.ReadAllText(configPath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return config?.BaiduTranslate != null &&
                       IsValidCredential(config.BaiduTranslate.AppId) &&
                       IsValidCredential(config.BaiduTranslate.ApiKey)
                    ? config
                    : null;
            }
            catch
            {
                return null;
            }
        }

        public void RefreshApiKey()
        {
            LoadConfig();
        }

        public void SetCustomApiKey(string appId, string apiKey)
        {
            if (!IsValidCredential(appId) || !IsValidCredential(apiKey))
                throw new ArgumentException("百度翻译 API 凭据无效或长度超限。");

            _appId = appId.Trim();
            _apiKey = apiKey.Trim();
            SecretStorageService.SaveSecret(new BaiduSecretConfig { AppId = _appId, ApiKey = _apiKey });
        }

        private void LoadCache()
        {
            lock (_cacheLock)
            {
                try
                {
                    if (File.Exists(_translationCachePath))
                    {
                        var info = new FileInfo(_translationCachePath);
                        if (info.Length <= 10 * 1024 * 1024)
                        {
                            var cache = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_translationCachePath));
                            if (cache != null)
                                _translationCache = TrimCache(cache);
                        }
                    }
                }
                catch
                {
                    _translationCache = new Dictionary<string, string>();
                }

                try
                {
                    if (File.Exists(_limitCachePath))
                    {
                        var info = new FileInfo(_limitCachePath);
                        if (info.Length <= 1024 * 1024)
                        {
                            var cache = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(_limitCachePath));
                            if (cache != null)
                            {
                                var cutoffDate = DateTime.Now.AddDays(-MaxDailyHistoryDays).ToString("yyyy-MM-dd");
                                _dailyLimitCache = cache
                                    .Where(item => string.CompareOrdinal(item.Key, cutoffDate) >= 0)
                                    .ToDictionary(item => item.Key, item => Math.Clamp(item.Value, 0, DAILY_LIMIT));
                            }
                        }
                    }
                }
                catch
                {
                    _dailyLimitCache = new Dictionary<string, int>();
                }

                _dailyLimitCache.TryAdd(_currentDate, 0);
            }
        }

        private static Dictionary<string, string> TrimCache(Dictionary<string, string> cache)
        {
            if (cache.Count <= MaxCacheEntries)
                return cache;

            return cache.Skip(cache.Count - MaxCacheEntries)
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private void SaveCache()
        {
            Dictionary<string, string> translationSnapshot;
            Dictionary<string, int> limitSnapshot;
            lock (_cacheLock)
            {
                _translationCache = TrimCache(_translationCache);
                translationSnapshot = new Dictionary<string, string>(_translationCache);
                limitSnapshot = new Dictionary<string, int>(_dailyLimitCache);
            }

            try
            {
                WriteAtomic(_translationCachePath, JsonSerializer.Serialize(translationSnapshot));
                WriteAtomic(_limitCachePath, JsonSerializer.Serialize(limitSnapshot));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存翻译缓存失败: {ex.Message}");
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            var directory = Path.GetDirectoryName(path) ?? throw new IOException("缓存目录无效。");
            Directory.CreateDirectory(directory);
            var tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(tempPath, content, Encoding.UTF8);
                File.Move(tempPath, path, true);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }

        public int GetRemainingLimit()
        {
            CheckDate();
            lock (_cacheLock)
            {
                return Math.Max(0, DAILY_LIMIT - _dailyLimitCache[_currentDate]);
            }
        }

        public void ResetDailyLimit(int newLimit)
        {
            CheckDate();
            lock (_cacheLock)
            {
                _dailyLimitCache[_currentDate] = Math.Clamp(newLimit, 0, DAILY_LIMIT);
            }
            SaveCache();
        }

        private void CheckDate()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            lock (_cacheLock)
            {
                if (today == _currentDate)
                    return;

                _currentDate = today;
                _dailyLimitCache[_currentDate] = 0;
            }
        }

        private bool TryConsumeLimit()
        {
            CheckDate();
            lock (_cacheLock)
            {
                if (_dailyLimitCache[_currentDate] >= DAILY_LIMIT)
                    return false;

                _dailyLimitCache[_currentDate]++;
                return true;
            }
        }

        private void RefundLimit()
        {
            lock (_cacheLock)
            {
                if (_dailyLimitCache.TryGetValue(_currentDate, out var count) && count > 0)
                    _dailyLimitCache[_currentDate] = count - 1;
            }
        }

        private static async Task<string> ReadResponseBodyAsync(HttpContent content)
        {
            if (content.Headers.ContentLength is > MaxResponseBytes)
                throw new InvalidDataException("翻译服务响应过大。");

            await using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                if (read == 0)
                    break;

                if (memory.Length + read > MaxResponseBytes)
                    throw new InvalidDataException("翻译服务响应过大。");

                memory.Write(buffer, 0, read);
            }

            return Encoding.UTF8.GetString(memory.ToArray());
        }

        public async Task<string> TranslateAsync(string text, string from = "auto", string to = "zh")
        {
            if (!IsValidCredential(_appId) || !IsValidCredential(_apiKey))
                throw new InvalidOperationException("未配置有效的百度翻译 API 密钥，请在设置页面中配置。");

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("待翻译文本不能为空。", nameof(text));

            if (text.Length > MaxTextLength)
                throw new ArgumentException($"待翻译文本不能超过 {MaxTextLength} 个字符。", nameof(text));

            if (!IsValidLanguage(from) || !IsValidLanguage(to))
                throw new ArgumentException("翻译语言参数无效。", nameof(from));

            var cacheKey = $"{from}:{to}:{text}";
            lock (_cacheLock)
            {
                if (_translationCache.TryGetValue(cacheKey, out var cachedResult))
                    return cachedResult;
            }

            if (!TryConsumeLimit())
                throw new InvalidOperationException($"每日翻译限额已用完，最多只能翻译 {DAILY_LIMIT} 次。");

            try
            {
                var salt = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
                var sign = GenerateSign(text, salt);
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["q"] = text,
                    ["from"] = from,
                    ["to"] = to,
                    ["appid"] = _appId,
                    ["salt"] = salt,
                    ["sign"] = sign
                });

                using var response = await SharedHttpClient.PostAsync(API_URL, content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await ReadResponseBodyAsync(response.Content).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<BaiduTranslateResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                    throw new InvalidOperationException("翻译响应解析失败。");

                if (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode != "0")
                    throw new InvalidOperationException($"API错误 [{result.ErrorCode}]: {GetErrorMessage(result.ErrorCode, result.ErrorMsg)}");

                var translation = result.TransResult?.FirstOrDefault()?.Dst;
                if (string.IsNullOrWhiteSpace(translation) || translation.Length > MaxTextLength)
                    throw new InvalidDataException("翻译结果无效或过大。");

                lock (_cacheLock)
                {
                    _translationCache[cacheKey] = translation;
                }
                SaveCache();
                return translation;
            }
            catch
            {
                RefundLimit();
                SaveCache();
                throw;
            }
        }

        private string GenerateSign(string text, string salt)
        {
            var signStr = _appId + text + salt + _apiKey;
            return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(signStr))).ToLowerInvariant();
        }

        private static string GetErrorMessage(string errorCode, string defaultMsg)
        {
            return errorCode switch
            {
                "52001" => "请求超时，请检查网络连接",
                "52002" => "系统错误，请稍后重试",
                "52003" => "未授权用户，请检查 APP ID 和密钥是否正确",
                "54003" => "访问频率受限，请降低请求频率",
                "54004" => "账户余额不足",
                "54005" => "长 query 请求频繁",
                "58000" => "客户端 IP 非法",
                "58001" => "不支持的语言类型",
                "58002" => "服务当前已关闭",
                "90107" => "认证未通过或未生效",
                _ => string.IsNullOrWhiteSpace(defaultMsg) ? "未知错误" : defaultMsg
            };
        }

        public async Task<List<TranslationResultItem>> BatchTranslateAsync(List<string> words, string from = "auto", string to = "zh")
        {
            if (words == null)
                throw new ArgumentNullException(nameof(words));

            if (words.Count > MaxBatchWords)
                throw new ArgumentException($"批量翻译最多支持 {MaxBatchWords} 个词。", nameof(words));

            var results = new List<TranslationResultItem>(words.Count);
            foreach (var word in words)
            {
                try
                {
                    var translation = await TranslateAsync(word, from, to);
                    results.Add(new TranslationResultItem { Word = word, Translation = translation });
                }
                catch (Exception ex)
                {
                    results.Add(new TranslationResultItem { Word = word, Translation = $"翻译失败: {ex.Message}" });
                }
            }

            return results;
        }
    }

    public class SecretConfig
    {
        [JsonPropertyName("BaiduTranslate")]
        public required BaiduTranslateConfig BaiduTranslate { get; set; }
    }

    public class BaiduTranslateConfig
    {
        [JsonPropertyName("AppId")]
        public required string AppId { get; set; }

        [JsonPropertyName("ApiKey")]
        public required string ApiKey { get; set; }
    }

    public class BaiduTranslateResponse
    {
        [JsonPropertyName("from")]
        public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")]
        public string To { get; set; } = string.Empty;

        [JsonPropertyName("trans_result")]
        public TransResult[] TransResult { get; set; } = Array.Empty<TransResult>();

        [JsonPropertyName("error_code")]
        public string ErrorCode { get; set; } = string.Empty;

        [JsonPropertyName("error_msg")]
        public string ErrorMsg { get; set; } = string.Empty;
    }

    public class TransResult
    {
        [JsonPropertyName("src")]
        public string Src { get; set; } = string.Empty;

        [JsonPropertyName("dst")]
        public string Dst { get; set; } = string.Empty;
    }

    public class TranslationResultItem
    {
        public string Word { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }
}