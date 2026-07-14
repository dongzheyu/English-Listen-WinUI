using System;
using System.Collections.Generic;

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

    public class WordListGroup
    {
        public string Name { get; set; } = string.Empty;
        public List<string> WordListNames { get; set; } = new();
    }
}