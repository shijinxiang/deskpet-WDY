using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Ocr;

public class MockOcrProvider : IOcrProvider
{
    public string ProviderId => "Mock";
    public string DisplayName => "演示/测试 OCR (无需联网)";

    public async Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default)
    {
        // 模拟 400ms 处理延迟
        await Task.Delay(400, cancellationToken);

        if (imageBytes.Length == 0)
        {
            return OcrResult.Failed("截取的图像数据为空，无法进行 OCR 识别。");
        }

        var sample = "Welcome to TranslatePet Desktop Assistant!\nThis is a sample text recognized from screen snipping.\nYou can edit this text before translation.";
        return OcrResult.Succeeded(sample, confidence: 0.98f);
    }
}
