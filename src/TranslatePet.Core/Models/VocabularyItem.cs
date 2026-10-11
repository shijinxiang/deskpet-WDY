namespace TranslatePet.Core.Models;

public class VocabularyItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    // 单词或短语
    public string Word { get; set; } = string.Empty;

    // 音标，如 /əˈkeɪʒən/
    public string Phonetic { get; set; } = string.Empty;

    // 核心释义，如 "n. 场合；时机；盛大活动"
    public string Meaning { get; set; } = string.Empty;

    // 来源例句上下文
    public string ContextSentence { get; set; } = string.Empty;

    // 来源例句中文翻译
    public string ContextTranslation { get; set; } = string.Empty;

    // 助记技巧或深度解析
    public string MemoryTip { get; set; } = string.Empty;

    // 分类标签
    public string Tags { get; set; } = "日常交流";

    // 归属本子: "Vocabulary" (生词本) 或 "Memory" (记忆本)
    public string BookType { get; set; } = "Vocabulary";

    // 打字练习统计 (TypeWords 核心机制)
    public int TypingPracticedCount { get; set; } = 0;
    public int TypingMistakes { get; set; } = 0;
    public DateTime? LastTypingPracticedAt { get; set; }

    // 创建时间
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 艾宾浩斯下一次复习时间
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;

    // 已复习次数
    public int ReviewCount { get; set; } = 0;

    // 艾宾浩斯记忆阶段: 0=新词, 1=1天后, 2=2天后, 3=4天后, 4=7天后, 5=15天后, 6=掌握
    public int MemoryStage { get; set; } = 0;

    // 是否已彻底掌握
    public bool IsMastered { get; set; } = false;
}
