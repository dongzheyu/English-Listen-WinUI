using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.Media.Playback;
using Windows.Foundation;
using VoiceGender = English_Listen_WinUI.Models.VoiceGender;

namespace English_Listen_WinUI.Services
{
    /// <summary>
    /// Windows系统自带TTS语音引擎封装
    /// 使用Windows.Media.SpeechSynthesis API (WinRT)
    /// 支持Windows 11 24H2+的高质量语音合成
    /// </summary>
    public class WindowsTtsService : IDisposable
    {
        private readonly SpeechSynthesizer _synthesizer;
        private readonly MediaPlayer _mediaPlayer;
        private readonly object _lockObject = new object();
        private bool _isInitialized;
        private bool _isSpeaking;
        private bool _isPaused;

        // 可用的语音列表
        private List<Models.VoiceInfo> _availableVoices = new List<Models.VoiceInfo>();
        
        // 当前选中的语音
        private Models.VoiceInfo? _currentVoice;

        // 属性
        public bool IsSpeaking 
        { 
            get { lock (_lockObject) { return _isSpeaking; } }
            private set { lock (_lockObject) { _isSpeaking = value; } }
        }

        public bool IsInitialized => _isInitialized;
        public Models.VoiceInfo[] AvailableVoices => _availableVoices.ToArray();
        public Models.VoiceInfo? CurrentVoice => _currentVoice;

        public WindowsTtsService()
        {
            _synthesizer = new SpeechSynthesizer();
            _mediaPlayer = new MediaPlayer();
            InitializeService();
        }

        /// <summary>
        /// 初始化TTS服务
        /// </summary>
        private void InitializeService()
        {
            try
            {
                // 获取所有可用语音
                LoadAvailableVoices();
                
                // 设置默认语音
                SetDefaultVoice();

                // 注册媒体播放事件 - 简化处理
                // 注意：由于WinRT事件类型转换的复杂性，我们在SpeakAsync方法中直接处理播放完成
                // 如果需要更复杂的事件处理，可以考虑其他方法

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"Windows TTS服务初始化完成，找到 {_availableVoices.Count} 个语音");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Windows TTS服务初始化失败: {ex.Message}");
                _isInitialized = false;
            }
        }

