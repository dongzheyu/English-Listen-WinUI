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

        public SpeechService()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                try
                {
                    _synthesizer.SetOutputToDefaultAudioDevice();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"SpeechService SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SpeechService 初始化失败: {ex.Message}");
                _synthesizer = null;
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
                _synthesizer.SelectVoice(voiceName);
                Debug.WriteLine($"英文语音已设置为: {voiceName}");
                return true;
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
                _synthesizer.SelectVoice(voiceName);
                Debug.WriteLine($"中文语音已设置为: {voiceName}");
                return true;
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
                try
                {
                    _synthesizer.SelectVoice(voiceName);
                }
                catch
                {
                    Debug.WriteLine($"无法设置语音 {voiceName}，使用默认语音");
                }

                IsSpeaking = true;
                await Task.Run(() => _synthesizer.Speak(text));
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
                _synthesizer?.SpeakAsyncCancelAll();
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