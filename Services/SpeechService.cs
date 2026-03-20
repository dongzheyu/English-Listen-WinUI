using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using System.Runtime.InteropServices.WindowsRuntime;
using English_Listen_WinUI.Helpers;
using English_Listen_WinUI.Models;

namespace English_Listen_WinUI.Services
{
    /// <summary>
    /// 语音合成服务 - 支持多种引擎
    /// EngineType: "Flite", "WindowsTTS", "Auto"
    /// </summary>
    public class SpeechService : IDisposable
    {
        // 引擎类型
        private string _engineType = "SAPI"; // "SAPI" only
        private bool _isSpeaking;
        private bool _isPaused;
        private MediaPlayer? _mediaPlayer;
        private WindowsTtsService? _windowsTtsService;
        private readonly object _lockObject = new object();

        // 队列顺序朗读
        private readonly ConcurrentQueue<(string text, string? voiceModel)> _queue = new();
        private Task? _worker;
        private CancellationTokenSource? _lifetimeCts = new();

        // Windows TTS语音设置
        private string _currentWindowsTtsVoice = string.Empty;
        private bool _isVoiceInitialized = false;

        public bool IsSpeaking
        {
            get { lock (_lockObject) { return _isSpeaking; } }
            private set { lock (_lockObject) { _isSpeaking = value; } }
        }

        public string EngineType
        {
            get => _engineType;
            set
            {
                _engineType = value;
                UpdateEngineConfiguration();
            }
        }

        public bool IsWindowsTtsAvailable => _windowsTtsService?.IsInitialized ?? false;

        public SpeechService()
        {
            InitializeMediaPlayer();
            InitializeWindowsTts();
            StartWorker();
        }

        private void InitializeMediaPlayer()
        {
            try
            {
                _mediaPlayer = new MediaPlayer();
            }
            catch
            {
                _mediaPlayer = null;
            }
        }

