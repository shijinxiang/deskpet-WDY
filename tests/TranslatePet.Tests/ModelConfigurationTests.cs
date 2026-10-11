using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services;
using TranslatePet.Core.Services.Ocr;
using TranslatePet.Core.Services.Translation;
using Xunit;

namespace TranslatePet.Tests;

public class ModelConfigurationTests : IDisposable
{
    private readonly string _tempFile;

    public ModelConfigurationTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    private class FakeNativeOcrProvider : IOcrProvider
    {
        public string ProviderId => "WindowsNative";
        public string DisplayName => "Fake Native OCR";

        public Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OcrResult.Succeeded("Native Recognized Text"));
        }
    }

    [Fact]
    public async Task BaiduOcr_EmptyKeys_ReturnsFailurePrompt()
    {
        var settingsService = new SettingsService(_tempFile);
        var provider = new BaiduCloudOcrProvider(settingsService);

        var (ok, msg) = await provider.TestConnectionAsync("", "");
        Assert.False(ok);
        Assert.Contains("请先填入完整的", msg);
    }

    [Fact]
    public async Task OpenAiProvider_EmptyBaseUrl_ReturnsFailurePrompt()
    {
        var settingsService = new SettingsService(_tempFile);
        var provider = new OpenAiCompatibleTranslationProvider(settingsService);

        var (ok, msg) = await provider.TestConnectionAsync("", "sk-xxx", "gpt-4o-mini");
        Assert.False(ok);
        Assert.Contains("请先填写 API 端点", msg);
    }

    [Fact]
    public async Task OpenAiProvider_EmptyKeyForCloudService_ReturnsFailurePrompt()
    {
        var settingsService = new SettingsService(_tempFile);
        var provider = new OpenAiCompatibleTranslationProvider(settingsService);

        var (ok, msg) = await provider.TestConnectionAsync("https://api.deepseek.com/v1", "", "deepseek-chat");
        Assert.False(ok);
        Assert.Contains("云端大模型服务必须填写 API Key", msg);
    }

    [Fact]
    public async Task OcrService_FallsBackToNative_WhenBaiduKeysAreEmpty()
    {
        var settingsService = new SettingsService(_tempFile);
        settingsService.CurrentSettings.OcrProvider = "BaiduCloud";
        settingsService.CurrentSettings.BaiduOcrApiKey = "";
        settingsService.CurrentSettings.BaiduOcrSecretKey = "";

        var baiduProvider = new BaiduCloudOcrProvider(settingsService);
        var nativeProvider = new FakeNativeOcrProvider();

        var service = new OcrService(settingsService, new IOcrProvider[] { nativeProvider, baiduProvider });

        var dummyBytes = new byte[] { 1, 2, 3, 4 };
        var result = await service.RecognizeAsync(dummyBytes);

        Assert.True(result.Success);
        Assert.Equal("Native Recognized Text", result.Text);
    }

    [Fact]
    public void AppSettings_DefaultDeepThinking_IsDisabledForSpeed()
    {
        var settings = new AppSettings();
        Assert.False(settings.EnableDeepThinking);
    }
}
