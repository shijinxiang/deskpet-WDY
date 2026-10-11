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
            if (_providers.TryGetValue("WindowsNative", out var nativeFallback))
            {
                provider = nativeFallback;
            }
            else
            {
                provider = _providers.Values.FirstOrDefault();
            }
        }

        if (provider == null)
        {
            return OcrResult.Failed("系统中未注册可用的 OCR 识别引擎。");
        }

        // 如果配置了百度云但未填写 API 凭据，自动无缝降级为 Windows 本地原生 OCR
        if (provider.ProviderId.Equals("BaiduCloud", StringComparison.OrdinalIgnoreCase))
        {
            var s = _settingsService.CurrentSettings;
            if (string.IsNullOrWhiteSpace(s.BaiduOcrApiKey) || string.IsNullOrWhiteSpace(s.BaiduOcrSecretKey))
            {
                if (_providers.TryGetValue("WindowsNative", out var nativeFallback))
                {
                    return await nativeFallback.RecognizeTextAsync(imageBytes, language, cancellationToken);
                }
            }
        }

        var result = await provider.RecognizeTextAsync(imageBytes, language, cancellationToken);

        // 如果百度云失败，尝试本地原生 OCR 智能兜底
        if (!result.Success && provider.ProviderId.Equals("BaiduCloud", StringComparison.OrdinalIgnoreCase))
        {
            if (_providers.TryGetValue("WindowsNative", out var nativeFallback))
            {
                var fallbackResult = await nativeFallback.RecognizeTextAsync(imageBytes, language, cancellationToken);
                if (fallbackResult.Success)
                {
                    return fallbackResult;
                }
            }
        }

        return result;
    }
}
