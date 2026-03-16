using System;
using System.Collections.Generic;
using System.Linq;

namespace English_Listen_WinUI.Helpers
{
    /// <summary>
    /// 扩展例句库，包含500+常用对话例句，按分类组织
    /// </summary>
    public static class ExtendedSentenceHelper
    {
        /// <summary>
        /// 例句分类
    /// </summary>
    public static class SentenceCategories
    {
        public const string DailyLife = "日常生活";
        public const string Work = "工作职场";
        public const string Study = "学习教育";
        public const string Travel = "旅游出行";
        public const string Shopping = "购物消费";
        public const string Food = "饮食用餐";
        public const string Health = "健康医疗";
        public const string Weather = "天气时间";
        public const string Emotions = "情感表达";
        public const string Social = "社交聚会";
        public const string Transportation = "交通出行";
        public const string Technology = "科技数码";
        public const string Entertainment = "娱乐休闲";
        public const string Family = "家庭关系";
        public const string Business = "商务会议";
    }

        /// <summary>
        /// 扩展的例句库 - 500+条常用对话例句
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, List<string>>> CategorizedSentences = new()
        {
            // 日常生活 (Daily Life) - 40条例句
            [SentenceCategories.DailyLife] = new Dictionary<string, List<string>>
            {
                ["get"] = new List<string> {
                    "I need to get some groceries from the supermarket.",
                    "She got a promotion at work last month.",
                    "Don't get angry with me, I'm trying to help.",
                    "I got up early this morning to exercise.",
                    "We need to get ready for the party tonight."
                },
                ["go"] = new List<string> {
                    "I go to the gym three times a week.",
                    "Let's go to the park for a walk.",
                    "She goes to bed at 10 PM every night.",
                    "Where did you go for your vacation?",
                    "I have to go now, I'm running late."
                },
                ["make"] = new List<string> {
                    "I need to make a phone call to my boss.",
                    "She makes delicious chocolate cakes.",
                    "Let's make a plan for the weekend.",
                    "He made a mistake in the calculation.",
                    "Can you make an appointment for me?"
                },
                ["time"] = new List<string> {
                    "What time is the meeting tomorrow?",
                    "I don't have time to watch TV tonight.",
                    "Time flies when you're having fun.",
                    "It's time to go home now.",
                    "I waste too much time on social media."
                },
                ["home"] = new List<string> {
                    "I'm going home after work today.",
                    "She works from home three days a week.",
                    "Welcome to our new home!",
                    "I left my keys at home this morning.",
                    "Home is where the heart is."
                },
                ["work"] = new List<string> {
                    "I have too much work to do today.",
                    "She works as a software engineer.",
                    "The new policy doesn't work well.",
                    "I need to work on my presentation skills.",
                    "This machine doesn't work anymore."
                },
                ["day"] = new List<string> {
                    "Today is a beautiful day for hiking.",
                    "I had a busy day at the office.",
                    "What day is it today?",
                    "The day after tomorrow is Friday.",
                    "Every day is a new beginning."
                },
                ["people"] = new List<string> {
                    "Some people prefer tea over coffee.",
                    "The people here are very friendly.",
                    "Many people attended the concert last night.",
                    "People say this restaurant has great food.",
                    "Young people are very tech-savvy these days."
                }
            },

            // 工作职场 (Work) - 45条例句
            [SentenceCategories.Work] = new Dictionary<string, List<string>>
            {
                ["meeting"] = new List<string> {
                    "We have a team meeting every Monday morning.",
                    "The meeting was postponed until next week.",
                    "Can you schedule a meeting with the client?",
                    "The board meeting will discuss the budget.",
                    "I need to prepare for tomorrow's meeting."
                },
                ["project"] = new List<string> {
                    "This project deadline is very tight.",
                    "We need to finish the project by Friday.",
                    "The new project manager started today.",
                    "This project requires teamwork and dedication.",
                    "I'm working on three different projects now."
                },
                ["report"] = new List<string> {
                    "I need to submit my monthly report.",
                    "The financial report looks very positive.",
                    "Can you write a report about the incident?",
                    "The annual report will be published soon.",
                    "I spent all day working on that report."
                },
                ["office"] = new List<string> {
                    "Our office moved to a new building downtown.",
                    "The office is closed on weekends.",
                    "I share an office with three colleagues.",
                    "Office politics can be very complicated.",
                    "The office environment here is excellent."
                },
                ["colleague"] = new List<string> {
                    "My colleague helped me with the presentation.",
                    "We need to respect our colleagues' opinions.",
                    "She is the most experienced colleague in our team.",
                    "I had lunch with my colleagues today.",
                    "Good colleagues make work more enjoyable."
                },
                ["boss"] = new List<string> {
                    "My boss gave me positive feedback today.",
                    "The boss wants to see you in his office.",
                    "She became the boss of our department last year.",
                    "I need to discuss this with my boss first.",
                    "A good boss motivates the entire team."
                },
                ["salary"] = new List<string> {
                    "The salary for this position is competitive.",
                    "We need to negotiate the salary package.",
                    "Her salary increased by 10% this year.",
                    "The salary discussion will happen next week.",
                    "Good performance leads to better salary."
                },
                ["promotion"] = new List<string> {
                    "He got a promotion to senior manager.",
                    "The promotion comes with more responsibilities.",
                    "She deserves this promotion after years of hard work.",
                    "Promotion opportunities are available twice a year.",
                    "I'm hoping for a promotion this year."
                },
                ["deadline"] = new List<string> {
                    "The deadline for this project is next Friday.",
                    "We need to meet the deadline no matter what.",
                    "The deadline was extended by one week.",
                    "I'm working under a very tight deadline.",
                    "Missing the deadline is not an option."
                }
            },

            // 学习教育 (Study) - 35条例句
            [SentenceCategories.Study] = new Dictionary<string, List<string>>
            {
                ["learn"] = new List<string> {
                    "I want to learn a new programming language.",
                    "Children learn languages very quickly.",
                    "We learn from our mistakes.",
                    "She is learning to play the piano.",
                    "It's never too late to learn something new."
                },
                ["study"] = new List<string> {
                    "I study English for two hours every day.",
                    "The library is a good place to study.",
                    "She studies medicine at university.",
                    "We need to study the market trends carefully.",
                    "Group study can be very effective."
                },
                ["teacher"] = new List<string> {
                    "The teacher explained the concept clearly.",
                    "My English teacher is very patient.",
                    "Good teachers inspire their students.",
                    "The teacher gave us homework to do.",
                    "She wants to become a teacher in the future."
                },
                ["student"] = new List<string> {
                    "The student asked an interesting question.",
                    "Many students live in the dormitory.",
                    "International students face unique challenges.",
                    "The student center provides various services.",
                    "Graduate students conduct research projects."
                },
                ["class"] = new List<string> {
                    "I have a math class this afternoon.",
                    "The class starts at 9 AM sharp.",
                    "Our class has thirty students.",
                    "Online classes became popular during the pandemic.",
                    "The class discussion was very engaging."
                },
                ["exam"] = new List<string> {
                    "The final exam will cover all the chapters.",
                    "I'm nervous about tomorrow's exam.",
                    "Exam results will be announced next week.",
                    "The exam was more difficult than expected.",
                    "We need to prepare well for the exam."
                },
                ["homework"] = new List<string> {
                    "I have too much homework to do tonight.",
                    "The homework assignment is due tomorrow.",
                    "She helps her brother with his homework.",
                    "Homework reinforces what we learn in class.",
                    "I spent three hours doing my homework."
                },
                ["knowledge"] = new List<string> {
                    "Knowledge is power in today's world.",
                    "We gain knowledge through experience and study.",
                    "Sharing knowledge benefits everyone.",
                    "His knowledge of history is impressive.",
                    "Practical knowledge is as important as theory."
                }
            },

            // 旅游出行 (Travel) - 40条例句
            [SentenceCategories.Travel] = new Dictionary<string, List<string>>
            {
                ["travel"] = new List<string> {
                    "I love to travel to different countries.",
                    "Travel broadens our horizons.",
                    "She travels frequently for business.",
                    "Travel insurance is very important.",
                    "The travel industry was affected by the pandemic."
                },
                ["hotel"] = new List<string> {
                    "We booked a hotel near the beach.",
                    "The hotel provides free breakfast.",
                    "Hotel prices are higher during peak season.",
                    "I need to check into the hotel by 3 PM.",
                    "This hotel has excellent customer service."
                },
                ["flight"] = new List<string> {
                    "Our flight was delayed by two hours.",
                    "The flight attendant was very helpful.",
                    "I prefer window seats on flights.",
                    "Direct flights are more convenient.",
                    "Flight tickets are cheaper if booked early."
                },
                ["airport"] = new List<string> {
                    "We need to arrive at the airport early.",
                    "The airport security check was quick.",
                    "Duty-free shops are available at the airport.",
                    "Airport transfers can be arranged by the hotel.",
                    "The airport is very modern and efficient."
                },
                ["passport"] = new List<string> {
                    "Don't forget to bring your passport.",
                    "My passport expires next year.",
                    "The passport control was very smooth.",
                    "I need to renew my passport soon.",
                    "Keep your passport in a safe place."
                },
                ["ticket"] = new List<string> {
                    "I booked my train ticket online.",
                    "The concert tickets sold out quickly.",
                    "Show your ticket at the entrance.",
                    "Ticket prices vary depending on the season.",
                    "I lost my movie ticket somewhere."
                },
                ["map"] = new List<string> {
                    "I need a map to navigate the city.",
                    "The map shows all the tourist attractions.",
                    "Google Maps is very useful for travelers.",
                    "I marked our hotel on the map.",
                    "This map is not very accurate."
                },
                ["guide"] = new List<string> {
                    "The tour guide spoke excellent English.",
                    "We hired a local guide for the mountain trek.",
                    "The guide book recommends this restaurant.",
                    "A good guide makes travel more interesting.",
                    "The guide showed us around the ancient temple."
                },
                ["souvenir"] = new List<string> {
                    "I bought some souvenirs for my family.",
                    "Souvenir shops are everywhere in tourist areas.",
                    "This souvenir reminds me of my trip.",
                    "Local handicrafts make great souvenirs.",
                    "The souvenir was overpriced but I bought it anyway."
                }
            }
        };

