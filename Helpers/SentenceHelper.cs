using System;
using System.Collections.Generic;
using System.Linq;

namespace English_Listen_WinUI.Helpers
{
    /// <summary>
    /// 例句管理助手类
    /// </summary>
    public static class SentenceHelper
    {
        /// <summary>
        /// 常见单词的例句库
        /// </summary>
        private static readonly Dictionary<string, List<string>> WordSentences = new()
        {
            // 基础词汇
            {"the", new List<string> {
                "The sun is shining brightly today.",
                "I saw the movie last night.",
                "She is the best student in our class."
            }},
            {"and", new List<string> {
                "I like apples and oranges.",
                "She is smart and beautiful.",
                "We walked and talked for hours."
            }},
            {"have", new List<string> {
                "I have a new book to read.",
                "They have three children.",
                "Do you have any questions?"
            }},
            
            // 常见动词
            {"go", new List<string> {
                "I go to school every day.",
                "Let's go to the park together.",
                "Where did you go yesterday?"
            }},
            {"get", new List<string> {
                "I need to get some groceries.",
                "She got a promotion at work.",
                "Don't get angry with me."
            }},
            {"make", new List<string> {
                "I want to make a cake for her birthday.",
                "This machine can make coffee.",
                "She makes beautiful paintings."
            }},
            {"take", new List<string> {
                "Please take a seat.",
                "I need to take a break.",
                "He took the book from the shelf."
            }},
            {"see", new List<string> {
                "I can see the mountains from here.",
                "Did you see that movie?",
                "She sees her doctor regularly."
            }},
            {"come", new List<string> {
                "Come here, please.",
                "When will you come back?",
                "The train is coming soon."
            }},
            {"know", new List<string> {
                "I know the answer to that question.",
                "Do you know how to swim?",
                "She knows three languages."
            }},
            
            // 常见名词
            {"time", new List<string> {
                "What time is it now?",
                "I don't have time to talk.",
                "Time flies when you're having fun."
            }},
            {"year", new List<string> {
                "This year has been challenging.",
                "She graduated last year.",
                "The new year brings new opportunities."
            }},
            {"people", new List<string> {
                "Many people attended the concert.",
                "Some people prefer tea over coffee.",
                "The people here are very friendly."
            }},
            {"day", new List<string> {
                "Today is a beautiful day.",
                "I work five days a week.",
                "The day after tomorrow is Friday."
            }},
            {"man", new List<string> {
                "The man is wearing a blue suit.",
                "That man looks familiar.",
                "The old man walked slowly down the street."
            }},
            {"woman", new List<string> {
                "The woman is reading a newspaper.",
                "She is a successful business woman.",
                "That woman helped me yesterday."
            }},
            {"child", new List<string> {
                "The child is playing with toys.",
                "Every child deserves education.",
                "The children are in the garden."
            }},
            {"world", new List<string> {
                "The world is becoming smaller.",
                "She wants to travel around the world.",
                "We live in a digital world."
            }},
            {"school", new List<string> {
                "My children go to the local school.",
                "School starts at eight o'clock.",
                "She teaches at a primary school."
            }},
            
            // 形容词
            {"good", new List<string> {
                "This is a good book to read.",
                "She is a good friend of mine.",
                "The weather is good today."
            }},
            {"new", new List<string> {
                "I bought a new car yesterday.",
                "This is a new experience for me.",
                "She moved to a new apartment."
            }},
            {"first", new List<string> {
                "This is my first time here.",
                "She was the first to arrive.",
                "The first chapter is interesting."
            }},
            {"last", new List<string> {
                "This is the last bus tonight.",
                "I saw her last week.",
                "The last chapter was exciting."
            }},
            {"long", new List<string> {
                "The river is very long.",
                "She has long beautiful hair.",
                "It was a long and difficult journey."
            }},
            {"great", new List<string> {
                "We had a great time at the party.",
                "This is a great opportunity.",
                "She is doing great work."
            }},
            {"little", new List<string> {
                "The little girl is very cute.",
                "I have little time to spare.",
                "She speaks a little French."
            }},
            {"old", new List<string> {
                "The old house needs repair.",
                "My grandfather is very old.",
                "This is an old tradition."
            }}
        };

        /// <summary>
        /// 获取单词的例句
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>例句列表，如果找不到返回空列表</returns>
        public static List<string> GetSentences(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new List<string>();

            var lowerWord = word.ToLower().Trim();
            
            if (WordSentences.TryGetValue(lowerWord, out List<string>? sentences))
                return sentences;

            return new List<string>();
        }

        /// <summary>
        /// 获取单词的随机例句
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>随机例句，如果没有例句返回空字符串</returns>
        public static string GetRandomSentence(string word)
        {
            var sentences = GetSentences(word);
            if (sentences.Count == 0)
                return string.Empty;

            var random = new Random();
            return sentences[random.Next(sentences.Count)];
        }

        /// <summary>
        /// 获取单词的第一个例句
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>第一个例句，如果没有例句返回空字符串</returns>
        public static string GetFirstSentence(string word)
        {
            var sentences = GetSentences(word);
            return sentences.FirstOrDefault() ?? string.Empty;
        }

        /// <summary>
        /// 添加自定义例句
        /// </summary>
        /// <param name="word">单词</param>
        /// <param name="sentence">例句</param>
        public static void AddSentence(string word, string sentence)
        {
            if (string.IsNullOrEmpty(word) || string.IsNullOrEmpty(sentence))
                return;

            var lowerWord = word.ToLower().Trim();
            
            if (!WordSentences.ContainsKey(lowerWord))
                WordSentences[lowerWord] = new List<string>();
            
            if (!WordSentences[lowerWord].Contains(sentence))
                WordSentences[lowerWord].Add(sentence);
        }

        /// <summary>
        /// 获取包含指定单词的例句数量
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>例句数量</returns>
        public static int GetSentenceCount(string word)
        {
            var sentences = GetSentences(word);
            return sentences.Count;
        }

        /// <summary>
        /// 检查是否有指定单词的例句
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>是否有例句</returns>
        public static bool HasSentences(string word)
        {
            return GetSentenceCount(word) > 0;
        }

        /// <summary>
        /// 获取所有有例句的单词列表
        /// </summary>
        /// <returns>单词列表</returns>
        public static List<string> GetWordsWithSentences()
        {
            return new List<string>(WordSentences.Keys);
        }

        /// <summary>
        /// 获取例句统计信息
        /// </summary>
        /// <returns>统计信息字符串</returns>
        public static string GetStatistics()
        {
            int totalWords = WordSentences.Count;
            int totalSentences = WordSentences.Values.Sum(list => list.Count);
            double avgSentencesPerWord = totalWords > 0 ? (double)totalSentences / totalWords : 0;

            return $"例句统计: {totalWords} 个单词, {totalSentences} 条例句, 平均每词 {avgSentencesPerWord:F1} 条例句";
        }
    }
}