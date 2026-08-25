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
        private const int MaxSpeechTextLength = 5000;
        private readonly object _synthesizerLock = new();
        private readonly SemaphoreSlim _speechGate = new(1, 1);
        private string _engineType = "SAPI";
        private bool _hasAudioDevice;
        private bool _isPaused;
        private bool _isSpeaking;
        private bool _disposed;
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
                }
                catch (Exception ex)
                {
                    _hasAudioDevice = false;
                    Debug.WriteLine($"SpeechService SetOutputToDefaultAudioDevice 失败: {ex.Message}");
                    _synthesizer.Dispose();
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
            get => _isSpeaking;
            private set => _isSpeaking = value;
        }

        public string EngineType
        {
            get => _engineType;
            set => _engineType = value;
        }

        public bool IsWindowsTtsAvailable
        {
            get
            {
                lock (_synthesizerLock)
                {
                    return !_disposed && _synthesizer != null;
                }
            }
        }

        public bool HasAudioDevice => _hasAudioDevice;
        public string AudioDeviceStatus => _hasAudioDevice ? "音频设备正常" : "未检测到音频设备";

        public void Dispose()
        {
            lock (_synthesizerLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _isPaused = true;
                try
                {
                    _synthesizer?.SpeakAsyncCancelAll();
                }
                catch
                {
                }

                _synthesizer?.Dispose();
                _synthesizer = null;
                _hasAudioDevice = false;
            }

            _speechGate.Dispose();
        }

        public bool CheckAudioDeviceAvailable()
        {
            lock (_synthesizerLock)
            {
                if (_disposed || _synthesizer == null)
                    return false;

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
            lock (_synthesizerLock)
            {
                if (_disposed || _synthesizer == null)
                    return voices.ToArray();

                try
                {
                    foreach (var voice in _synthesizer.GetInstalledVoices())
                    {
                        if (voice == null || !voice.Enabled)
                            continue;

                        var info = voice.VoiceInfo;
                        var name = info.Name;
                        var id = info.Id ?? string.Empty;
                        var isNatural = id.StartsWith("Local-", StringComparison.OrdinalIgnoreCase)
                            || name.IndexOf("Online", StringComparison.OrdinalIgnoreCase) >= 0;

                        voices.Add(new VoiceInfo
                        {
                            Name = name,
                            DisplayName = name,
                            Culture = info.Culture?.Name ?? string.Empty,
                            Gender = VoiceGender.Female,
                            IsNatural = isNatural
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"获取语音列表失败: {ex.Message}");
                }
            }

            return voices.ToArray();
        }

        private static bool IsNaturalVoiceId(string? id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith("Local-", StringComparison.OrdinalIgnoreCase);

        public bool SetWindowsTtsEnglishVoice(string voiceName) => SelectVoice(voiceName);
        public bool SetWindowsTtsChineseVoice(string voiceName) => SelectVoice(voiceName);

        private bool SelectVoice(string voiceName)
        {
            if (string.IsNullOrWhiteSpace(voiceName) || voiceName.Length > 256)
                return false;

            lock (_synthesizerLock)
            {
                if (_disposed || _synthesizer == null)
                    return false;

                try
                {
                    _synthesizer.SelectVoice(voiceName);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"设置语音失败: {ex.Message}");
                    return false;
                }
            }
        }

        public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsValidSpeechText(text))
                return;

            await SpeakCoreAsync(text, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task SpeakAsync(string text, string voiceName, bool isEnglish)
        {
            if (!IsValidSpeechText(text))
                return;

            await SpeakCoreAsync(text, voiceName, CancellationToken.None).ConfigureAwait(false);
        }

        private static bool IsValidSpeechText(string? text) =>
            !string.IsNullOrWhiteSpace(text) && text.Length <= MaxSpeechTextLength;

        private async Task SpeakCoreAsync(string text, string? voiceName, CancellationToken cancellationToken)
        {
            await _speechGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SpeechSynthesizer? synthesizer;
                lock (_synthesizerLock)
                {
                    if (_disposed || _synthesizer == null || _isPaused)
                        return;

                    synthesizer = _synthesizer;
                    if (!string.IsNullOrWhiteSpace(voiceName))
                    {
                        try
                        {
                            synthesizer.SelectVoice(voiceName);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"无法设置语音 {voiceName}，使用默认语音: {ex.Message}");
                        }
                    }
                }

                IsSpeaking = true;
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<SpeakCompletedEventArgs>? handler = null;
                handler = (_, args) =>
                {
                    synthesizer.SpeakCompleted -= handler;
                    completion.TrySetResult(true);
                };

                synthesizer.SpeakCompleted += handler;
                try
                {
                    using var registration = cancellationToken.Register(() =>
                    {
                        try
                        {
                            synthesizer.SpeakAsyncCancelAll();
                        }
                        catch
                        {
                        }
                    });

                    synthesizer.SpeakAsync(text);
                    await completion.Task.ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                finally
                {
                    synthesizer.SpeakCompleted -= handler;
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
                _speechGate.Release();
            }
        }

        public void Stop()
        {
            lock (_synthesizerLock)
            {
                if (_disposed || _synthesizer == null)
                    return;

                try
                {
                    _synthesizer.SpeakAsyncCancelAll();
                }
                catch
                {
                }
            }

            IsSpeaking = false;
        }

        public void Pause()
        {
            lock (_synthesizerLock)
            {
                if (_disposed || _isPaused || !_isSpeaking)
                    return;

                _isPaused = true;
                try
                {
                    _synthesizer?.SpeakAsyncCancelAll();
                }
                catch
                {
                }
            }

            IsSpeaking = false;
        }

        public void Resume()
        {
            lock (_synthesizerLock)
            {
                if (!_disposed)
                    _isPaused = false;
            }
        }

        public string[] DumpRawVoiceInfo()
        {
            var lines = new List<string>();
            lock (_synthesizerLock)
            {
                if (_disposed || _synthesizer == null)
                    return lines.ToArray();

                try
                {
                    foreach (var v in _synthesizer.GetInstalledVoices())
                    {
                        if (v?.VoiceInfo == null)
                            continue;

                        var info = v.VoiceInfo;
                        var addInfo = string.Join("; ", info.AdditionalInfo?.Select(kv => $"{kv.Key}={kv.Value}") ?? Enumerable.Empty<string>());
                        lines.Add($"Name=[{info.Name}] Id=[{info.Id}] Culture=[{info.Culture?.Name}] IsNatural={IsNaturalVoiceId(info.Id)} AddInfo=[{addInfo}]");
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