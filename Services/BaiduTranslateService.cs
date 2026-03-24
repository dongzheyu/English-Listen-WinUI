using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using English_Listen_WinUI.ViewModels;

namespace English_Listen_WinUI.Services
{
    public class BaiduTranslateService
    {
        private const string API_URL = "https://fanyi-api.baidu.com/api/trans/vip/translate";
        private const int DAILY_LIMIT = 100;
        private const string RESOURCE_NAME = "English_Listen_WinUI.Config.secret.json";

        private readonly HttpClient _httpClient;
        private readonly string _translationCachePath;
        private readonly string _limitCachePath;

        private Dictionary<string, string> _translationCache;
        private Dictionary<string, int> _dailyLimitCache;
        private string _currentDate;
        private string _appId = null!;
        private string _apiKey = null!;

        public BaiduTranslateService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            var appDataPath = AppDomain.CurrentDomain.BaseDirectory;
            var cacheDir = Path.Combine(appDataPath, "cache");
            if (!Directory.Exists(cacheDir))
            {
                try
                {
                    Directory.CreateDirectory(cacheDir);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建缓存目录失败: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"加载配置失败: {ex.Message}");
            }
            LoadCache();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        private void LoadConfig()
        {
            SecretConfig? config = null;

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceStream = assembly.GetManifestResourceStream(RESOURCE_NAME);

                if (resourceStream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"嵌入资源 {RESOURCE_NAME} 未找到，尝试从外部文件读取");
                    config = LoadConfigFromFile();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"从嵌入资源 {RESOURCE_NAME} 读取配置");
                    using (var reader = new StreamReader(resourceStream))
                    {
                        var json = reader.ReadToEnd();
                        config = JsonConvert.DeserializeObject<SecretConfig>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从嵌入资源读取配置失败: {ex.Message}，尝试从外部文件读取");
                config = LoadConfigFromFile();
            }

            if (config == null || config.BaiduTranslate == null)
            {
                throw new InvalidOperationException("配置文件格式错误：缺少 BaiduTranslate 配置节。");
            }

            if (string.IsNullOrEmpty(config.BaiduTranslate.AppId))
            {
                throw new InvalidOperationException("配置文件错误：AppId 不能为空。");
            }

            if (string.IsNullOrEmpty(config.BaiduTranslate.ApiKey))
            {
                throw new InvalidOperationException("配置文件错误：ApiKey 不能为空。");
            }

            _appId = config.BaiduTranslate.AppId;
            _apiKey = config.BaiduTranslate.ApiKey;

            System.Diagnostics.Debug.WriteLine($"成功加载配置: AppId={_appId}");
        }

        private SecretConfig LoadConfigFromFile()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "secret.json");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"配置文件不存在: {configPath}。请在项目根目录的 config 文件夹中创建 secret.json 文件。");
                }

                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<SecretConfig>(json);

                if (config == null || config.BaiduTranslate == null)
                {
                    throw new Exception("配置文件格式错误：缺少 BaiduTranslate 配置节。");
                }

                if (string.IsNullOrEmpty(config.BaiduTranslate.AppId))
                {
                    throw new Exception("配置文件错误：AppId 不能为空。");
                }

                if (string.IsNullOrEmpty(config.BaiduTranslate.ApiKey))
                {
                    throw new Exception("配置文件错误：ApiKey 不能为空。");
                }

                System.Diagnostics.Debug.WriteLine($"从外部文件加载配置: AppId={config.BaiduTranslate.AppId}");
                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载配置文件失败: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"已设置自定义API: AppId={_appId}");
        }

        private void LoadCache()
        {
            var jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            };

            if (File.Exists(_translationCachePath))
            {
                try
                {
                    var json = File.ReadAllText(_translationCachePath);
                    _translationCache = JsonConvert.DeserializeObject<Dictionary<string, string>>(json, jsonSettings) ?? new Dictionary<string, string>();
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
                    var cache = JsonConvert.DeserializeObject<Dictionary<string, int>>(json, jsonSettings) ?? new Dictionary<string, int>();

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
            try
            {
                var jsonSettings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };

                var translationJson = JsonConvert.SerializeObject(_translationCache, jsonSettings);
                File.WriteAllText(_translationCachePath, translationJson);

                var limitJson = JsonConvert.SerializeObject(_dailyLimitCache, jsonSettings);
                File.WriteAllText(_limitCachePath, limitJson);
            }
            catch
            {
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
            System.Diagnostics.Debug.WriteLine($"已重置当日限额为: {newLimit}");
        }

        private void CheckDate()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            if (today != _currentDate)
            {
                _currentDate = today;
                _dailyLimitCache[_currentDate] = 0;
                SaveCache();
            }
        }

        private bool CheckLimit()
        {
            CheckDate();
            return _dailyLimitCache[_currentDate] < DAILY_LIMIT;
        }

        private void IncrementLimit()
        {
            CheckDate();
            _dailyLimitCache[_currentDate]++;
            SaveCache();
        }

        public async Task<string> TranslateAsync(string text, string from = "auto", string to = "zh")
        {
            if (!CheckLimit())
            {
                throw new Exception($"每日翻译限额已用完，最多只能翻译{DAILY_LIMIT}个单词");
            }

            var cacheKey = $"{from}:{to}:{text}";
            if (_translationCache.TryGetValue(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            try
            {
                var salt = new Random().Next(100000, 999999).ToString();
                var sign = GenerateSign(text, salt);

                var requestUrl = $"{API_URL}?q={Uri.EscapeDataString(text)}&from={from}&to={to}&appid={_appId}&salt={salt}&sign={sign}";

                System.Diagnostics.Debug.WriteLine($"翻译API请求: {requestUrl}");
                System.Diagnostics.Debug.WriteLine($"APP_ID: {_appId}");
                System.Diagnostics.Debug.WriteLine($"Sign: {sign}");

                var response = await _httpClient.GetAsync(requestUrl);
                var json = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"翻译API响应: {json}");

                var result = JsonConvert.DeserializeObject<BaiduTranslateResponse>(json);

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
                    _translationCache[cacheKey] = translation;
                    IncrementLimit();
                    SaveCache();
                    return translation;
                }

                throw new Exception("翻译结果为空");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"翻译异常: {ex.Message}");
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
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                var sb = new StringBuilder();
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
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

        public async Task<List<TranslationResultItem>> BatchTranslateAsync(List<string> words, string from = "auto", string to = "zh")
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
        [JsonProperty("BaiduTranslate")]
        public required BaiduTranslateConfig BaiduTranslate { get; set; }
    }

    public class BaiduTranslateConfig
    {
        [JsonProperty("AppId")]
        public required string AppId { get; set; }

        [JsonProperty("ApiKey")]
        public required string ApiKey { get; set; }
    }

    public class BaiduTranslateResponse
    {
        [JsonProperty("from")]
        public string From { get; set; } = string.Empty;

        [JsonProperty("to")]
        public string To { get; set; } = string.Empty;

        [JsonProperty("trans_result")]
        public TransResult[] TransResult { get; set; } = Array.Empty<TransResult>();

        [JsonProperty("error_code")]
        public string ErrorCode { get; set; } = string.Empty;

        [JsonProperty("error_msg")]
        public string ErrorMsg { get; set; } = string.Empty;
    }

    public class TransResult
    {
        [JsonProperty("src")]
        public string Src { get; set; } = string.Empty;

        [JsonProperty("dst")]
        public string Dst { get; set; } = string.Empty;
    }

    public class TranslationResultItem
    {
        public string Word { get; set; } = string.Empty;
        public string Translation { get; set; } = string.Empty;
    }
}
