using System;
using System.Collections.Generic;

namespace English_Listen_WinUI.Helpers
{
    /// <summary>
    /// 包含分类的常用英语句子数据类
    /// </summary>
    public static class SentencesData
    {
        /// <summary>
        /// 获取所有分类的常用英语句子
        /// </summary>
        /// <returns>包含分类和句子的字典</returns>
        public static Dictionary<string, List<string>> GetCategorizedSentences()
        {
            return new Dictionary<string, List<string>>
            {
                ["日常生活"] = new List<string>
                {
                    "What time is it? - 现在几点了？",
                    "I need to get some groceries from the supermarket. - 我需要去超市买些食品杂货。",
                    "I got up early this morning to exercise. - 我今天早上早起锻炼。",
                    "We need to get ready for the party tonight. - 我们需要为今晚的派对做好准备。",
                    "I go to the gym three times a week. - 我每周去健身房三次。",
                    "Let's go to the park for a walk. - 我们去公园散步吧。",
                    "She goes to bed at 10 PM every night. - 她每天晚上10点睡觉。",
                    "I have to go now, I'm running late. - 我现在得走了，我要迟到了。",
                    "I'm going home after work today. - 我今天下班后回家。",
                    "She works from home three days a week. - 她每周在家工作三天。",
                    "Welcome to our new home! - 欢迎来到我们的新家！",
                    "I left my keys at home this morning. - 我今天早上把钥匙忘在家里了。",
                    "Home is where the heart is. - 家是心之所在。",
                    "Today is a beautiful day for hiking. - 今天是徒步旅行的好天气。",
                    "I had a busy day at the office. - 我在办公室度过了忙碌的一天。",
                    "What day is it today? - 今天是星期几？",
                    "The day after tomorrow is Friday. - 后天是星期五。",
                    "Every day is a new beginning. - 每一天都是新的开始。",
                    "I don't have time to watch TV tonight. - 我今晚没时间看电视。",
                    "Time flies when you're having fun. - 快乐时光总是过得很快。",
                    "It's time to go home now. - 现在该回家了。",
                    "I waste too much time on social media. - 我在社交媒体上浪费了太多时间。"
                },
                ["交流问候"] = new List<string>
                {
                    "Hello, how are you? - 你好，你怎么样？",
                    "Nice to meet you! - 很高兴见到你！",
                    "How have you been? - 你最近过得怎么样？",
                    "What's new with you? - 你有什么新鲜事吗？",
                    "Long time no see! - 好久不见！",
                    "How's everything going? - 一切顺利吗？",
                    "What's up? - 最近怎么样？",
                    "Good morning! - 早上好！",
                    "Good afternoon! - 下午好！",
                    "Good evening! - 晚上好！",
                    "Good night! - 晚安！",
                    "See you later! - 待会儿见！",
                    "Take care! - 保重！",
                    "Have a nice day! - 祝你今天愉快！",
                    "How can I help you? - 我能帮你什么忙吗？",
                    "Excuse me, do you speak English? - 打扰一下，你会说英语吗？",
                    "I'm sorry, I didn't catch that. - 对不起，我没听清楚。",
                    "Could you please repeat that? - 你能重复一下吗？",
                    "Thank you very much! - 非常感谢！",
                    "You're welcome! - 不客气！"
                },
                ["工作职场"] = new List<string>
                {
                    "We have a team meeting every Monday morning. - 我们每周一早上有团队会议。",
                    "The meeting was postponed until next week. - 会议被推迟到下周了。",
                    "Can you schedule a meeting with the client? - 你能安排与客户的会议吗？",
                    "The board meeting will discuss the budget. - 董事会会议将讨论预算。",
                    "I need to prepare for tomorrow's meeting. - 我需要为明天的会议做准备。",
                    "This project deadline is very tight. - 这个项目的截止日期很紧。",
                    "We need to finish the project by Friday. - 我们需要在周五前完成这个项目。",
                    "The new project manager started today. - 新的项目经理今天开始上班。",
                    "This project requires teamwork and dedication. - 这个项目需要团队合作和奉献精神。",
                    "I'm working on three different projects now. - 我现在正在做三个不同的项目。",
                    "I need to submit my monthly report. - 我需要提交我的月度报告。",
                    "The financial report looks very positive. - 财务报告看起来非常乐观。",
                    "Our office moved to a new building downtown. - 我们的办公室搬到了市中心的新大楼。",
                    "The office is closed on weekends. - 办公室周末不营业。",
                    "I share an office with three colleagues. - 我和三位同事共用一个办公室。",
                    "My colleague helped me with the presentation. - 我的同事帮我做了演示文稿。",
                    "My boss gave me positive feedback today. - 我的老板今天给了我积极的反馈。",
                    "The salary for this position is competitive. - 这个职位的薪水很有竞争力。",
                    "He got a promotion to senior manager. - 他被提升为高级经理。",
                    "The deadline for this project is next Friday. - 这个项目的截止日期是下周五。"
                },
                ["学习教育"] = new List<string>
                {
                    "I want to learn a new programming language. - 我想学习一门新的编程语言。",
                    "Children learn languages very quickly. - 孩子们学习语言非常快。",
                    "We learn from our mistakes. - 我们从错误中学习。",
                    "She is learning to play the piano. - 她正在学弹钢琴。",
                    "It's never too late to learn something new. - 学习新东西永远不会太晚。",
                    "I study English for two hours every day. - 我每天学习英语两小时。",
                    "The library is a good place to study. - 图书馆是个学习的好地方。",
                    "She studies medicine at university. - 她在大学学习医学。",
                    "The teacher explained the concept clearly. - 老师清楚地解释了这个概念。",
                    "My English teacher is very patient. - 我的英语老师非常有耐心。",
                    "The student asked an interesting question. - 学生提出了一个有趣的问题。",
                    "I have a math class this afternoon. - 我今天下午有数学课。",
                    "The class starts at 9 AM sharp. - 课程上午9点准时开始。",
                    "The final exam will cover all the chapters. - 期末考试将涵盖所有章节。",
                    "I'm nervous about tomorrow's exam. - 我对明天的考试感到紧张。",
                    "I have too much homework to do tonight. - 我今晚有太多作业要做。",
                    "Knowledge is power in today's world. - 在当今世界，知识就是力量。",
                    "We gain knowledge through experience and study. - 我们通过经验和学习获得知识。",
                    "Group study can be very effective. - 小组学习可以非常有效。",
                    "Online classes became popular during the pandemic. - 在疫情期间，在线课程变得流行起来。"
                },
                ["旅游出行"] = new List<string>
                {
                    "I love to travel to different countries. - 我喜欢去不同的国家旅行。",
                    "Travel broadens our horizons. - 旅行开阔我们的视野。",
                    "She travels frequently for business. - 她经常因公出差。",
                    "Travel insurance is very important. - 旅行保险非常重要。",
                    "We booked a hotel near the beach. - 我们预订了海边附近的酒店。",
                    "The hotel provides free breakfast. - 酒店提供免费早餐。",
                    "Hotel prices are higher during peak season. - 旺季酒店价格更高。",
                    "I need to check into the hotel by 3 PM. - 我需要在下午3点前办理酒店入住。",
                    "Our flight was delayed by two hours. - 我们的航班延误了两个小时。",
                    "The flight attendant was very helpful. - 空乘人员非常乐于助人。",
                    "I prefer window seats on flights. - 我更喜欢飞机上的靠窗座位。",
                    "We need to arrive at the airport early. - 我们需要提前到达机场。",
                    "The airport security check was quick. - 机场安检很快。",
                    "Don't forget to bring your passport. - 别忘了带护照。",
                    "My passport expires next year. - 我的护照明年到期。",
                    "I booked my train ticket online. - 我在网上预订了火车票。",
                    "I need a map to navigate the city. - 我需要一张地图来游览城市。",
                    "Google Maps is very useful for travelers. - 谷歌地图对旅行者非常有用。",
                    "The tour guide spoke excellent English. - 导游英语说得很好。",
                    "I bought some souvenirs for my family. - 我给家人买了一些纪念品。"
                },
                ["购物消费"] = new List<string>
                {
                    "How much does this cost? - 这个多少钱？",
                    "Do you have this in a different size? - 这个有其他尺寸吗？",
                    "I'd like to try this on. - 我想试穿一下这个。",
                    "Where is the fitting room? - 试衣间在哪里？",
                    "Do you accept credit cards? - 你们接受信用卡吗？",
                    "Is there a discount for students? - 学生有折扣吗？",
                    "This is on sale today. - 这个今天在打折。",
                    "I'm just looking, thank you. - 我只是看看，谢谢。",
                    "Can I get a refund if it doesn't fit? - 如果不合适，我可以退款吗？",
                    "Do you have this item in stock? - 这个商品有现货吗？",
                    "I'd like to exchange this for a different color. - 我想把这个换成不同的颜色。",
                    "Where can I find the electronics department? - 我在哪里可以找到电子产品区？",
                    "The receipt is in the bag. - 收据在袋子里。",
                    "I need to return this item. - 我需要退货。",
                    "What are your store hours? - 你们的营业时间是什么时候？",
                    "Is this the final price? - 这是最终价格吗？",
                    "I'll take it! - 我要了！",
                    "Do you offer gift wrapping? - 你们提供礼品包装吗？",
                    "This is exactly what I was looking for. - 这正是我在找的东西。",
                    "I found a great deal on this item. - 我找到了这个商品的超值优惠。"
                },
                ["饮食用餐"] = new List<string>
                {
                    "I'd like to make a reservation for two. - 我想预订两个人的座位。",
                    "What do you recommend? - 你推荐什么？",
                    "I'm allergic to nuts. - 我对坚果过敏。",
                    "Could I see the menu, please? - 我可以看一下菜单吗？",
                    "I'll have the chicken pasta. - 我要鸡肉意面。",
                    "Can I get this to go? - 我可以打包带走吗？",
                    "The food here is delicious. - 这里的食物很美味。",
                    "I'm vegetarian. - 我是素食主义者。",
                    "Could we have the bill, please? - 我们可以结账了吗？",
                    "Is service included? - 服务费包含在内吗？",
                    "I'd like a glass of water, please. - 我想要一杯水。",
                    "This dish is too spicy for me. - 这道菜对我来说太辣了。",
                    "Do you have any desserts? - 你们有什么甜点吗？",
                    "I'm full, thank you. - 我饱了，谢谢。",
                    "What's the special today? - 今天的特色菜是什么？",
                    "I'll split the bill with you. - 我和你分摊账单。",
                    "This restaurant has great reviews. - 这家餐厅评价很好。",
                    "I'm on a diet. - 我在节食。",
                    "Could I have some more bread, please? - 我可以再要一些面包吗？",
                    "The coffee here is excellent. - 这里的咖啡很棒。"
                },
                ["健康医疗"] = new List<string>
                {
                    "I have a doctor's appointment tomorrow. - 我明天有医生预约。",
                    "I'm not feeling well today. - 我今天感觉不舒服。",
                    "I have a headache. - 我头痛。",
                    "I need to refill my prescription. - 我需要续开处方药。",
                    "Where is the nearest pharmacy? - 最近的药店在哪里？",
                    "I'm allergic to penicillin. - 我对青霉素过敏。",
                    "I've been coughing for three days. - 我已经咳嗽三天了。",
                    "I need to get a flu shot. - 我需要打流感疫苗。",
                    "My blood pressure is high. - 我的血压高。",
                    "I exercise regularly to stay healthy. - 我定期锻炼以保持健康。",
                    "I should drink more water. - 我应该多喝水。",
                    "I'm trying to lose weight. - 我正在努力减肥。",
                    "I need to get more sleep. - 我需要多睡一会儿。",
                    "I have a fever. - 我发烧了。",
                    "I twisted my ankle. - 我扭伤了脚踝。",
                    "I need to take my medication. - 我需要吃药了。",
                    "I'm feeling much better now. - 我现在感觉好多了。",
                    "I have a chronic condition. - 我有慢性病。",
                    "I need to schedule a check-up. - 我需要安排一次体检。",
                    "The doctor prescribed antibiotics. - 医生开了抗生素。"
                },
                ["情感表达"] = new List<string>
                {
                    "I'm so happy for you! - 我真为你高兴！",
                    "I'm really sorry to hear that. - 听到这个消息我很难过。",
                    "I'm excited about the trip! - 我对这次旅行很兴奋！",
                    "I'm feeling a bit down today. - 我今天有点沮丧。",
                    "I'm proud of you! - 我为你感到骄傲！",
                    "I'm worried about the exam. - 我担心考试。",
                    "I'm grateful for your help. - 我感谢你的帮助。",
                    "I'm disappointed with the results. - 我对结果感到失望。",
                    "I'm surprised by the news. - 我对这个消息感到惊讶。",
                    "I'm nervous about the presentation. - 我对演讲感到紧张。",
                    "I'm confident I can do it. - 我有信心我能行。",
                    "I'm frustrated with this problem. - 我对这个问题感到沮丧。",
                    "I'm relieved it's over. - 我松了一口气，终于结束了。",
                    "I'm curious about your culture. - 我对你们的文化很好奇。",
                    "I'm jealous of your success. - 我嫉妒你的成功。",
                    "I'm embarrassed by my mistake. - 我对自己的错误感到尴尬。",
                    "I'm hopeful for the future. - 我对未来充满希望。",
                    "I'm exhausted after work. - 下班后我很疲惫。",
                    "I'm bored with this movie. - 我对这部电影感到无聊。",
                    "I'm in love with her. - 我爱上了她。"
                },
                ["紧急情况"] = new List<string>
                {
                    "Help! - 救命！",
                    "Call the police! - 叫警察！",
                    "Call an ambulance! - 叫救护车！",
                    "Fire! - 着火了！",
                    "I need help! - 我需要帮助！",
                    "Someone stole my wallet! - 有人偷了我的钱包！",
                    "I'm lost. - 我迷路了。",
                    "My car broke down. - 我的车抛锚了。",
                    "There's been an accident! - 出事故了！",
                    "I need a doctor! - 我需要医生！",
                    "Where is the nearest hospital? - 最近的医院在哪里？",
                    "I've lost my passport. - 我的护照丢了。",
                    "My phone isn't working. - 我的手机坏了。",
                    "I need to contact my embassy. - 我需要联系我的大使馆。",
                    "Is there a first aid kit here? - 这里有急救箱吗？",
                    "I'm having trouble breathing. - 我呼吸困难。",
                    "The building is on fire! - 大楼着火了！",
                    "I need to report a crime. - 我需要报案。",
                    "My credit card was stolen. - 我的信用卡被盗了。",
                    "I need emergency assistance. - 我需要紧急援助。"
                }
            };
        }

        /// <summary>
        /// 获取所有句子（扁平化列表）
        /// </summary>
        /// <returns>包含所有句子的列表</returns>
        public static List<string> GetAllSentences()
        {
            var allSentences = new List<string>();
            var categorized = GetCategorizedSentences();
            
            foreach (var category in categorized)
            {
                allSentences.AddRange(category.Value);
            }
            
            return allSentences;
        }
    }
}