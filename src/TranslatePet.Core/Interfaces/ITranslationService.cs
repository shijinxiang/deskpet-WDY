using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface ITranslationService
{
    Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default);
    IEnumerable<ITranslationProvider> AvailableProviders { get; }
    void SetCurrentProvider(string providerId);
}
