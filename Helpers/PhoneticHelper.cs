using System;
using System.Text.RegularExpressions;

namespace English_Listen_WinUI.Helpers
{
    /// <summary>
    /// 音标处理助手类
    /// </summary>
    public static class PhoneticHelper
    {
        /// <summary>
        /// 常见单词的音标字典（简化版）
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> CommonPhonetics = new()
        {
            // 基础词汇
            {"the", "ðə"}, {"a", "ə"}, {"an", "ən"}, {"and", "ənd"}, {"or", "ɔːr"},
            {"but", "bʌt"}, {"not", "nɒt"}, {"in", "ɪn"}, {"on", "ɒn"}, {"at", "æt"},
            {"to", "tuː"}, {"for", "fɔːr"}, {"of", "əv"}, {"with", "wɪð"}, {"from", "frɒm"},
            {"by", "baɪ"}, {"is", "ɪz"}, {"are", "ɑːr"}, {"was", "wɒz"}, {"were", "wɜːr"},
            {"be", "biː"}, {"been", "biːn"}, {"have", "hæv"}, {"has", "hæz"}, {"had", "hæd"},
            {"do", "duː"}, {"does", "dʌz"}, {"did", "dɪd"}, {"will", "wɪl"}, {"would", "wʊd"},
            {"could", "kʊd"}, {"should", "ʃʊd"}, {"can", "kæn"}, {"may", "meɪ"}, {"might", "maɪt"},
            
            // 人称代词
            {"i", "aɪ"}, {"you", "juː"}, {"he", "hiː"}, {"she", "ʃiː"}, {"it", "ɪt"},
            {"we", "wiː"}, {"they", "ðeɪ"}, {"me", "miː"}, {"him", "hɪm"}, {"her", "hɜːr"},
            {"us", "ʌs"}, {"them", "ðem"}, {"my", "maɪ"}, {"your", "jɔːr"}, {"his", "hɪz"},
            {"her", "hɜːr"}, {"its", "ɪts"}, {"our", "aʊər"}, {"their", "ðer"},
            
            // 常见动词
            {"go", "ɡoʊ"}, {"get", "ɡet"}, {"make", "meɪk"}, {"take", "teɪk"}, {"come", "kʌm"},
            {"see", "siː"}, {"know", "noʊ"}, {"think", "θɪŋk"}, {"look", "lʊk"}, {"use", "juːz"},
            {"find", "faɪnd"}, {"give", "ɡɪv"}, {"tell", "tel"}, {"work", "wɜːrk"}, {"call", "kɔːl"},
            {"try", "traɪ"}, {"need", "niːd"}, {"feel", "fiːl"}, {"become", "bɪˈkʌm"}, {"leave", "liːv"},
            {"put", "pʊt"}, {"mean", "miːn"}, {"keep", "kiːp"}, {"let", "let"}, {"begin", "bɪˈɡɪn"},
            {"seem", "siːm"}, {"help", "help"}, {"talk", "tɔːk"}, {"turn", "tɜːrn"}, {"start", "stɑːrt"},
            {"show", "ʃoʊ"}, {"hear", "hɪr"}, {"play", "pleɪ"}, {"run", "rʌn"}, {"move", "muːv"},
            {"live", "lɪv"}, {"believe", "bɪˈliːv"}, {"hold", "hoʊld"}, {"bring", "brɪŋ"}, {"happen", "ˈhæpən"},
            {"write", "raɪt"}, {"provide", "prəˈvaɪd"}, {"sit", "sɪt"}, {"stand", "stænd"}, {"lose", "luːz"},
            
            // 常见名词
            {"time", "taɪm"}, {"year", "jɪr"}, {"way", "weɪ"}, {"day", "deɪ"}, {"man", "mæn"},
            {"thing", "θɪŋ"}, {"woman", "ˈwʊmən"}, {"life", "laɪf"}, {"child", "tʃaɪld"}, {"world", "wɜːrld"},
            {"school", "skuːl"}, {"state", "steɪt"}, {"family", "ˈfæməli"}, {"student", "ˈstjuːdnt"}, {"group", "ɡruːp"},
            {"country", "ˈkʌntri"}, {"problem", "ˈprɒbləm"}, {"hand", "hænd"}, {"part", "pɑːrt"}, {"place", "pleɪs"},
            {"case", "keɪs"}, {"week", "wiːk"}, {"company", "ˈkʌmpəni"}, {"system", "ˈsɪstəm"}, {"program", "ˈproʊɡræm"},
            {"question", "ˈkwestʃən"}, {"work", "wɜːrk"}, {"government", "ˈɡʌvərnmənt"}, {"number", "ˈnʌmbər"}, {"night", "naɪt"},
            {"point", "pɔɪnt"}, {"home", "hoʊm"}, {"water", "ˈwɔːtər"}, {"room", "ruːm"}, {"mother", "ˈmʌðər"},
            {"area", "ˈeriə"}, {"money", "ˈmʌni"}, {"story", "ˈstɔːri"}, {"fact", "fækt"}, {"right", "raɪt"},
            
            // 形容词
            {"good", "ɡʊd"}, {"new", "njuː"}, {"first", "fɜːrst"}, {"last", "læst"}, {"long", "lɒŋ"},
            {"great", "ɡreɪt"}, {"little", "ˈlɪtl"}, {"own", "oʊn"}, {"other", "ˈʌðər"}, {"old", "oʊld"},
            {"right", "raɪt"}, {"big", "bɪɡ"}, {"high", "haɪ"}, {"different", "ˈdɪfrənt"}, {"small", "smɔːl"},
            {"large", "lɑːrdʒ"}, {"next", "nekst"}, {"early", "ˈɜːrli"}, {"young", "jʌŋ"}, {"important", "ɪmˈpɔːrtnt"},
            {"few", "fjuː"}, {"public", "ˈpʌblɪk"}, {"bad", "bæd"}, {"same", "seɪm"}, {"able", "ˈeɪbl"},
            
            // 副词
            {"up", "ʌp"}, {"so", "soʊ"}, {"out", "aʊt"}, {"just", "dʒʌst"}, {"now", "naʊ"},
            {"then", "ðen"}, {"more", "mɔːr"}, {"also", "ˈɔːlsoʊ"}, {"here", "hɪr"}, {"how", "haʊ"},
            {"its", "ɪts"}, {"our", "aʊər"}, {"over", "ˈoʊvər"}, {"such", "sʌtʃ"}, {"than", "ðæn"},
            {"them", "ðem"}, {"very", "ˈveri"}, {"well", "wel"}, {"when", "wen"}, {"where", "wer"},
            {"only", "ˈoʊnli"}, {"down", "daʊn"}, {"back", "bæk"}, {"still", "stɪl"}, {"any", "ˈeni"},
            {"away", "əˈweɪ"}, {"today", "təˈdeɪ"}, {"each", "iːtʃ"}, {"enough", "ɪˈnʌf"}, {"ever", "ˈevər"},
            {"once", "wʌns"}, {"rather", "ˈræðər"}, {"soon", "suːn"}, {"together", "təˈɡeðər"}, {"too", "tuː"},
            {"quite", "kwaɪt"}, {"almost", "ˈɔːlmoʊst"}, {"always", "ˈɔːlweɪz"}, {"around", "əˈraʊnd"}, {"before", "bɪˈfɔːr"}
        };