        /// <summary>
        /// 加载可用的语音
        /// </summary>
        private void LoadAvailableVoices()
        {
            try
            {
                _availableVoices.Clear();
                
                var voices = SpeechSynthesizer.AllVoices;
                
                foreach (var voice in voices)
                {
                    if (voice != null)
                    {
                        // 检查是否为中文语音或英文语音
                        var language = voice.Language;
                        if (language.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) || 
                            language.StartsWith("en-", StringComparison.OrdinalIgnoreCase))
                        {
                        var voiceInfo = new Models.VoiceInfo
                        {
                            Name = voice.DisplayName,
                            DisplayName = voice.DisplayName,
                            Culture = language,
                            Gender = ConvertGender(voice.Gender),
                            Engine = "WindowsTTS"
                        };
                            
                            _availableVoices.Add(voiceInfo);
                        }
                    }
                }

                // 按语言排序：中文在前，英文在后
                _availableVoices = _availableVoices.OrderBy(v => 
                {
                    if (v.Culture?.StartsWith("zh-") == true) return 0;
                    if (v.Culture?.StartsWith("en-") == true) return 1;
                    return 2;
                }).ThenBy(v => v.DisplayName).ToList();

                System.Diagnostics.Debug.WriteLine($"找到 {_availableVoices.Count} 个语音:");
                foreach (var voice in _availableVoices)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {voice.DisplayName} ({voice.Culture}, {voice.Gender})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载语音列表失败: {ex.Message}");
                _availableVoices.Clear();
            }
        }

        /// <summary>
        /// 设置默认语音
        /// </summary>
        private void SetDefaultVoice()
        {
            try
            {
                if (_availableVoices.Count > 0)
                {
                    // 优先选择中文语音中的Microsoft Xiaoxiao或其他Microsoft语音
                    var chineseVoice = _availableVoices.FirstOrDefault(v => 
                        v.Culture?.StartsWith("zh-") == true);
                    
                    if (chineseVoice != null)
                    {
                        _currentVoice = chineseVoice;
                        _synthesizer.Voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => 
                            v?.DisplayName == chineseVoice.DisplayName);
                    }
                    else
                    {
                        // 如果没有中文语音，使用第一个可用的
                        _currentVoice = _availableVoices[0];
                        _synthesizer.Voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => 
                            v?.DisplayName == _availableVoices[0].DisplayName);
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"默认语音设置为: {_currentVoice.DisplayName}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("未找到合适的语音，将使用系统默认语音");
                    _currentVoice = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置默认语音失败: {ex.Message}");
                _currentVoice = null;
            }
        }

        /// <summary>
        /// 转换语音性别
        /// </summary>
        private Models.VoiceGender ConvertGender(Windows.Media.SpeechSynthesis.VoiceGender voiceGender)
        {
            return voiceGender switch
            {
                Windows.Media.SpeechSynthesis.VoiceGender.Male => Models.VoiceGender.Male,
                Windows.Media.SpeechSynthesis.VoiceGender.Female => Models.VoiceGender.Female,
                _ => Models.VoiceGender.Neutral
            };
        }

        /// <summary>
        /// 选择指定语音
        /// </summary>
        public bool SelectVoice(string voiceName)
        {
            try
            {
                if (string.IsNullOrEmpty(voiceName))
                    return false;

                var voice = _availableVoices.FirstOrDefault(v => 
                    v.DisplayName.Equals(voiceName, StringComparison.OrdinalIgnoreCase));

                if (voice != null)
                {
                    var windowsVoice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => 
                        v?.DisplayName.Equals(voiceName, StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (windowsVoice != null)
                    {
                        _synthesizer.Voice = windowsVoice;
                        _currentVoice = voice;
                        System.Diagnostics.Debug.WriteLine($"语音切换为: {voiceName}");
                        return true;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"未找到指定语音: {voiceName}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换语音失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步朗读文本
        /// </summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !_isInitialized)
                return;

            try
            {
                IsSpeaking = true;
                
                // 合成语音到流
                var stream = await _synthesizer.SynthesizeTextToStreamAsync(text);
                
                // 使用MediaPlayer播放
                _mediaPlayer.SetStreamSource(stream);
                _mediaPlayer.Play();
                
                // 等待播放完成
                var tcs = new TaskCompletionSource<bool>();
                
                // 订阅播放结束事件
                void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
                {
                    // 如果处于暂停状态，不触发完成事件
                    if (_isPaused) return;
                    
                    if (sender.PlaybackState == MediaPlaybackState.None || 
                        (sender.PlaybackState == MediaPlaybackState.Paused && sender.Position >= sender.NaturalDuration))
                    {
                        tcs.TrySetResult(true);
                    }
                }
                
                _mediaPlayer.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;
                
                try
                {
                    // 等待播放完成或超时（根据音频长度动态设置超时时间）
                    var audioLengthMs = _mediaPlayer.PlaybackSession?.NaturalDuration.TotalMilliseconds ?? 5000;
                    var timeout = Math.Max(audioLengthMs + 1000, 10000); // 音频长度+1秒，最少10秒
                    
                    await Task.WhenAny(tcs.Task, Task.Delay((int)timeout));
                }
                finally
                {
                    _mediaPlayer.PlaybackSession.PlaybackStateChanged -= PlaybackSession_PlaybackStateChanged;
                }
                
                // 额外等待确保音频完全播放完毕
                await Task.Delay(500);
                tcs.TrySetResult(true);
                await tcs.Task;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"朗读异常: {ex.Message}");
            }
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
            try
            {
                _mediaPlayer.Pause();
                IsSpeaking = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止朗读失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 暂停朗读
        /// </summary>
        public void Pause()
        {
            try
            {
                _isPaused = true;
                _mediaPlayer.Pause();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"暂停朗读失败: {ex.Message}");
                _isPaused = false;
            }
        }

        /// <summary>
        /// 恢复朗读
        /// </summary>
        public void Resume()
        {
            try
            {
                _isPaused = false;
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复朗读失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前语音信息
        /// </summary>
        public string GetCurrentVoiceInfo()
        {
            if (_currentVoice != null)
            {
                return $"当前语音: {_currentVoice.DisplayName} ({_currentVoice.Culture}, {_currentVoice.Gender})";
            }
            return "使用系统默认语音";
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (IsSpeaking)
                {
                    Stop();
                }
                
                // 注意：由于我们使用了lambda表达式注册事件，
                // 这里无法直接移除它们，但MediaPlayer和SpeechSynthesizer会在Dispose时清理
                _synthesizer.Dispose();
                _mediaPlayer.Dispose();
                
                System.Diagnostics.Debug.WriteLine("Windows TTS服务已释放");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放Windows TTS服务失败: {ex.Message}");
            }
        }
    }
}