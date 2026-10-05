using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Translation;

public class TranslationService : ITranslationService
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, ITranslationProvider> _providers;

    public IEnumerable<ITranslationProvider> AvailableProviders => _providers.Values;

    public TranslationService(ISettingsService settingsService, IEnumerable<ITranslationProvider> providers)
    {
        _settingsService = settingsService;
        _providers = providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
    }

    public void SetCurrentProvider(string providerId)
    {
        if (_providers.ContainsKey(providerId))
        {
            _settingsService.CurrentSettings.TranslationProvider = providerId;
        }
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default)
    {
        var providerId = _settingsService.CurrentSettings.TranslationProvider;
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            provider = _providers.Values.FirstOrDefault();
        }

        if (provider == null)
        {
            return TranslationResult.Failed(text, "系统中未配置可用的翻译提供方。", sourceLang, targetLang);
        }

        return await provider.TranslateAsync(text, sourceLang, targetLang, cancellationToken);
    }
}