        /// <summary>
        /// 获取单词的音标
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>音标字符串，如果找不到或不是英文单词返回null</returns>
        public static string? GetPhonetic(string word)
        {
            if (string.IsNullOrEmpty(word))
                return null;

            // 检查是否为英文单词（只包含英文字母）
            if (!IsEnglishWord(word))
                return null;

            var lowerWord = word.ToLower().Trim();
            
            // 查找常见单词音标
            if (CommonPhonetics.TryGetValue(lowerWord, out string? phonetic))
                return phonetic;

            // 简单的音标生成规则（基于拼写规则）
            return GeneratePhoneticFromSpelling(lowerWord);
        }

        /// <summary>
        /// 判断是否为英文单词（只包含英文字母）
        /// </summary>
        /// <param name="word">要检查的单词</param>
        /// <returns>是否为英文单词</returns>
        public static bool IsEnglishWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            // 使用正则表达式检查是否只包含英文字母
            return System.Text.RegularExpressions.Regex.IsMatch(word.Trim(), "^[a-zA-Z]+$");
        }

        /// <summary>
        /// 基于拼写规则生成简单的音标
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>生成的音标</returns>
        private static string GeneratePhoneticFromSpelling(string word)
        {
            if (string.IsNullOrEmpty(word))
                return "";

            var phonetic = word;

            // 简单的拼写到音标转换规则
            var rules = new[]
            {
                // 长元音
                new { Pattern = "ee", Replacement = "iː" },
                new { Pattern = "ea", Replacement = "iː" },
                new { Pattern = "ai", Replacement = "eɪ" },
                new { Pattern = "ay", Replacement = "eɪ" },
                new { Pattern = "oa", Replacement = "oʊ" },
                new { Pattern = "ow", Replacement = "oʊ" },
                new { Pattern = "ie", Replacement = "aɪ" },
                new { Pattern = "igh", Replacement = "aɪ" },
                new { Pattern = "ue", Replacement = "juː" },
                new { Pattern = "ui", Replacement = "uː" },
                
                // 短元音
                new { Pattern = "a", Replacement = "æ" },
                new { Pattern = "e", Replacement = "e" },
                new { Pattern = "i", Replacement = "ɪ" },
                new { Pattern = "o", Replacement = "ɒ" },
                new { Pattern = "u", Replacement = "ʌ" },
                
                // 辅音
                new { Pattern = "ch", Replacement = "tʃ" },
                new { Pattern = "sh", Replacement = "ʃ" },
                new { Pattern = "th", Replacement = "θ" },
                new { Pattern = "ph", Replacement = "f" },
                new { Pattern = "ck", Replacement = "k" },
                new { Pattern = "qu", Replacement = "kw" },
                new { Pattern = "ng", Replacement = "ŋ" },
                
                // 其他
                new { Pattern = "y", Replacement = "i" },
                new { Pattern = "ar", Replacement = "ɑːr" },
                new { Pattern = "er", Replacement = "ɜːr" },
                new { Pattern = "or", Replacement = "ɔːr" },
                new { Pattern = "ir", Replacement = "ɜːr" },
                new { Pattern = "ur", Replacement = "ɜːr" }
            };

            // 应用转换规则
            foreach (var rule in rules)
            {
                phonetic = Regex.Replace(phonetic, rule.Pattern, rule.Replacement, RegexOptions.IgnoreCase);
            }

            // 添加重音标记（简化版：假设第一个音节为重音）
            if (phonetic.Length > 2)
            {
                phonetic = "ˈ" + phonetic;
            }

            return "/" + phonetic + "/";
        }

