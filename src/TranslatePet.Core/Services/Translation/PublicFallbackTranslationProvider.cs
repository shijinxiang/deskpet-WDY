using System.Net.Http.Json;
using System.Text.Json;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Translation;

public class PublicFallbackTranslationProvider : ITranslationProvider
{
    private readonly HttpClient _httpClient;

    public string ProviderId => "PublicMyMemory";
    public string DisplayName => "公共免密翻译通道 (MyMemory, 免配置即用)";

    public PublicFallbackTranslationProvider(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslationResult.Failed(text, "待翻译内容为空", sourceLang, targetLang);
        }

        try
        {
            var src = NormalizeLang(sourceLang);
            var tgt = NormalizeLang(targetLang);

            var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={src}|{tgt}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("responseData", out var responseData) &&
                responseData.TryGetProperty("translatedText", out var translatedProp))
            {
                var translated = translatedProp.GetString() ?? string.Empty;
                return TranslationResult.Succeeded(text, translated, sourceLang, targetLang);
            }

            return TranslationResult.Failed(text, "公共翻译服务响应解析失败。", sourceLang, targetLang);
        }
        catch (OperationCanceledException)
        {
            return TranslationResult.Failed(text, "翻译已取消。", sourceLang, targetLang);
        }
        catch (Exception ex)
        {
            return TranslationResult.Failed(text, $"公共翻译请求失败: {ex.Message}", sourceLang, targetLang);
        }
    }

    private static string NormalizeLang(string lang)
    {
        return lang.ToLowerInvariant() switch
        {
            "zh" or "zh-hans" or "zh-cn" => "zh-CN",
            "zh-hant" or "zh-tw" => "zh-TW",
            "en" => "en",
            "ja" => "ja",
            "ko" => "ko",
            "fr" => "fr",
            "de" => "de",
            "ru" => "ru",
            _ => lang
        };
    }
}
