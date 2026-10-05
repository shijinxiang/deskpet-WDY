using System.Net.Http.Headers;
using System.Text.Json;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Ocr;

public class BaiduCloudOcrProvider : IOcrProvider
{
    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private string? _cachedAccessToken;
    private DateTime _tokenExpireTime = DateTime.MinValue;

    public string ProviderId => "BaiduCloud";
    public string DisplayName => "百度云通用文字识别 (Cloud OCR)";

    public BaiduCloudOcrProvider(ISettingsService settingsService, HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default)
    {
        var settings = _settingsService.CurrentSettings;
        if (string.IsNullOrWhiteSpace(settings.BaiduOcrApiKey) || string.IsNullOrWhiteSpace(settings.BaiduOcrSecretKey))
        {
            return OcrResult.Failed("未配置百度 OCR API 凭据。请在桌宠设置中填入 API Key 与 Secret Key。");
        }

        try
        {
            var token = await GetAccessTokenAsync(settings.BaiduOcrApiKey, settings.BaiduOcrSecretKey, cancellationToken);
            if (string.IsNullOrEmpty(token))
            {
                return OcrResult.Failed("获取百度 OCR Access Token 失败，请检查 API Key 和 Secret Key 是否正确。");
            }

            var requestUrl = $"https://aip.baidubce.com/rest/2.0/ocr/v1/general_basic?access_token={token}";
            var base64Image = Convert.ToBase64String(imageBytes);

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("image", base64Image),
                new KeyValuePair<string, string>("language_type", MapLanguage(language))
            });

            using var response = await _httpClient.PostAsync(requestUrl, formContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;

            if (root.TryGetProperty("error_code", out var errorCodeProp))
            {
                var errorMsg = root.TryGetProperty("error_msg", out var msgProp) ? msgProp.GetString() : "未知错误";
                return OcrResult.Failed($"百度 OCR 服务返回错误: [{errorCodeProp.GetInt32()}] {errorMsg}");
            }

            if (root.TryGetProperty("words_result", out var wordsResultProp) && wordsResultProp.ValueKind == JsonValueKind.Array)
            {
                var lines = new List<string>();
                foreach (var item in wordsResultProp.EnumerateArray())
                {
                    if (item.TryGetProperty("words", out var wordsProp))
                    {
                        var line = wordsProp.GetString();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            lines.Add(line);
                        }
                    }
                }

                var fullText = string.Join(Environment.NewLine, lines);
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    return OcrResult.Failed("未在选区中识别到任何文字，请重新框选。");
                }

                return OcrResult.Succeeded(fullText);
            }

            return OcrResult.Failed("百度 OCR 响应格式未能解析。");
        }
        catch (OperationCanceledException)
        {
            return OcrResult.Failed("OCR 识别任务已取消。");
        }
        catch (Exception ex)
        {
            return OcrResult.Failed($"OCR 网络或请求异常: {ex.Message}");
        }
    }

    private async Task<string?> GetAccessTokenAsync(string apiKey, string secretKey, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_cachedAccessToken) && DateTime.UtcNow < _tokenExpireTime)
        {
            return _cachedAccessToken;
        }

        var tokenUrl = $"https://aip.baidubce.com/oauth/2.0/token?grant_type=client_credentials&client_id={apiKey.Trim()}&client_secret={secretKey.Trim()}";
        using var response = await _httpClient.GetAsync(tokenUrl, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenProp))
        {
            _cachedAccessToken = tokenProp.GetString();
            if (doc.RootElement.TryGetProperty("expires_in", out var expiresProp))
            {
                _tokenExpireTime = DateTime.UtcNow.AddSeconds(expiresProp.GetInt32() - 300);
            }
            return _cachedAccessToken;
        }

        return null;
    }

    private static string MapLanguage(string lang)
    {
        return lang.ToLowerInvariant() switch
        {
            "zh" or "zh-hans" => "CHN_ENG",
            "en" => "ENG",
            "ja" => "JAP",
            "ko" => "KOR",
            _ => "CHN_ENG"
        };
    }
}
