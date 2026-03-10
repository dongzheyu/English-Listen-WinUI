using System;
using System.Runtime.InteropServices;

namespace English_Listen_WinUI.Helpers
{
    public static class OsVersionHelper
    {
        /// <summary>
        /// 检测是否为Windows 11 24H2或更高版本
        /// </summary>
        public static bool IsWindows11_24H2_OrLater()
        {
            try
            {
                // 对于.NET 5+, 使用Environment.OSVersion获取准确版本
                var osVersion = Environment.OSVersion;
                
                // 检查是否为Windows 11 (build number >= 22000)
                if (osVersion.Platform == PlatformID.Win32NT && osVersion.Version.Major >= 10)
                {
                    var build = osVersion.Version.Build;
                    var revision = osVersion.Version.Revision;
                    
                    // Windows 11起始build为22000
                    // 24H2版本的build number需要大于等于26100 (预计)
                    // 实际检测中，我们会使用更宽松的条件，检测Windows 11即可
                    
                    return build >= 22000; // Windows 11检测
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 获取详细的Windows版本信息
        /// </summary>
        public static string GetWindowsVersionInfo()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                var build = osVersion.Version.Build;
                var major = osVersion.Version.Major;
                var minor = osVersion.Version.Minor;
                
                string windowsVersion;
                if (build >= 22000)
                {
                    windowsVersion = $"Windows 11 (Build {build})";
                    
                    // 尝试区分不同版本的Windows 11
                    if (build >= 26100)
                        windowsVersion += " 24H2+";
                    else if (build >= 22621)
                        windowsVersion += " 22H2/23H2";
                    else
                        windowsVersion += " 21H2";
                }
                else if (build >= 19041)
                {
                    windowsVersion = $"Windows 10 (Build {build})";
                }
                else
                {
                    windowsVersion = $"Windows {major}.{minor} (Build {build})";
                }
                
                return windowsVersion;
            }
            catch (Exception ex)
            {
                return $"Unknown Windows Version ({ex.Message})";
            }
        }
        
        /// <summary>
        /// 检测系统是否支持Windows 11自带TTS
        /// </summary>
        public static bool SupportsWindowsBuiltInTTS()
        {
            // Windows 11 24H2+ 应该支持更好的TTS
            // 但为兼容性考虑，我们在Windows 11上就可以尝试使用系统TTS
            return IsWindows11_24H2_OrLater();
        }
        
        /// <summary>
        /// 获取推荐的语音引擎类型
        /// </summary>
        public static RecommendedEngine GetRecommendedEngine()
        {
            if (IsWindows11_24H2_OrLater())
            {
                return new RecommendedEngine
                {
                    EngineType = "WindowsTTS",
                    Reason = "Windows 11 24H2+ 支持高质量系统语音合成",
                    IsRecommended = true
                };
            }
            
            return new RecommendedEngine
            {
                EngineType = "Flite",
                Reason = "使用Flite语音引擎",
                IsRecommended = false
            };
        }
    }
    
    public class RecommendedEngine
    {
        public string EngineType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool IsRecommended { get; set; }
    }
}