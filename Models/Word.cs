using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace English_Listen_WinUI.Models
{
    public class Word
    {
        public string Text { get; set; } = string.Empty;
        public string? Phonetic { get; set; }
        public string? Meaning { get; set; }
        public DateTime AddedTime { get; set; } = DateTime.Now;
        public int WrongCount { get; set; }
        public int CorrectCount { get; set; }
    }

    public class WordList
    {
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime ModifiedTime { get; set; } = DateTime.Now;
        public List<Word> Words { get; set; } = new();
    }

    public class WordListGroup
    {
        public string Name { get; set; } = string.Empty;
        public List<string> WordListNames { get; set; } = new();
    }
}
