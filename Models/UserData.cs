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

        public bool AllowDataCollection { get; set; }
        public bool AllowCloudSync { get; set; }
        public bool AllowAnalytics { get; set; }
        public bool ShareLearningStats { get; set; }
    }

    public class AppSettings
    {
        public bool IsDarkTheme { get; set; }
        public int ReadInterval { get; set; } = 5;
        public int SpeechEngine { get; set; }
        public bool IsRandomOrder { get; set; } = true;
        public string WordlistDirPath { get; set; } = "./wordlist";
        public string? CurrentUser { get; set; }
    }
}
