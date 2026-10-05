namespace TranslatePet.Core.Models;

public record TranslationResult
{
    public bool Success { get; init; }
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = "auto";
    public string TargetLanguage { get; init; } = "zh-Hans";
    public string? ErrorMessage { get; init; }

    public static TranslationResult Succeeded(string originalText, string translatedText, string sourceLang, string targetLang) => new()
    {
        Success = true,
        OriginalText = originalText,
        TranslatedText = translatedText,
        SourceLanguage = sourceLang,
        TargetLanguage = targetLang
    };

    public static TranslationResult Failed(string originalText, string errorMessage, string sourceLang = "auto", string targetLang = "zh-Hans") => new()
    {
        Success = false,
        OriginalText = originalText,
        TranslatedText = string.Empty,
        ErrorMessage = errorMessage,
        SourceLanguage = sourceLang,
        TargetLanguage = targetLang
    };
}
