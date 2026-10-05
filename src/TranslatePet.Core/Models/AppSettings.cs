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
    // 可选: "BaiduCloud", "WindowsNative", "Mock"
    public string OcrProvider { get; set; } = "BaiduCloud";
    public string BaiduOcrApiKey { get; set; } = string.Empty;
    public string BaiduOcrSecretKey { get; set; } = string.Empty;

    // 翻译服务配置
    // 可选: "OpenAiCompatible", "PublicMyMemory"
    public string TranslationProvider { get; set; } = "OpenAiCompatible";
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string OpenAiCustomPrompt { get; set; } = "You are a professional translator. Translate the given text accurately and naturally to {TargetLanguage}. Only return the translation, without any explanations or quotes.";

    // 语言偏好设置
    public string DefaultSourceLanguage { get; set; } = "en";
    public string DefaultTargetLanguage { get; set; } = "zh-Hans";

    // 桌宠偏好
    public double PetPositionX { get; set; } = -1; // -1 表示使用默认屏幕右下角位置
    public double PetPositionY { get; set; } = -1;
    public bool PetTopMost { get; set; } = true;
    public bool PetVisible { get; set; } = true;
    public double PetScale { get; set; } = 1.0;

    // 隐私与运行配置
    public bool SaveHistory { get; set; } = false;
    public bool AutoStartOnBoot { get; set; } = false;
}
