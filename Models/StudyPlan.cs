using System;
using System.Collections.Generic;

namespace English_Listen_WinUI.Models
{
    /// <summary>
    /// 学习计划配置
    /// </summary>
    public class StudyPlanSettings
    {
        /// <summary>
        /// 每天背诵单词数量
        /// </summary>
        public int DailyWordCount { get; set; } = 20;

        /// <summary>
        /// 选中的词库文件名（单个，向后兼容）
        /// </summary>
        public string? SelectedWordList { get; set; }

        /// <summary>
        /// 选中的词库文件名列表（支持多选）
        /// </summary>
        public List<string> SelectedWordLists { get; set; } = new();

        /// <summary>
        /// 从词库中选择的起始索引（0-based）
        /// </summary>
        public int StartIndex { get; set; } = 0;

        /// <summary>
        /// 从词库中选择的结束索引（包含）
        /// </summary>
        public int EndIndex { get; set; } = 0;

        /// <summary>
        /// 是否使用随机顺序
        /// </summary>
        public bool RandomOrder { get; set; } = true;

        /// <summary>
        /// 上次选中的单词列表
        /// </summary>
        public List<string> SelectedWords { get; set; } = new();

        /// <summary>
        /// 已学习的单词列表（朗读过即标记）
        /// </summary>
        public List<string> LearnedWords { get; set; } = new();

        /// <summary>
        /// 上次学习日期
        /// </summary>
        public DateTime LastStudyDate { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 已完成学习天数
        /// </summary>
        public int CompletedDays { get; set; } = 0;

        /// <summary>
        /// 总共已学习单词数
        /// </summary>
        public int TotalLearnedWords { get; set; } = 0;

        /// <summary>
        /// 词书是否已锁定（选择一次后不可更改，除非确认丢失进度）
        /// </summary>
        public bool IsWordListLocked { get; set; } = false;
    }
}