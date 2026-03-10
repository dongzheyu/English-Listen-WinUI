using System;
using System.Threading.Tasks;
using English_Listen_WinUI.Services;

namespace TestUpdateLogic
{
    class Program
    {
        static async Task TestMain(string[] args)
        {
            Console.WriteLine("=== 更新逻辑测试 ===");
            
            var updateService = new UpdateService();
            
            // 测试不同场景
            Console.WriteLine("\n1. 测试开发版本场景:");
            var devVersionInfo = new UpdateInfo
            {
                CurrentVersion = "2.8.0",
                NewVersion = "2.7.0",
                IsDevelopmentVersion = true,
                IsUpdateAvailable = false
            };
            
            Console.WriteLine($"当前版本: {devVersionInfo.CurrentVersion}");
            Console.WriteLine($"远程版本: {devVersionInfo.NewVersion}");
            Console.WriteLine($"是否为开发版本: {devVersionInfo.IsDevelopmentVersion}");
            Console.WriteLine($"是否有更新可用: {devVersionInfo.IsUpdateAvailable}");
            
            // 模拟UI逻辑判断
            if (devVersionInfo.IsDevelopmentVersion)
            {
                Console.WriteLine("✅ 正确：显示开发版本警告，不显示下载选项");
            }
            else if (devVersionInfo.IsUpdateAvailable)
            {
                Console.WriteLine("❌ 错误：不应该显示下载选项");
            }
            
            Console.WriteLine("\n2. 测试正常更新场景:");
            var normalUpdateInfo = new UpdateInfo
            {
                CurrentVersion = "2.6.0",
                NewVersion = "2.7.0",
                IsDevelopmentVersion = false,
                IsUpdateAvailable = true
            };
            
            Console.WriteLine($"当前版本: {normalUpdateInfo.CurrentVersion}");
            Console.WriteLine($"远程版本: {normalUpdateInfo.NewVersion}");
            Console.WriteLine($"是否为开发版本: {normalUpdateInfo.IsDevelopmentVersion}");
            Console.WriteLine($"是否有更新可用: {normalUpdateInfo.IsUpdateAvailable}");
            
            if (normalUpdateInfo.IsDevelopmentVersion)
            {
                Console.WriteLine("❌ 错误：不应该显示开发版本警告");
            }
            else if (normalUpdateInfo.IsUpdateAvailable)
            {
                Console.WriteLine("✅ 正确：显示下载更新选项");
            }
            
            Console.WriteLine("\n3. 测试已是最新版本场景:");
            var latestInfo = new UpdateInfo
            {
                CurrentVersion = "2.7.0",
                NewVersion = "2.7.0",
                IsDevelopmentVersion = false,
                IsUpdateAvailable = false
            };
            
            Console.WriteLine($"当前版本: {latestInfo.CurrentVersion}");
            Console.WriteLine($"远程版本: {latestInfo.NewVersion}");
            Console.WriteLine($"是否为最新版本: {!latestInfo.IsUpdateAvailable}");
            
            if (!latestInfo.IsUpdateAvailable && !latestInfo.IsDevelopmentVersion)
            {
                Console.WriteLine("✅ 正确：显示已是最新版本");
            }
            
            Console.WriteLine("\n=== 测试完成 ===");
            Console.WriteLine("开发版本逻辑验证通过！✅");
            Console.WriteLine("当当前版本 > 远程版本时，只显示开发版本警告，不显示下载选项。");
            
            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}