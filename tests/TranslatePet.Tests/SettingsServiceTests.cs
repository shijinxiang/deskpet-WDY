using TranslatePet.Core.Services;
using Xunit;

namespace TranslatePet.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFile;

    public SettingsServiceTests()
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

    [Fact]
    public async Task SettingsService_LoadDefault_WhenFileDoesNotExist()
    {
        var service = new SettingsService(_tempFile);
        await service.LoadSettingsAsync();

        Assert.NotNull(service.CurrentSettings);
        Assert.Equal("Alt", service.CurrentSettings.HotkeyAlt ? "Alt" : "");
        Assert.Equal("Q", service.CurrentSettings.HotkeyKey);
        Assert.Equal("en", service.CurrentSettings.DefaultSourceLanguage);
        Assert.Equal("zh-Hans", service.CurrentSettings.DefaultTargetLanguage);
        Assert.False(service.CurrentSettings.SaveHistory);
    }

    [Fact]
    public async Task SettingsService_SaveAndReload_PersistsChanges()
    {
        var service = new SettingsService(_tempFile);
        await service.LoadSettingsAsync();

        service.CurrentSettings.HotkeyKey = "T";
        service.CurrentSettings.BaiduOcrApiKey = "test_key_123";
        service.CurrentSettings.OpenAiModel = "deepseek-chat";
        await service.SaveSettingsAsync();

        var reloadedService = new SettingsService(_tempFile);
        await reloadedService.LoadSettingsAsync();

        Assert.Equal("T", reloadedService.CurrentSettings.HotkeyKey);
        Assert.Equal("test_key_123", reloadedService.CurrentSettings.BaiduOcrApiKey);
        Assert.Equal("deepseek-chat", reloadedService.CurrentSettings.OpenAiModel);
    }
}
