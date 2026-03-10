using System;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace TestSpeechSynthesis
{
    class Program
    {
        static async Task TestMain(string[] args)
        {
            try
            {
                Console.WriteLine("Testing Windows.Media.SpeechSynthesis...");
                
                // 创建语音合成器
                var synthesizer = new SpeechSynthesizer();
                
                // 获取可用语音
                var voices = SpeechSynthesizer.AllVoices;
                Console.WriteLine($"Available voices: {voices.Count}");
                foreach (var voice in voices)
                {
                    Console.WriteLine($"- {voice.DisplayName}");
                }
                
                // 测试语音合成
                var text = "Hello, this is a test of Windows Media Speech Synthesis.";
                Console.WriteLine($"Synthesizing: {text}");
                
                var stream = await synthesizer.SynthesizeTextToStreamAsync(text);
                
                // 使用MediaPlayer播放
                var mediaPlayer = new MediaPlayer();
                mediaPlayer.SetStreamSource(stream);
                mediaPlayer.Play();
                
                Console.WriteLine("Speech synthesis test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}