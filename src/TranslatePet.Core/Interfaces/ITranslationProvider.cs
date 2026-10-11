using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface ITranslationProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, string? customPrompt = null, CancellationToken cancellationToken = default);
}
