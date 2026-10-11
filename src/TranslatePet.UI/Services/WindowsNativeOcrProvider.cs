using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using TranslatePet.Core.Interfaces;
using AppOcrResult = TranslatePet.Core.Models.OcrResult;

namespace TranslatePet.UI.Services;

/// <summary>
/// 基于 Windows 10/11 原生 WinRT OcrEngine 的离线高精度文字识别提供方。
/// 毫秒级识别、零网络请求、完全免费免密且保护隐私。
/// </summary>
public class WindowsNativeOcrProvider : IOcrProvider
{
    public string ProviderId => "WindowsNative";
    public string DisplayName => "Windows 本地原生 OCR (免配置 / 毫秒级 / 离线)";

    /// <summary>
    /// 当前 Windows 系统中是否安装了支持 OCR 的语言包
    /// </summary>
    public static bool IsSupported => OcrEngine.AvailableRecognizerLanguages.Count > 0;

    /// <summary>
    /// 获取当前系统已安装并支持 OCR 的语言列表
    /// </summary>
    public static IReadOnlyList<string> GetSupportedLanguages()
    {
        return OcrEngine.AvailableRecognizerLanguages
            .Select(l => $"{l.DisplayName} [{l.LanguageTag}]")
            .ToList();
    }

    public async Task<AppOcrResult> RecognizeTextAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default)
    {
        if (imageBytes == null || imageBytes.Length == 0)
        {
            return AppOcrResult.Failed("截图数据为空，未能截取到有效图像。");
        }

        if (!IsSupported)
        {
            return AppOcrResult.Failed("当前 Windows 系统未检测到 OCR 语言包组件。建议在 Windows「设置 - 时间和语言 - 语言」中安装中文/英文语言包，或在桌宠设置中切换为百度云 OCR。");
        }

        try
        {
            // 1. 将图像字节转为 WinRT 随机访问流
            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(randomAccessStream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(imageBytes);
                await writer.StoreAsync();
            }

            // 2. 解码并转换为 OcrEngine 所需的 Bgra8 格式
            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            var sourceSoftwareBitmap = await decoder.GetSoftwareBitmapAsync();

            SoftwareBitmap ocrBitmap;
            if (sourceSoftwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                sourceSoftwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
            {
                ocrBitmap = SoftwareBitmap.Convert(sourceSoftwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            }
            else
            {
                ocrBitmap = sourceSoftwareBitmap;
            }

            // 3. 选择最匹配的 OCR 引擎语言
            var engine = SelectOcrEngine(language);
            if (engine == null)
            {
                return AppOcrResult.Failed("未能初始化 Windows 本地 OCR 引擎。请检查系统语言包设置。");
            }

            // 4. 执行文字识别
            var winResult = await engine.RecognizeAsync(ocrBitmap);
            if (winResult == null || winResult.Lines == null || winResult.Lines.Count == 0)
            {
                return AppOcrResult.Failed("选区内未识别到文字，请重新框选。");
            }

            var recognizedLines = new List<string>();
            foreach (var line in winResult.Lines)
            {
                var text = line.Text?.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    recognizedLines.Add(text);
                }
            }

            var fullText = string.Join(Environment.NewLine, recognizedLines);
            if (string.IsNullOrWhiteSpace(fullText))
            {
                return AppOcrResult.Failed("选区内未识别到有效文字，请重新框选。");
            }

            return AppOcrResult.Succeeded(fullText);
        }
        catch (OperationCanceledException)
        {
            return AppOcrResult.Failed("OCR 识别任务已取消。");
        }
        catch (Exception ex)
        {
            return AppOcrResult.Failed($"Windows 本地 OCR 识别异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据期望的语言代码选择最适合的本地 OCR 引擎
    /// </summary>
    private static OcrEngine? SelectOcrEngine(string language)
    {
        var available = OcrEngine.AvailableRecognizerLanguages;
        if (available.Count == 0) return null;

        var normalizedLang = language.Trim().ToLowerInvariant();

        Language? targetLanguage = null;

        if (normalizedLang == "auto")
        {
            // auto 模式优先尝试用户系统首选语言，其次寻找中文或英文
            var profileEngine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (profileEngine != null) return profileEngine;

            targetLanguage = available.FirstOrDefault(l => l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                          ?? available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                          ?? available[0];
        }
        else if (normalizedLang.StartsWith("zh"))
        {
            targetLanguage = available.FirstOrDefault(l => l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                          ?? available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        }
        else if (normalizedLang.StartsWith("en"))
        {
            targetLanguage = available.FirstOrDefault(l => l.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        }
        else if (normalizedLang.StartsWith("ja"))
        {
            targetLanguage = available.FirstOrDefault(l => l.LanguageTag.StartsWith("ja", StringComparison.OrdinalIgnoreCase));
        }
        else if (normalizedLang.StartsWith("ko"))
        {
            targetLanguage = available.FirstOrDefault(l => l.LanguageTag.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
        }

        targetLanguage ??= available[0];

        return OcrEngine.TryCreateFromLanguage(targetLanguage);
    }
}
