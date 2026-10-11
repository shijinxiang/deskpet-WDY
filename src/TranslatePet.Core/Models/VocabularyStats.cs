namespace TranslatePet.Core.Models;

public class VocabularyStats
{
    // 总生词量
    public int TotalWords { get; set; }

    // 生词本词量 (待学/打字练习)
    public int VocabularyBookWords { get; set; }

    // 记忆本词量 (艾宾浩斯强化/默写)
    public int MemoryBookWords { get; set; }

    // 今日待复习词量
    public int DueReviewWords { get; set; }

    // 已彻底掌握词量
    public int MasteredWords { get; set; }

    // 今日已复习词量
    public int ReviewedTodayWords { get; set; }

    // 本周新增词量
    public int AddedThisWeekWords { get; set; }

    // 累计打字击键练习总次数
    public int TotalTypingPracticed { get; set; }
}
