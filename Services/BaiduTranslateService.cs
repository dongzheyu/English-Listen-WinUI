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
        private const int MAX_CACHE_ENTRIES = 10000;
        private const int MAX_DAILY_HISTORY_DAYS = 7;

        private static readonly HttpClient _sharedHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        private readonly object _cacheLock = new object();
        private readonly string _limitCachePath;

        private readonly Random _random = new();

        private readonly string _translationCachePath;
        private string _apiKey = null!;
        private string _appId = null!;
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
                appDataPath = AppDomain.CurrentDomain.BaseDirectory;
            }

            var cacheDir = Path.Combine(appDataPath, "cache");
            if (!Directory.Exists(cacheDir))
            {
                try
                {
                    Directory.CreateDirectory(cacheDir);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"创建缓存目录失败: {ex.Message}");
                }
            }

            _translationCachePath = Path.Combine(cacheDir, "translation_cache.json");
            _limitCachePath = Path.Combine(cacheDir, "translation_limit.json");
            _currentDate = DateTime.Now.ToString("yyyy-MM-dd");

            _translationCache = new Dictionary<string, string>();
            _dailyLimitCache = new Dictionary<string, int> { { _currentDate, 0 } };

            try
            {
                LoadConfig();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载配置失败: {ex.Message}");
            }

            LoadCache();
        }

        public void Dispose()
        {
            // _sharedHttpClient is static, do not dispose
        }

        private void LoadConfig()
        {
            // Priority 1: DPAPI encrypted storage
            var secret = SecretStorageService.LoadSecret();
            if (secret != null && !string.IsNullOrEmpty(secret.AppId) && !string.IsNullOrEmpty(secret.ApiKey))
            {
                _appId = secret.AppId;
                _apiKey = secret.ApiKey;
                Debug.WriteLine($"从加密存储加载配置: AppId={_appId}");
                return;
            }

            // Priority 2: Legacy plaintext config file
            var legacyConfig = LoadConfigFromFile();
            if (legacyConfig != null)
            {
                SecretStorageService.SaveSecret(new BaiduSecretConfig
                {
                    AppId = legacyConfig.BaiduTranslate.AppId,
                    ApiKey = legacyConfig.BaiduTranslate.ApiKey
                });
                _appId = legacyConfig.BaiduTranslate.AppId;
                _apiKey = legacyConfig.BaiduTranslate.ApiKey;
                Debug.WriteLine($"从旧版文件迁移配置: AppId={_appId}");
                return;
            }

            // Priority 3: Old BaiduTranslateApiKey field in settings.json (format: "appId:apiKey")
            var settingsApiKey = LoadFromSettingsJson();
            if (settingsApiKey != null)
            {
                SecretStorageService.SaveSecret(settingsApiKey);
                _appId = settingsApiKey.AppId;
                _apiKey = settingsApiKey.ApiKey;
                Debug.WriteLine($"从 settings.json 旧字段迁移配置: AppId={_appId}");
                return;
            }

            // Priority 4: Built-in default (user's personal API key)
            _appId = "20260316002574195";
            _apiKey = "CV5ogmfsAmHALHF9goY5";
            Debug.WriteLine($"使用默认配置: AppId={_appId}");
        }

        private BaiduSecretConfig? LoadFromSettingsJson()
        {
            try
            {
                string settingsPath;
                try
                {
                    settingsPath = Path.Combine(
                        ApplicationData.Current.LocalFolder.Path,
                        "config", "settings.json");
                }
                catch
                {
                    settingsPath = Path.Combine(AppContext.BaseDirectory, "config", "settings.json");
                }

                if (!File.Exists(settingsPath)) return null;

                var json = File.ReadAllText(settingsPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("BaiduTranslateApiKey", out var apiKeyElement))
                {
                    var apiKeyStr = apiKeyElement.GetString();
                    if (!string.IsNullOrEmpty(apiKeyStr) && apiKeyStr.Contains(':'))
                    {
                        var parts = apiKeyStr.Split(':', 2);
                        var appId = parts[0].Trim();
                        var apiKey = parts[1].Trim();
                        if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(apiKey))
                        {
                            return new BaiduSecretConfig { AppId = appId, ApiKey = apiKey };
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SecretStorage] 读取 settings.json 失败: {ex.Message}");
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
                    appDataPath = AppDomain.CurrentDomain.BaseDirectory;
                }

                var configPath = Path.Combine(appDataPath, "config", "secret.json");

                if (!File.Exists(configPath))
                    return null;

                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SecretConfig>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (config?.BaiduTranslate == null)
                    return null;

                if (string.IsNullOrEmpty(config.BaiduTranslate.AppId) ||
                    string.IsNullOrEmpty(config.BaiduTranslate.ApiKey))
                    return null;

                return config;
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
            _appId = appId;
            _apiKey = apiKey;
            SecretStorageService.SaveSecret(new BaiduSecretConfig { AppId = appId, ApiKey = apiKey });
            Debug.WriteLine($"已设置自定义API并持久化: AppId={_appId}");
        }

        private void LoadCache()
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            if (File.Exists(_translationCachePath))
            {
                try
                {
                    var json = File.ReadAllText(_translationCachePath);
                    _translationCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json, jsonOptions) ??
                                        new Dictionary<string, string>();

                    if (_translationCache.Count > MAX_CACHE_ENTRIES)
                    {
                        var excess = _translationCache.Count - MAX_CACHE_ENTRIES;
                        var keysToRemove = _translationCache.Keys.Take(excess).ToList();
                        foreach (var key in keysToRemove)
                        {
                            _translationCache.Remove(key);
                        }
                    }
                }
                catch
                {
                    _translationCache = new Dictionary<string, string>();
                }
            }

            if (File.Exists(_limitCachePath))
            {
                try
                {
                    var json = File.ReadAllText(_limitCachePath);
                    var cache = JsonSerializer.Deserialize<Dictionary<string, int>>(json, jsonOptions) ??
                                new Dictionary<string, int>();

                    var cutoffDate = DateTime.Now.AddDays(-MAX_DAILY_HISTORY_DAYS).ToString("yyyy-MM-dd");
                    var staleKeys = cache.Keys.Where(k => string.Compare(k, cutoffDate, StringComparison.Ordinal) < 0)
                        .ToList();
                    foreach (var key in staleKeys)
                    {
                        cache.Remove(key);
                    }

                    if (cache.TryGetValue(_currentDate, out var count))
                    {
                        _dailyLimitCache[_currentDate] = count;
                    }
                }
                catch
                {
                    _dailyLimitCache[_currentDate] = 0;
                }
            }
        }

        private void SaveCache()
        {
            lock (_cacheLock)
            {
                try
                {
                    if (_translationCache.Count > MAX_CACHE_ENTRIES)
                    {
                        var excess = _translationCache.Count - MAX_CACHE_ENTRIES;
                        var keysToRemove = _translationCache.Keys.Take(excess).ToList();
                        foreach (var key in keysToRemove)
                        {
                            _translationCache.Remove(key);
                        }
                    }

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

                    var translationJson = JsonSerializer.Serialize(_translationCache, jsonOptions);
                    File.WriteAllText(_translationCachePath, translationJson);

                    var limitJson = JsonSerializer.Serialize(_dailyLimitCache, jsonOptions);
                    File.WriteAllText(_limitCachePath, limitJson);
                }
                catch
                {
                }
            }
        }

        public int GetRemainingLimit()
        {
            CheckDate();
            return DAILY_LIMIT - _dailyLimitCache[_currentDate];
        }

        public void ResetDailyLimit(int newLimit)
        {
            CheckDate();
            _dailyLimitCache[_currentDate] = 0;
            SaveCache();
            Debug.WriteLine($"已重置当日限额为: {newLimit}");
        }

        private void CheckDate()
        {
            lock (_cacheLock)
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                if (today != _currentDate)
                {
                    _currentDate = today;
                    _dailyLimitCache[_currentDate] = 0;
                    SaveCache();
                }
            }
        }

        private bool CheckLimit()
        {
            CheckDate();
            return _dailyLimitCache[_currentDate] < DAILY_LIMIT;
        }

        private void IncrementLimit()
        {
            lock (_cacheLock)
            {
                CheckDate();
                _dailyLimitCache[_currentDate]++;
                SaveCache();
            }
        }

        public async Task<string> TranslateAsync(string text, string from = "auto", string to = "zh")
        {
            if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("未配置百度翻译 API 密钥，请在设置页面中配置。");
            }

            if (!CheckLimit())
            {
                throw new Exception($"每日翻译限额已用完，最多只能翻译{DAILY_LIMIT}个单词");
            }

            var cacheKey = $"{from}:{to}:{text}";

            lock (_cacheLock)
            {
                if (_translationCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    return cachedResult;
                }
            }

            try
            {
                var salt = _random.Next(100000, 999999).ToString();
                var sign = GenerateSign(text, salt);

                var requestUrl =
                    $"{API_URL}?q={Uri.EscapeDataString(text)}&from={from}&to={to}&appid={_appId}&salt={salt}&sign={sign}";

                var response = await _sharedHttpClient.GetAsync(requestUrl);
                var json = await response.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<BaiduTranslateResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result == null)
                {
                    throw new Exception("翻译响应解析失败");
                }

                if (!string.IsNullOrEmpty(result.ErrorCode) && result.ErrorCode != "0")
                {
                    var errorMsg = GetErrorMessage(result.ErrorCode, result.ErrorMsg);
                    throw new Exception($"API错误 [{result.ErrorCode}]: {errorMsg}");
                }

                if (result.TransResult != null && result.TransResult.Length > 0)
                {
                    var translation = result.TransResult[0].Dst;

                    lock (_cacheLock)
                    {
                        _translationCache[cacheKey] = translation;
                        IncrementLimit();
                        SaveCache();
                    }

                    return translation;
                }

                throw new Exception("翻译结果为空");
            }
            catch (Exception ex)
            {
                throw new Exception($"翻译失败: {ex.Message}");
            }
        }

        private string GenerateSign(string text, string salt)
        {
            var signStr = _appId + text + salt + _apiKey;
            return GetMD5(signStr);
        }

        private string GetMD5(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string GetErrorMessage(string errorCode, string defaultMsg)
        {
            return errorCode switch
            {
                "52001" => "请求超时，请检查网络连接",
                "52002" => "系统错误，请稍后重试",
                "52003" => "未授权用户，请检查APP ID和密钥是否正确",
                "54003" => "访问频率受限，请降低请求频率",
                "54004" => "账户余额不足",
                "54005" => "长query请求频繁",
                "58000" => "客户端IP非法",
                "58001" => "不支持的语言类型",
                "58002" => "服务当前已关闭",
                "90107" => "认证未通过或未生效",
                _ => string.IsNullOrEmpty(defaultMsg) ? "未知错误" : defaultMsg
            };
        }

        public async Task<List<TranslationResultItem>> BatchTranslateAsync(List<string> words, string from = "auto",
            string to = "zh")
        {
            var results = new List<TranslationResultItem>();

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
        [JsonPropertyName("BaiduTranslate")] public required BaiduTranslateConfig BaiduTranslate { get; set; }
    }

    public class BaiduTranslateConfig
    {
        [JsonPropertyName("AppId")] public required string AppId { get; set; }

        [JsonPropertyName("ApiKey")] public required string ApiKey { get; set; }
    }

    public class BaiduTranslateResponse
    {
        [JsonPropertyName("from")] public string From { get; set; } = string.Empty;

        [JsonPropertyName("to")] public string To { get; set; } = string.Empty;

        [JsonPropertyName("trans_result")] public TransResult[] TransResult { get; set; } = Array.Empty<TransResult>();

        [JsonPropertyName("error_code")] public string ErrorCode { get; set; } = string.Empty;

        [JsonPropertyName("error_msg")] public string ErrorMsg { get; set; } = string.Empty;
    }

    public class TransResult
    {
        [JsonPropertyName("src")] public string Src { get; set; } = string.Empty;

        [JsonPropertyName("dst")] public string Dst { get; set; } = string.Empty;
    }

    public class TranslationResultItem
    {
        public string Word { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }
}