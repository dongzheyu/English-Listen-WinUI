using System;
using System.Threading.Tasks;
using English_Listen_WinUI.Helpers;
using English_Listen_WinUI.Services;

namespace TestWindowsTTS
{
    class Program
    {
        static async Task TestMain(string[] args)
        {
            Console.WriteLine("=== Windows 11 TTS 功能测试 ===");
            
            // 测试OS版本检测
            Console.WriteLine($"操作系统版本: {OsVersionHelper.GetWindowsVersionInfo()}");
            Console.WriteLine($"是否支持Windows TTS: {OsVersionHelper.SupportsWindowsBuiltInTTS()}");
            
            // 测试语音服务
            try
            {
                Console.WriteLine("\n正在初始化语音服务...");
                var speechService = new SpeechService();
                
                Console.WriteLine($"SAPI引擎: 可用");
                Console.WriteLine($"Windows TTS可用: {speechService.IsWindowsTtsAvailable}");
                Console.WriteLine($"推荐引擎: {speechService.GetRecommendedEngine()}");
                
                var engines = speechService.GetAvailableEngines();
                Console.WriteLine($"可用引擎数量: {engines.Count}");
                foreach (var engine in engines)
                {
                    Console.WriteLine($"  - {engine.Key}: {engine.Value}");
                }
                
                // 如果Windows TTS可用，测试语音列表
                if (speechService.IsWindowsTtsAvailable)
                {
                    Console.WriteLine("\n正在获取Windows TTS语音列表...");
                    var voices = speechService.GetWindowsTtsVoices();
                    Console.WriteLine($"找到 {voices.Length} 个语音:");
                    foreach (var voice in voices)
                    {
                        Console.WriteLine($"  - {voice.DisplayName} ({voice.Culture}, {voice.Gender})");
                    }
                }
                
                // 测试语音合成
                Console.WriteLine("\n正在测试语音合成...");
                Console.WriteLine("测试内容: Hello, this is a test of Windows 11 speech synthesis.");
                
                speechService.EngineType = "WindowsTTS";
                await speechService.SpeakAsync("Hello, this is a test of Windows 11 speech synthesis.");
                
                Console.WriteLine("语音合成测试完成！");
                
                // 等待播放完成
                await Task.Delay(3000);
                
                speechService.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"测试失败: {ex.Message}");
                Console.WriteLine($"详细错误: {ex.StackTrace}");
            }
            
            Console.WriteLine("\n测试完成。按任意键退出...");
            Console.ReadKey();
        }
    }
}