using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using English_Listen_WinUI.Models;

namespace English_Listen_WinUI.Services
{
    public class SpeechService : IDisposable
    {
        private string _engineType = "SAPI";
        private bool _isSpeaking;
        private bool _isPaused;
        private SpeechSynthesizer? _synthesizer;
        private bool _hasAudioDevice = false;
        private readonly object _synthesizerLock = new object();

        public bool IsSpeaking
        {
            get { return _isSpeaking; }
            private set { _isSpeaking = value; }
        }

        public string EngineType
        {
            get => _engineType;
            set => _engineType = value;
        }

        public bool IsWindowsTtsAvailable => _synthesizer != null;

        public bool HasAudioDevice => _hasAudioDevice;

        public string AudioDeviceStatus => _hasAudioDevice ? "音频设备正常" : "未检测到音频设备";

        public SpeechService()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                try
                {
                    _synthesizer.SetOutputToDefaultAudioDevice();
                    _hasAudioDevice = true;
                    Debug.WriteLine("SpeechService: 音频设备初始化成功");
                }
                catch (Exception ex)
                {
                    _hasAudioDevice = false;
                    Debug.WriteLine($"SpeechService SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    _synthesizer?.Dispose();
                    _synthesizer = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SpeechService 初始化失败: {ex.Message}");
                _synthesizer = null;
                _hasAudioDevice = false;
            }
        }

        public bool CheckAudioDeviceAvailable()
        {
            lock (_synthesizerLock)
            {
                if (_synthesizer == null) return false;

                try
                {
                    _synthesizer.SetOutputToDefaultAudioDevice();
                    _hasAudioDevice = true;
                    return true;
                }
                catch
                {
                    _hasAudioDevice = false;
                    return false;
                }
            }
        }

        public Dictionary<string, string> GetAvailableEngines()
        {
            return new Dictionary<string, string>
            {
                { "SAPI", "Windows SAPI 语音引擎" }
            };
        }

        public string GetRecommendedEngine()
        {
            return "SAPI";
        }

        public Models.VoiceInfo[] GetWindowsTtsVoices()
        {
            var voices = new List<Models.VoiceInfo>();
            if (_synthesizer == null) return voices.ToArray();

            try
            {
                lock (_synthesizerLock)
                {
                    var installedVoices = _synthesizer.GetInstalledVoices();
                    foreach (var voice in installedVoices)
                    {
                        if (voice != null && voice.Enabled)
                        {
                            var info = voice.VoiceInfo;
                            voices.Add(new Models.VoiceInfo
                            {
                                Name = info.Name,
                                DisplayName = info.Name,
                                Culture = info.Culture.Name,
                                Gender = Models.VoiceGender.Female,
                                Engine = "SAPI"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取语音列表失败: {ex.Message}");
            }
            return voices.ToArray();
        }

        public bool SetWindowsTtsEnglishVoice(string voiceName)
        {
            if (_synthesizer == null || string.IsNullOrEmpty(voiceName)) return false;

            try
            {
                lock (_synthesizerLock)
                {
                    _synthesizer.SelectVoice(voiceName);
                    Debug.WriteLine($"英文语音已设置为: {voiceName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置英文语音失败: {ex.Message}");
                return false;
            }
        }

        public bool SetWindowsTtsChineseVoice(string voiceName)
        {
            if (_synthesizer == null || string.IsNullOrEmpty(voiceName)) return false;

            try
            {
                lock (_synthesizerLock)
                {
                    _synthesizer.SelectVoice(voiceName);
                    Debug.WriteLine($"中文语音已设置为: {voiceName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置中文语音失败: {ex.Message}");
                return false;
            }
        }

        public Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;

            try
            {
                IsSpeaking = true;
                _synthesizer?.Speak(text);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"朗读失败: {ex.Message}");
            }
            finally
            {
                IsSpeaking = false;
            }
            return Task.CompletedTask;
        }

        public async Task SpeakAsync(string text, string voiceName, bool isEnglish)
        {
            if (string.IsNullOrWhiteSpace(text) || _synthesizer == null) return;

            try
            {
                if (!string.IsNullOrEmpty(voiceName))
                {
                    try
                    {
                        _synthesizer.SelectVoice(voiceName);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"无法设置语音 {voiceName}，使用默认语音: {ex.Message}");
                    }
                }

                IsSpeaking = true;
                await Task.Run(() =>
                {
                    try
                    {
                        lock (_synthesizerLock)
                        {
                            if (_synthesizer != null)
                            {
                                _synthesizer.Speak(text);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"语音播放失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"朗读失败: {ex.Message}");
            }
            finally
            {
                IsSpeaking = false;
            }
        }

        public void Stop()
        {
            try
            {
                lock (_synthesizerLock)
                {
                    _synthesizer?.SpeakAsyncCancelAll();
                }
            }
            catch { }
            IsSpeaking = false;
        }

        public void Pause()
        {
            if (!_isPaused && _isSpeaking)
            {
                _isPaused = true;
            }
        }

        public void Resume()
        {
            if (_isPaused)
            {
                _isPaused = false;
            }
        }

        public void Dispose()
        {
            if (_synthesizer != null)
            {
                _synthesizer.Dispose();
                _synthesizer = null;
            }
        }
    }
}