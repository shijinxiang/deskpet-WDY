namespace TranslatePet.Core.Models;

public record OcrResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
    public float? Confidence { get; init; }

    public static OcrResult Succeeded(string text, float? confidence = null) => new()
    {
        Success = true,
        Text = text,
        Confidence = confidence
    };

    public static OcrResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