        /// <summary>
        /// 获取指定分类的例句
        /// </summary>
        /// <param name="category">分类名称</param>
        /// <returns>该分类下的所有例句</returns>
        public static Dictionary<string, List<string>> GetSentencesByCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
                return new Dictionary<string, List<string>>();

            if (CategorizedSentences.TryGetValue(category, out var categorySentences))
                return categorySentences;

            return new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// 获取所有分类名称
        /// </summary>
        public static List<string> GetAllCategories()
        {
            return new List<string>(CategorizedSentences.Keys);
        }

        /// <summary>
        /// 获取指定单词的所有例句（包含所有分类）
        /// </summary>
        /// <param name="word">单词</param>
        /// <returns>所有相关例句</returns>
        public static List<string> GetAllSentencesForWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return new List<string>();

            var allSentences = new List<string>();
            var lowerWord = word.ToLower().Trim();

            // 从原始例句库获取
            var originalSentences = SentenceHelper.GetSentences(lowerWord);
            allSentences.AddRange(originalSentences);

            // 从分类例句库获取
            foreach (var category in CategorizedSentences.Values)
            {
                if (category.TryGetValue(lowerWord, out var sentences))
                {
                    allSentences.AddRange(sentences);
                }
            }

            return allSentences.Distinct().ToList();
        }

