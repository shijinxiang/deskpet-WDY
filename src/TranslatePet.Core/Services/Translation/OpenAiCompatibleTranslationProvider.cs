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

    public async Task<TranslationResult> TranslateAsync(string text, string sourceLang, string targetLang, string? customPrompt = null, CancellationToken cancellationToken = default)
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

            // 根据场景或显式覆盖挑选专门的提示词
            string? rawPrompt = customPrompt;
            if (string.IsNullOrWhiteSpace(rawPrompt))
            {
                rawPrompt = settings.TranslationStyle switch
                {
                    "Conversational" => settings.PromptConversational,
                    "Formal" => settings.PromptFormal,
                    _ => settings.PromptStandard
                };
            }

            var systemPrompt = rawPrompt
                .Replace("{TargetLanguage}", targetLang)
                .Replace("{SourceText}", text);

            var modelToUse = string.IsNullOrWhiteSpace(settings.OpenAiModel) ? "gpt-4o-mini" : settings.OpenAiModel.Trim();

            // 如果关闭了深度思考，且当前选择的是深度推理模型，智能切换为对应极速模型，彻底避免等待数十秒
            if (!settings.EnableDeepThinking)
            {
                if (modelToUse.Equals("deepseek-reasoner", StringComparison.OrdinalIgnoreCase))
                {
                    modelToUse = "deepseek-chat";
                }
                else if (modelToUse.Equals("deepseek-ai/DeepSeek-R1", StringComparison.OrdinalIgnoreCase))
                {
                    modelToUse = "deepseek-ai/DeepSeek-V3";
                }

                systemPrompt += "\n\n【极速响应与禁用思维链指令】：严禁输出任何思维链推导过程（Chain-of-Thought），严禁输出 <think> 标签或内心推演，请直接输出最终翻译结果本体，追求秒级极速响应。";
            }
            else
            {
                systemPrompt += "\n\n【深度思考模式】：允许进行长思维链推导和深度语境推敲后输出高质量译文。";
            }

            var requestBody = new
            {
                model = modelToUse,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = text }
                },
                temperature = settings.TranslationStyle == "Conversational" ? 0.6 : 0.2
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
                if (firstChoice.TryGetProperty("message", out var msgProp))
                {
                    var content = msgProp.TryGetProperty("content", out var contentProp) ? contentProp.GetString()?.Trim() ?? string.Empty : string.Empty;

                    if (!settings.EnableDeepThinking)
                    {
                        // 过滤可能残存的 <think>...</think> 思考标签
                        content = System.Text.RegularExpressions.Regex.Replace(content, @"<think>[\s\S]*?</think>", string.Empty).Trim();
                    }
                    else if (msgProp.TryGetProperty("reasoning_content", out var reasoningProp))
                    {
                        var reasoning = reasoningProp.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(reasoning))
                        {
                            content = $"【🧠 深度推导过程】\n{reasoning}\n\n【🎯 译文】\n{content}";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        return TranslationResult.Succeeded(text, content, sourceLang, targetLang);
                    }
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

    public async Task<(bool Success, string Message)> TestConnectionAsync(string baseUrl, string apiKey, string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return (false, "请先填写 API 端点 (Base URL)。");
        }

        var isLocal = baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1");
        if (string.IsNullOrWhiteSpace(apiKey) && !isLocal)
        {
            return (false, "云端大模型服务必须填写 API Key。");
        }

        try
        {
            var trimmedBase = baseUrl.TrimEnd('/');
            var endpoint = trimmedBase.EndsWith("/chat/completions") ? trimmedBase : $"{trimmedBase}/chat/completions";

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var requestBody = new
            {
                model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim(),
                messages = new object[]
                {
                    new { role = "user", content = "Reply with 'Hello' if you can receive this." }
                },
                max_tokens = 20,
                temperature = 0.1
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = jsonContent;

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            }

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            using var response = await _httpClient.SendAsync(request, linkedCts.Token);
            sw.Stop();

            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var snippet = responseString.Length > 200 ? responseString.Substring(0, 200) + "..." : responseString;
                return (false, $"❌ 接口返回错误 [HTTP {(int)response.StatusCode}]:\n{snippet}");
            }

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var msgProp) && msgProp.TryGetProperty("content", out var contentProp))
                {
                    var reply = contentProp.GetString()?.Trim() ?? "OK";
                    return (true, $"✅ 大模型连通成功！(响应耗时: {sw.ElapsedMilliseconds}ms)\n回复内容: \"{reply}\"");
                }
            }

            return (true, $"✅ 接口连接成功 (HTTP 200，耗时 {sw.ElapsedMilliseconds}ms)。");
        }
        catch (OperationCanceledException)
        {
            return (false, "❌ 连接超时 (15秒内无响应)，请检查网络连接、科学代理或 Base URL 是否正确。");
        }
        catch (Exception ex)
        {
            return (false, $"❌ 请求异常: {ex.Message}");
        }
    }
}
