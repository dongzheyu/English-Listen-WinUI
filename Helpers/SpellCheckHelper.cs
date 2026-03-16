using System;
using System.Collections.Generic;
using System.Linq;

namespace English_Listen_WinUI.Helpers
{
    /// <summary>
    /// 拼写检查助手类，提供拼写纠错和相似单词建议
    /// </summary>
    public static class SpellCheckHelper
    {
        /// <summary>
        /// 计算两个字符串之间的编辑距离（Levenshtein距离）
        /// </summary>
        /// <param name="s">第一个字符串</param>
        /// <param name="t">第二个字符串</param>
        /// <returns>编辑距离</returns>
        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s))
                return string.IsNullOrEmpty(t) ? 0 : t.Length;
            
            if (string.IsNullOrEmpty(t))
                return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            // 初始化第一行和第一列
            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }

        /// <summary>
        /// 获取相似单词建议
        /// </summary>
        /// <param name="input">用户输入</param>
        /// <param name="correctWord">正确单词</param>
        /// <param name="maxSuggestions">最大建议数量</param>
        /// <returns>相似单词建议列表</returns>
        public static List<string> GetSimilarWordSuggestions(string input, string correctWord, int maxSuggestions = 3)
        {
            var suggestions = new List<string>();
            
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(correctWord))
                return suggestions;

            // 总是包含正确单词
            suggestions.Add(correctWord);

            // 常见的拼写错误模式
            var commonMistakes = GenerateCommonMistakes(correctWord);
            
            // 根据编辑距离筛选建议
            var candidates = new List<(string word, int distance)>();
            
            foreach (var mistake in commonMistakes)
            {
                int distance = LevenshteinDistance(input, mistake);
                if (distance <= 2 && !suggestions.Contains(mistake))
                {
                    candidates.Add((mistake, distance));
                }
            }

            // 按编辑距离排序，取前几个
            candidates = candidates.OrderBy(x => x.distance).Take(maxSuggestions - 1).ToList();
            suggestions.AddRange(candidates.Select(x => x.word));

            return suggestions.Distinct().Take(maxSuggestions).ToList();
        }

        /// <summary>
        /// 生成常见的拼写错误变体
        /// </summary>
        /// <param name="word">正确单词</param>
        /// <returns>常见错误变体列表</returns>
        private static List<string> GenerateCommonMistakes(string word)
        {
            var mistakes = new List<string>();
            
            if (string.IsNullOrEmpty(word))
                return mistakes;

            // 大小写变体
            mistakes.Add(word.ToLower());
            mistakes.Add(word.ToUpper());
            mistakes.Add(char.ToUpper(word[0]) + word.Substring(1).ToLower());

            // 常见的字母替换错误
            var replacements = new Dictionary<string, string[]>
            {
                { "a", new[] { "e", "o", "u" } },
                { "e", new[] { "a", "i", "o" } },
                { "i", new[] { "e", "o", "y" } },
                { "o", new[] { "a", "e", "u" } },
                { "u", new[] { "a", "o" } },
                { "c", new[] { "k", "s" } },
                { "k", new[] { "c", "ck" } },
                { "s", new[] { "c", "z" } },
                { "z", new[] { "s" } },
                { "ph", new[] { "f" } },
                { "f", new[] { "ph" } }
            };

            // 生成单字母替换错误
            foreach (var replacement in replacements)
            {
                int index = word.IndexOf(replacement.Key, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    foreach (var replacementChar in replacement.Value)
                    {
                        var mistake = word.Substring(0, index) + replacementChar + 
                                    word.Substring(index + replacement.Key.Length);
                        mistakes.Add(mistake);
                    }
                }
            }

            // 双写字母错误
            for (int i = 0; i < word.Length - 1; i++)
            {
                if (word[i] == word[i + 1])
                {
                    // 删除重复的字母
                    var mistake = word.Remove(i, 1);
                    mistakes.Add(mistake);
                }
                else
                {
                    // 添加重复的字母
                    var mistake = word.Insert(i + 1, word[i].ToString());
                    mistakes.Add(mistake);
                }
            }

            // 交换相邻字母
            for (int i = 0; i < word.Length - 1; i++)
            {
                var chars = word.ToCharArray();
                (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
                mistakes.Add(new string(chars));
            }

            // 删除字母
            for (int i = 0; i < word.Length; i++)
            {
                var mistake = word.Remove(i, 1);
                mistakes.Add(mistake);
            }

            // 插入常见字母
            var commonLetters = new[] { 'e', 'a', 'i', 'o', 'u', 'r', 's', 't' };
            for (int i = 0; i <= word.Length; i++)
            {
                foreach (var letter in commonLetters)
                {
                    var mistake = word.Insert(i, letter.ToString());
                    mistakes.Add(mistake);
                }
            }

            return mistakes.Distinct().ToList();
        }

        /// <summary>
        /// 检查拼写是否正确
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="correctWord">正确单词</param>
        /// <returns>是否拼写正确</returns>
        public static bool IsSpellingCorrect(string userInput, string correctWord)
        {
            if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(correctWord))
                return false;

            return userInput.Trim().Equals(correctWord.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取拼写错误的类型
        /// </summary>
        /// <param name="userInput">用户输入</param>
        /// <param name="correctWord">正确单词</param>
        /// <returns>错误类型描述</returns>
        public static string GetSpellingErrorType(string userInput, string correctWord)
        {
            if (IsSpellingCorrect(userInput, correctWord))
                return string.Empty;

            var input = userInput.ToLower();
            var correct = correctWord.ToLower();

            if (input.Length == correct.Length)
            {
                int diffCount = 0;
                for (int i = 0; i < input.Length; i++)
                {
                    if (input[i] != correct[i])
                        diffCount++;
                }

                if (diffCount == 1)
                    return "单字母错误";
                else if (diffCount == 2)
                    return "双字母错误";
                else
                    return "多字母错误";
            }
            else if (input.Length < correct.Length)
            {
                return "漏写字母";
            }
            else
            {
                return "多写字母";
            }
        }
    }
}