        /// <summary>
        /// 判断音标是英式还是美式（简化判断）
        /// </summary>
        /// <param name="phonetic">音标</param>
        /// <returns>音标类型</returns>
        public static string GetPhoneticType(string phonetic)
        {
            if (string.IsNullOrEmpty(phonetic))
                return "Unknown";

            // 英式音标特征
            if (phonetic.Contains("ɑː") || phonetic.Contains("ɒ") || phonetic.Contains("əʊ"))
                return "British";
            
            // 美式音标特征
            if (phonetic.Contains("æ") || phonetic.Contains("ɝ") || phonetic.Contains("oʊ"))
                return "American";

            return "General";
        }

        /// <summary>
        /// 格式化音标显示
        /// </summary>
        /// <param name="phonetic">音标</param>
        /// <param name="style">显示样式</param>
        /// <returns>格式化后的音标</returns>
        public static string FormatPhonetic(string phonetic, string style = "default")
        {
            if (string.IsNullOrEmpty(phonetic))
                return "";

            return style.ToLower() switch
            {
                "bracket" => $"[{phonetic}]",
                "slash" => $"/{phonetic}/",
                "parentheses" => $"({phonetic})",
                _ => phonetic.StartsWith("/") && phonetic.EndsWith("/") ? phonetic : $"/{phonetic}/"
            };
        }

        /// <summary>
        /// 获取单词的音节数（简化估算）
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>音节数</returns>
        public static int EstimateSyllables(string word)
        {
            if (string.IsNullOrEmpty(word))
                return 0;

            word = word.ToLower();
            int syllableCount = 0;
            bool lastWasVowel = false;

            foreach (char c in word)
            {
                if ("aeiouy".Contains(c))
                {
                    if (!lastWasVowel)
                    {
                        syllableCount++;
                    }
                    lastWasVowel = true;
                }
                else
                {
                    lastWasVowel = false;
                }
            }

            // 调整特殊情况
            if (word.EndsWith("e") && syllableCount > 1)
                syllableCount--;

            if (word.EndsWith("le") && word.Length > 2 && !"aeiou".Contains(word[word.Length - 3]))
                syllableCount++;

            return Math.Max(1, syllableCount);
        }
    }
}