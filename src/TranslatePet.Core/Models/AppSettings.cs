namespace TranslatePet.Core.Models;

public class AppSettings
{
    // 快捷键设置 (默认 Alt + Q)
    public bool HotkeyAlt { get; set; } = true;
    public bool HotkeyCtrl { get; set; } = false;
    public bool HotkeyShift { get; set; } = false;
    public bool HotkeyWin { get; set; } = false;
    public string HotkeyKey { get; set; } = "Q";

    // OCR 引擎配置
    // 可选: "WindowsNative", "BaiduCloud", "Mock"
    public string OcrProvider { get; set; } = "WindowsNative";
    public string BaiduOcrApiKey { get; set; } = string.Empty;
    public string BaiduOcrSecretKey { get; set; } = string.Empty;

    // 翻译服务配置
    // 可选: "OpenAiCompatible", "PublicMyMemory"
    public string TranslationProvider { get; set; } = "OpenAiCompatible";
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public bool EnableDeepThinking { get; set; } = false; // 大模型深度思考 / 思维链推理开关 (默认 false: 极速秒级直出，避免翻译太慢)

    // 当前翻译模式: "Conversational" (地道社交口语), "Standard" (通用准确), "Formal" (商务正式)
    public string TranslationStyle { get; set; } = "Conversational";

    // ===== 专项目功能预设提示词 (用户可自由定制) =====

    // 1. 地道口语与外国友人顺畅对话提示词
    public string PromptConversational { get; set; } =
@"你是一位母语级别的跨文化交流专家与语言顾问。你的任务是将内容翻译为地道、自然、毫无机翻感的日常口语表达，使外国友人交流如同老友交谈般顺畅。
要求：
1. 彻底摆脱中式直译，精准还原说话者的语气、情感、网络流行语或地道俚语（Slang/Idiom）。
2. 使用英语母语者日常最自然、最习惯的遣词造句（Colloquial & Native phrasing）。
3. 输出格式规范：
   - 首先给出【地道译文】；
   - 若句子中包含特殊俚语、文化背景梗或更有趣味的地道替代表达，在末尾附上简明扼要的【💡 地道表达小贴士】（不超过3条）。";

    // 2. 通用学术严谨标准提示词
    public string PromptStandard { get; set; } =
@"你是一位专业高水准翻译专家。请将待翻译内容精准、规范、忠实地翻译为 {TargetLanguage}。
要求：
1. 术语专业准确，语法逻辑严密，保留原文段落结构与标点习惯。
2. 语言通顺优美，只返回翻译结果本体，不夹带任何寒暄或客套解释。";

    // 3. 商务正式公文礼仪提示词
    public string PromptFormal { get; set; } =
@"你是一位涉外商务与外交通信顾问。请将内容翻译为得体、专业、优雅的职场与商务级 {TargetLanguage}。
要求：
1. 用词严谨专业，语气礼貌庄重，契合外企工作汇报、商业信函、合同或官方公告场景。
2. 保持专业排版，直接输出高质量正式译文。";

    // 4. 重点生词智能提取提示词
    public string PromptWordExtraction { get; set; } =
@"你是一位敏锐的英语词汇私教。请从以下英文文本中，智能提炼出 1 至 4 个最值得积累的核心重点生词、地道短语或习语搭配：
文本内容：""{SourceText}""
要求按以下 Markdown 格式清晰输出：
- **单词/短语** [美式音标] : 中文核心释义 (词性)
  - 语境例句: 例句英文 (中文翻译)
只返回提取结果列表，排版清晰美观。";

    // 5. 生词深度剖析与助记提示词
    public string PromptWordExplanation { get; set; } =
@"你是一位风趣幽默的语言记忆教练哆啦A梦。请为目标生词或短语 ""{SourceText}"" 生成深度记忆卡片：
1. 【音标发音】IPA 音标与发音关键点
2. 【核心释义】常用词性与精准中文含义
3. 【地道搭配】2个母语者高频使用的常用口语/书面搭配
4. 【生动例句】2个地道语境例句（附中文对照）
5. 【哆啦A梦记忆魔法】形象联想记忆口诀或词根词缀趣解，帮助用户轻松过目不忘。";

    // 语言偏好设置
    public string DefaultSourceLanguage { get; set; } = "en";
    public string DefaultTargetLanguage { get; set; } = "zh-Hans";

    // 桌宠偏好
    public double PetPositionX { get; set; } = -1; // -1 表示使用默认屏幕右下角位置
    public double PetPositionY { get; set; } = -1;
    public bool PetTopMost { get; set; } = true;
    public bool PetVisible { get; set; } = true;
    public double PetScale { get; set; } = 1.0;
    public bool EnableEdgeSnap { get; set; } = true; // 启用桌面边缘吸附与探头帧动画

    // 每日背单词与生词本设置
    public bool EnableDailyWordReminder { get; set; } = true; // 开启每日背单词提醒
    public string DailyReminderTime { get; set; } = "20:00"; // 每日提醒时间 (HH:mm)
    public string LastReminderDate { get; set; } = string.Empty; // 上次提醒日期 (YYYY-MM-DD)
    public int DailyReviewGoal { get; set; } = 10; // 每日建议复习单词数

    // 隐私与运行配置
    public bool SaveHistory { get; set; } = false;
    public bool AutoStartOnBoot { get; set; } = false;
}
