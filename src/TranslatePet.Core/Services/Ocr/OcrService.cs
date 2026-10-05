using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Ocr;

public class OcrService : IOcrService
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, IOcrProvider> _providers;
    private string _currentProviderId;

    public IEnumerable<IOcrProvider> AvailableProviders => _providers.Values;

    public OcrService(ISettingsService settingsService, IEnumerable<IOcrProvider> providers)
    {
        _settingsService = settingsService;
        _providers = providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
        _currentProviderId = _settingsService.CurrentSettings.OcrProvider;
    }

    public void SetCurrentProvider(string providerId)
    {
        if (_providers.ContainsKey(providerId))
        {
            _currentProviderId = providerId;
            _settingsService.CurrentSettings.OcrProvider = providerId;
        }
    }

    public async Task<OcrResult> RecognizeAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return OcrResult.Failed("截图数据为空，未能截取到有效图像。");
        }

        var providerId = _settingsService.CurrentSettings.OcrProvider;
        if (!_providers.TryGetValue(providerId, out var provider))
        {
            provider = _providers.Values.FirstOrDefault();
        }

        if (provider == null)
        {
            return OcrResult.Failed("系统中未注册可用的 OCR 识别引擎。");
        }

        return await provider.RecognizeTextAsync(imageBytes, language, cancellationToken);
    }
}