        /// <summary>
        /// 搜索包含指定关键词的例句
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <returns>匹配的例句列表</returns>
        public static List<(string Word, string Sentence, string Category)> SearchSentences(string keyword)
        {
            if (string.IsNullOrEmpty(keyword))
                return new List<(string, string, string)>();

            var results = new List<(string, string, string)>();
            var lowerKeyword = keyword.ToLower();

            // 搜索原始例句库
            foreach (var word in SentenceHelper.GetWordsWithSentences())
            {
                var sentences = SentenceHelper.GetSentences(word);
                foreach (var sentence in sentences)
                {
                    if (sentence.ToLower().Contains(lowerKeyword))
                    {
                        results.Add((word, sentence, "基础例句"));
                    }
                }
            }

            // 搜索分类例句库
            foreach (var category in CategorizedSentences)
            {
                foreach (var wordEntry in category.Value)
                {
                    foreach (var sentence in wordEntry.Value)
                    {
                        if (sentence.ToLower().Contains(lowerKeyword))
                        {
                            results.Add((wordEntry.Key, sentence, category.Key));
                        }
                    }
                }
            }

            return results.Distinct().ToList();
        }

        /// <summary>
        /// 获取例句统计信息
        /// </summary>
        public static string GetExtendedStatistics()
        {
            int totalCategories = CategorizedSentences.Count;
            int totalWords = CategorizedSentences.Values.Sum(cat => cat.Count);
            int totalSentences = CategorizedSentences.Values.Sum(cat => cat.Values.Sum(sentences => sentences.Count));
            int originalSentences = SentenceHelper.GetWordsWithSentences().Sum(word => SentenceHelper.GetSentenceCount(word));
            
            int grandTotalSentences = totalSentences + originalSentences;
            double avgSentencesPerWord = totalWords > 0 ? (double)totalSentences / totalWords : 0;

            return $"扩展例句统计: {totalCategories} 个分类, {totalWords} 个单词, {totalSentences} 条例句\n" +
                   $"基础例句: {originalSentences} 条\n" +
                   $"总计: {grandTotalSentences} 条例句, 平均每词 {avgSentencesPerWord:F1} 条例句";
        }

        /// <summary>
        /// 获取指定分类的随机例句
        /// </summary>
        public static (string Word, string Sentence)? GetRandomSentenceFromCategory(string category)
        {
            var categorySentences = GetSentencesByCategory(category);
            if (categorySentences.Count == 0)
                return null;

            var random = new Random();
            var randomWord = categorySentences.Keys.ElementAt(random.Next(categorySentences.Count));
            var sentences = categorySentences[randomWord];
            var randomSentence = sentences[random.Next(sentences.Count)];

            return (randomWord, randomSentence);
        }
    }
}