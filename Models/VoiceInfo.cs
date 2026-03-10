using System;
using System.Globalization;

namespace English_Listen_WinUI.Models
{
    /// <summary>
    /// 语音信息模型 - 兼容Windows TTS和Flite
    /// </summary>
    public class VoiceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Culture { get; set; }
        public VoiceGender Gender { get; set; } = VoiceGender.Neutral;
        public string Engine { get; set; } = "Flite"; // "WindowsTTS" or "Flite"
        public bool IsDefault { get; set; }
        public string Description => $"{DisplayName} ({Culture ?? "en-US"}, {Gender})";
    }

    public enum VoiceGender
    {
        Male,
        Female,
        Neutral
    }

    /// <summary>
    /// 语音引擎类型
    /// </summary>
    public enum SpeechEngineType
    {
        Auto,
        Flite,
        WindowsTTS
    }

    /// <summary>
    /// 语音引擎配置
    /// </summary>
    public class SpeechEngineConfig
    {
        public string EngineType { get; set; } = "Auto"; // "Auto", "Flite", "WindowsTTS"
        public string? VoiceName { get; set; }
        public int Volume { get; set; } = 100; // 0-100
        public int Rate { get; set; } = 0; // -10 to 10 for Windows TTS
        
        /// <summary>
        /// 获取引擎显示名称
        /// </summary>
        public string GetEngineDisplayName()
        {
            return EngineType switch
            {
                "Auto" => "自动选择",
                "Flite" => "Flite 语音引擎",
                "WindowsTTS" => "Windows 系统语音",
                _ => "未知引擎"
            };
        }
    }
}