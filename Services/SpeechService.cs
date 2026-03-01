using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace English_Listen_WinUI.Services
{
    public class SpeechService : IDisposable
    {
        private SpeechSynthesizer? _synthesizer;
        private bool _isFliteAvailable;
        private string _flitePath = string.Empty;
        private bool _isSpeaking;
        private readonly object _lockObject = new();

        public bool IsSpeaking
        {
            get { lock (_lockObject) { return _isSpeaking; } }
            private set { lock (_lockObject) { _isSpeaking = value; } }
        }

        public SpeechService()
        {
            InitializeSynthesizer();
            CheckFlite();
        }

        private void InitializeSynthesizer()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer();
                _synthesizer.SetOutputToDefaultAudioDevice();
            }
            catch
            {
                _synthesizer = null;
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

        public List<string> GetAvailableVoices()
        {
            var voices = new List<string>();
            try
            {
                if (_synthesizer != null)
                {
                    foreach (var voice in _synthesizer.GetInstalledVoices())
                    {
                        if (voice.Enabled)
                        {
                            voices.Add(voice.VoiceInfo.Name);
                        }
                    }
                }
            }
            catch { }
            return voices;
        }

        public void SetVoice(string voiceName)
        {
            try
            {
                if (_synthesizer != null)
                {
                    _synthesizer.SelectVoice(voiceName);
                }
            }
            catch { }
        }

        public async Task SpeakAsync(string text, int engine = 0)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                if (engine == 1 && _isFliteAvailable && File.Exists(_flitePath))
                {
                    await SpeakWithFliteAsync(text);
                }
                else
                {
                    await SpeakWithSapiAsync(text);
                }
            }
            catch { }
        }

        private Task SpeakWithSapiAsync(string text)
        {
            return Task.Run(() =>
            {
                try
                {
                    IsSpeaking = true;
                    _synthesizer?.Speak(text);
                }
                finally
                {
                    IsSpeaking = false;
                }
            });
        }

        private async Task SpeakWithFliteAsync(string text)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"el_temp_{Guid.NewGuid():N}.wav");
            try
            {
                IsSpeaking = true;
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _flitePath,
                        Arguments = $"-t \"{text}\" -o \"{tempFile}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                if (File.Exists(tempFile))
                {
                    using var player = new System.Media.SoundPlayer(tempFile);
                    player.PlaySync();
                    File.Delete(tempFile);
                }
            }
            catch
            {
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

        public void Dispose()
        {
            Stop();
            _synthesizer?.Dispose();
        }
    }
}
