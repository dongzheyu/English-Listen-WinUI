using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using VoiceGender = English_Listen_WinUI.Models.VoiceGender;
using VoiceInfo = English_Listen_WinUI.Models.VoiceInfo;

namespace English_Listen_WinUI.Services
{
    public class SpeechService : IDisposable
    {
        private readonly object _synthesizerLock = new object();
        private string _engineType = "SAPI";
        private bool _hasAudioDevice = false;
        private bool _isPaused;
        private bool _isSpeaking;
        private SpeechSynthesizer? _synthesizer;

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

        public void Dispose()
        {
            if (_synthesizer != null)
            {
                _synthesizer.Dispose();
                _synthesizer = null;
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

        public VoiceInfo[] GetWindowsTtsVoices()
        {
            var voices = new List<VoiceInfo>();
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
                            var name = info.Name;
                            var id = info.Id ?? "";
                            // NaturalVoiceSAPIAdapter: Id starts with "Local-" (Narrator natural voices)
                            // Regular SAPI: Id starts with "TTS_" (e.g. TTS_MS_EN-US_ZIRA_11.0)
                            var isNatural = id.StartsWith("Local-", StringComparison.OrdinalIgnoreCase)
                                            || name.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0;
                            voices.Add(new VoiceInfo
                            {
                                Name = name,
                                DisplayName = name,
                                Culture = info.Culture.Name,
                                Gender = VoiceGender.Female,
                                IsNatural = isNatural
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

        private static bool IsNaturalVoiceId(string id)
        {
            return !string.IsNullOrEmpty(id) &&
                   id.StartsWith("Local-", StringComparison.OrdinalIgnoreCase);
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

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_synthesizer == null) return;

            if (_isPaused) return;

            IsSpeaking = true;
            try
            {
                using (cancellationToken.Register(() =>
                       {
                           try
                           {
                               _synthesizer?.SpeakAsyncCancelAll();
                           }
                           catch
                           {
                           }
                       }))
                {
                    var tcs = new TaskCompletionSource<bool>();
                    EventHandler<SpeakCompletedEventArgs>? handler = null;
                    handler = (sender, args) =>
                    {
                        _synthesizer!.SpeakCompleted -= handler;
                        tcs.TrySetResult(true);
                    };
                    _synthesizer.SpeakCompleted += handler;

                    try
                    {
                        _synthesizer.SpeakAsync(text);
                        cancellationToken.ThrowIfCancellationRequested();
                        await tcs.Task;
                    }
                    finally
                    {
                        _synthesizer.SpeakCompleted -= handler;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("朗读被取消");
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
                var tcs = new TaskCompletionSource<bool>();
                EventHandler<SpeakCompletedEventArgs>? handler = null;
                handler = (sender, args) =>
                {
                    _synthesizer!.SpeakCompleted -= handler;
                    tcs.TrySetResult(true);
                };
                _synthesizer.SpeakCompleted += handler;

                try
                {
                    _synthesizer.SpeakAsync(text);
                    await tcs.Task;
                }
                finally
                {
                    _synthesizer.SpeakCompleted -= handler;
                }
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
            catch
            {
            }

            IsSpeaking = false;
        }

        public void Pause()
        {
            if (!_isPaused && _isSpeaking)
            {
                _isPaused = true;
                try
                {
                    _synthesizer?.SpeakAsyncCancelAll();
                }
                catch
                {
                }
            }
        }

        public void Resume()
        {
            if (_isPaused)
            {
                _isPaused = false;
            }
        }

        // ponytail: debug, remove after NaturalVoiceSAPIAdapter detection stable
        public string[] DumpRawVoiceInfo()
        {
            var lines = new List<string>();
            lock (_synthesizerLock)
            {
                if (_synthesizer == null) return lines.ToArray();
                try
                {
                    var installed = _synthesizer.GetInstalledVoices();
                    foreach (var v in installed)
                    {
                        if (v?.VoiceInfo == null) continue;
                        var info = v.VoiceInfo;
                        var addInfo = string.Join("; ",
                            info.AdditionalInfo?.Select(kv => $"{kv.Key}={kv.Value}") ?? Enumerable.Empty<string>());
                        var isNatural = IsNaturalVoiceId(info.Id);
                        lines.Add(
                            $"Name=[{info.Name}] Id=[{info.Id}] Culture=[{info.Culture?.Name}] " +
                            $"IsNatural={isNatural} AddInfo=[{addInfo}]");
                    }
                }
                catch (Exception ex)
                {
                    lines.Add($"Error: {ex.Message}");
                }
            }

            return lines.ToArray();
        }
    }
}