        /// <summary>
        /// 初始化Windows TTS服务并加载保存的语音设置
        /// </summary>
        private void InitializeWindowsTts()
        {
            try
            {
                // 只在Windows 11上初始化Windows TTS
                if (OsVersionHelper.IsWindows11_24H2_OrLater())
                {
                    _windowsTtsService = new WindowsTtsService();
                    System.Diagnostics.Debug.WriteLine("Windows TTS服务初始化完成");

                    // 延迟加载保存的语音设置（等待Settings加载完成）
                    _ = LoadSavedWindowsTtsVoiceAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("当前系统不支持Windows TTS，需要Windows 11 24H2+");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS服务初始化失败: {ex.Message}");
                _windowsTtsService = null;
            }
        }

        /// <summary>
        /// 从设置中加载保存的Windows TTS语音
        /// </summary>
        private async Task LoadSavedWindowsTtsVoiceAsync()
        {
            // 等待一小段时间确保Settings已加载
            await Task.Delay(500);

            try
            {
                var savedVoice = GetSavedWindowsTtsVoiceName();
                if (!string.IsNullOrEmpty(savedVoice))
                {
                    SetWindowsTtsVoice(savedVoice);
                }
                _isVoiceInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载保存的Windows TTS语音失败: {ex.Message}");
                _isVoiceInitialized = true;
            }
        }

        /// <summary>
        /// 获取保存的Windows TTS语音名称
        /// </summary>
        private string GetSavedWindowsTtsVoiceName()
        {
            try
            {
                if (App.SharedViewModel?.Settings?.Settings != null)
                {
                    return App.SharedViewModel.Settings.Settings.WindowsTtsVoiceName ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取保存的语音名称失败: {ex.Message}");
            }
            return string.Empty;
        }

        private void UpdateEngineConfiguration()
        {
            // 根据引擎类型更新配置
            System.Diagnostics.Debug.WriteLine($"语音引擎切换到: {_engineType}");
        }

        /// <summary>
        /// 获取推荐的引擎类型
        /// </summary>
        public string GetRecommendedEngine()
        {
            return "SAPI";
        }

        /// <summary>
        /// 获取可用的引擎列表
        /// </summary>
        public Dictionary<string, string> GetAvailableEngines()
        {
            var engines = new Dictionary<string, string>();

            if (IsWindowsTtsAvailable)
            {
                engines["SAPI"] = "Windows SAPI 语音引擎";
            }

            return engines;
        }

        /// <summary>
        /// 获取Windows TTS的可用语音列表
        /// </summary>
        public VoiceInfo[] GetWindowsTtsVoices()
        {
            return _windowsTtsService?.AvailableVoices ?? new VoiceInfo[0];
        }

        /// <summary>
        /// 设置Windows TTS语音
        /// </summary>
        /// <param name="voiceName">语音名称（DisplayName）</param>
        /// <returns>是否设置成功</returns>
        public bool SetWindowsTtsVoice(string voiceName)
        {
            if (_windowsTtsService == null || string.IsNullOrEmpty(voiceName))
                return false;

            try
            {
                lock (_lockObject)
                {
                    if (_windowsTtsService.SelectVoice(voiceName))
                    {
                        _currentWindowsTtsVoice = voiceName;
                        System.Diagnostics.Debug.WriteLine($"SpeechService: Windows TTS语音已设置为: {voiceName}");
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"SpeechService: 无法设置Windows TTS语音: {voiceName}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置Windows TTS语音失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取当前Windows TTS语音名称
        /// </summary>
        public string GetCurrentWindowsTtsVoice()
        {
            return _currentWindowsTtsVoice;
        }

        // 公共入口：仅入队，立即返回
        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            _queue.Enqueue((text, null));
            return Task.CompletedTask;
        }

        /// <summary>
        /// 使用指定Windows TTS语音进行朗读（用于试听功能）
        /// </summary>
        public async Task SpeakWithWindowsTtsVoiceAsync(string text, string voiceName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || _windowsTtsService == null) return;

            try
            {
                // 临时保存当前语音
                var originalVoice = _windowsTtsService.CurrentVoice;

                // 切换到指定语音
                if (_windowsTtsService.SelectVoice(voiceName))
                {
                    // 直接播放，不经过队列
                    await _windowsTtsService.SpeakAsync(text);
                }

                // 恢复原始语音（如果有）
                if (originalVoice != null)
                {
                    _windowsTtsService.SelectVoice(originalVoice.Name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS指定语音朗读失败: {ex.Message}");
            }
        }

        // 顺序播放工作线程
        private void StartWorker()
        {
            _worker = Task.Run(async () =>
            {
                var token = _lifetimeCts!.Token;
                while (!token.IsCancellationRequested)
                {
                    if (_queue.TryDequeue(out var item))
                    {
                        try
                        {
                            var engine = GetRecommendedEngine();

                            if (_windowsTtsService != null)
                                await SpeakWithWindowsTtsAsync(item.text);
                            else
                                System.Diagnostics.Debug.WriteLine("没有可用的语音引擎");
                        }
                        catch (OperationCanceledException) { /* 用户停止 */ }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"朗读错误: {ex.Message}");
                        }
                    }
                    else
                    {
                        await Task.Delay(50, token); // 空闲时稍等
                    }
                }
            });
        }

        /// <summary>
        /// 使用Windows TTS朗读（应用保存的语音设置）
        /// </summary>
        private async Task SpeakWithWindowsTtsAsync(string text)
        {
            if (_windowsTtsService == null) return;

            try
            {
                IsSpeaking = true;

                // 在朗读前应用保存的语音设置
                await ApplyWindowsTtsVoiceSettingAsync();

                await _windowsTtsService.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS朗读失败: {ex.Message}");
            }
            finally
            {
                IsSpeaking = false;
            }
        }

        /// <summary>
        /// 应用保存的Windows TTS语音设置
        /// </summary>
        private async Task ApplyWindowsTtsVoiceSettingAsync()
        {
            // 如果还没有初始化语音设置，先加载
            if (!_isVoiceInitialized)
            {
                await LoadSavedWindowsTtsVoiceAsync();
            }

            // 如果有保存的语音设置且与当前不同，则应用
            if (!string.IsNullOrEmpty(_currentWindowsTtsVoice))
            {
                var currentVoice = _windowsTtsService?.CurrentVoice;
                if (currentVoice == null || currentVoice.Name != _currentWindowsTtsVoice)
                {
                    _windowsTtsService?.SelectVoice(_currentWindowsTtsVoice);
                    System.Diagnostics.Debug.WriteLine($"应用保存的语音: {_currentWindowsTtsVoice}");
                }
            }
        }

        /// <summary>
        /// 停止朗读
        /// </summary>
        public void Stop()
        {
            while (_queue.TryDequeue(out _)) { }

            // 停止Windows TTS
            _windowsTtsService?.Stop();

            // 停止Flite音频播放
            try
            {
                _mediaPlayer?.Pause();
            }
            catch { }

            IsSpeaking = false;
        }

        /// <summary>
        /// 暂停朗读
        /// </summary>
        public void Pause()
        {
            lock (_lockObject)
            {
                if (!_isPaused && _isSpeaking)
                {
                    _isPaused = true;
                    _windowsTtsService?.Pause();
                    try
                    {
                        _mediaPlayer?.Pause();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 恢复朗读
        /// </summary>
        public void Resume()
        {
            lock (_lockObject)
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    _windowsTtsService?.Resume();
                    try
                    {
                        _mediaPlayer?.Play();
                    }
                    catch { }
                }
            }
        }

        public void Dispose()
        {
            _lifetimeCts?.Cancel();
            try { _worker?.Wait(500); } catch { }
            _lifetimeCts?.Dispose();
            Stop();
            _windowsTtsService?.Dispose();
        }
    }
}
