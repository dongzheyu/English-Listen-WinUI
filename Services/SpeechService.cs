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
        private string _engineType = "Auto"; // "Flite", "WindowsTTS", "Auto"
        private bool _isFliteAvailable;
        private string _flitePath = string.Empty;
        private bool _isSpeaking;
        private bool _isPaused;
        private MediaPlayer? _mediaPlayer;
        private WindowsTtsService? _windowsTtsService;
        private readonly object _lockObject = new();

        // 队列顺序朗读
        private readonly ConcurrentQueue<(string text, string? voiceModel)> _queue = new();
        private Task? _worker;
        private CancellationTokenSource? _lifetimeCts = new();

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
        public bool IsFliteAvailable => _isFliteAvailable;

        public SpeechService()
        {
            CheckFlite();
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

        private void CheckFlite()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var fliteExe = Path.Combine(appDir, "flite.exe");
            if (File.Exists(fliteExe))
            {
                _flitePath = fliteExe;
                _isFliteAvailable = true;
            }
        }

        private void InitializeWindowsTts()
        {
            try
            {
                // 只在Windows 11上初始化Windows TTS
                if (OsVersionHelper.IsWindows11_24H2_OrLater())
                {
                    _windowsTtsService = new WindowsTtsService();
                    System.Diagnostics.Debug.WriteLine("Windows TTS服务初始化完成");
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
            // 如果设置为Auto，根据系统版本推荐
            if (_engineType == "Auto")
            {
                if (IsWindowsTtsAvailable)
                    return "WindowsTTS";
                if (IsFliteAvailable)
                    return "Flite";
                return "None";
            }
            
            return _engineType;
        }

        /// <summary>
        /// 获取可用的引擎列表
        /// </summary>
        public Dictionary<string, string> GetAvailableEngines()
        {
            var engines = new Dictionary<string, string>();
            
            if (IsWindowsTtsAvailable)
            {
                engines["WindowsTTS"] = "Windows 11 自带语音合成";
            }
            
            if (IsFliteAvailable)
            {
                engines["Flite"] = "Flite 语音引擎";
            }
            
            if (engines.Count > 1)
            {
                engines["Auto"] = "自动选择推荐引擎";
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

        // 公共入口：仅入队，立即返回
        public Task SpeakAsync(string text, string? voiceModel = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
            _queue.Enqueue((text, voiceModel));
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
                            
                            switch (engine)
                            {
                                case "WindowsTTS":
                                    if (_windowsTtsService != null)
                                        await SpeakWithWindowsTtsAsync(item.text);
                                    else
                                        await SpeakWithFliteAsync(item.text, item.voiceModel);
                                    break;
                                    
                                case "Flite":
                                    if (_isFliteAvailable)
                                        await SpeakWithFliteAsync(item.text, item.voiceModel);
                                    break;
                                    
                                default:
                                    System.Diagnostics.Debug.WriteLine("没有可用的语音引擎");
                                    break;
                            }
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
        /// 使用Windows TTS朗读
        /// </summary>
        private async Task SpeakWithWindowsTtsAsync(string text)
        {
            if (_windowsTtsService == null) return;
            
            try
            {
                IsSpeaking = true;
                await _windowsTtsService.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS朗读失败: {ex.Message}");
                // 失败时回退到Flite
                if (_isFliteAvailable)
                    await SpeakWithFliteAsync(text, null);
            }
            finally
            {
                IsSpeaking = false;
            }
        }

        /// <summary>
        /// 使用Flite朗读（原有实现）
        /// </summary>
        private async Task SpeakWithFliteAsync(string text, string? voiceModel)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"el_temp_{Guid.NewGuid():N}.wav");
            try
            {
                IsSpeaking = true;
                var voiceArg = string.IsNullOrEmpty(voiceModel) ? "" : $"-voice {voiceModel}";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _flitePath,
                        Arguments = $"{voiceArg} -t \"{text}\" -o \"{tempFile}\"".Trim(),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (File.Exists(tempFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(tempFile);
                        if (_mediaPlayer != null)
                        {
                            _mediaPlayer.SetStreamSource(stream.AsRandomAccessStream());
                            _mediaPlayer.Play();
                            
                            // 等待播放完成
                            var tcs = new TaskCompletionSource<bool>();
                            _mediaPlayer.MediaEnded += (s, e) => tcs.TrySetResult(true);
                            await tcs.Task;
                        }
                        File.Delete(tempFile);
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                IsSpeaking = false;
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