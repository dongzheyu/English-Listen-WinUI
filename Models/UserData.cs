using System;
using System.Collections.Generic;

namespace English_Listen_WinUI.Models
{
    public class TestResult
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int TotalWords { get; set; }
        public int CorrectCount { get; set; }
        public double Accuracy { get; set; }
        public string WordListName { get; set; } = string.Empty;
    }

    public class UserData
    {
        public string Username { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime LastLoginTime { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        public List<string> WordLists { get; set; } = new();
        public List<TestResult> TestHistory { get; set; } = new();
        public int TotalStudyTime { get; set; }
        public int CompletedTests { get; set; }

        public bool IsDarkTheme { get; set; }
        public int ReadInterval { get; set; } = 5;
        public int SpeechEngine { get; set; }
        public bool IsRandomOrder { get; set; }
    }

    public class AppSettings
    {
        public int ThemeMode { get; set; } // 0 = Light, 1 = Dark, 2 = System
        public int ReadInterval { get; set; } = 5;
        public int SpeechEngine { get; set; }
        public string FliteVoiceModel { get; set; } = "cmu_us_slt";
        public bool IsRandomOrder { get; set; } = true;
        public string WordlistDirPath { get; set; } = "./wordlist";
        public string? CurrentUser { get; set; }
        public string SpeechEngineType { get; set; } = "Auto"; // "Auto", "Flite", "WindowsTTS"
        public string? WindowsTtsEnglishVoiceName { get; set; }
        public string? WindowsTtsChineseVoiceName { get; set; }
        public int WindowsTtsVolume { get; set; } = 100; // 0-100
        public int WindowsTtsRate { get; set; } = 0; // -10 to 10
        public string? BaiduTranslateApiMode { get; set; } = "default";
        public string? BaiduTranslateApiKey { get; set; }
    }


}
