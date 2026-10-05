using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Translation;

public class OpenAiCompatibleTranslationProvider : ITranslationProvider
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;

    public string ProviderId => "OpenAiCompatible";
    public string DisplayName => "OpenAI / 大模型兼容接口 (OpenAI, DeepSeek, Ollama 等)";

    public OpenAiCompatibleTranslationProvider(ISettingsService settingsService, HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return TranslationResult.Failed(text, "待翻译内容为空", sourceLang, targetLang);
        }

        var settings = _settingsService.CurrentSettings;
        if (string.IsNullOrWhiteSpace(settings.OpenAiApiKey) && !settings.OpenAiBaseUrl.Contains("localhost") && !settings.OpenAiBaseUrl.Contains("127.0.0.1"))
        {
            return TranslationResult.Failed(text, "未配置大模型 API 密钥。请在桌宠设置中填入 API Key，或切换为公共免密翻译通道。", sourceLang, targetLang);
        }

        try
        {
            var baseUrl = settings.OpenAiBaseUrl.TrimEnd('/');
            var endpoint = baseUrl.EndsWith("/chat/completions") ? baseUrl : $"{baseUrl}/chat/completions";

            var systemPrompt = settings.OpenAiCustomPrompt.Replace("{TargetLanguage}", targetLang);

            var requestBody = new
            {
                model = string.IsNullOrWhiteSpace(settings.OpenAiModel) ? "gpt-4o-mini" : settings.OpenAiModel.Trim(),
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text }
                },
                temperature = 0.3
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = jsonContent;
            if (!string.IsNullOrWhiteSpace(settings.OpenAiApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.OpenAiApiKey.Trim());
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return TranslationResult.Failed(text, $"大模型接口返回 HTTP {(int)response.StatusCode}: {responseString}", sourceLang, targetLang);
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msgProp) && msgProp.TryGetProperty("content", out var contentProp))
                {
                    var translated = contentProp.GetString()?.Trim() ?? string.Empty;
                    return TranslationResult.Succeeded(text, translated, sourceLang, targetLang);
                }
            }

            return TranslationResult.Failed(text, "无法解析大模型响应内容。", sourceLang, targetLang);
        }
        catch (OperationCanceledException)
        {
            return TranslationResult.Failed(text, "翻译请求已取消。", sourceLang, targetLang);
        }
        catch (Exception ex)
        {
            return TranslationResult.Failed(text, $"翻译网络异常: {ex.Message}", sourceLang, targetLang);
        }
    }
}
