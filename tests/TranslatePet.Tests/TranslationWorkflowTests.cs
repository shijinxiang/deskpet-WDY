using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services;
using TranslatePet.Core.Services.Ocr;
using TranslatePet.Core.Services.Translation;
using Xunit;

namespace TranslatePet.Tests;

public class MockCaptureService : ICaptureService
{
    public Task<byte[]> CaptureVirtualScreenAsync() => Task.FromResult(new byte[] { 1, 2, 3 });
    public Task<byte[]> CropRegionAsync(byte[] fullScreenBytes, ScreenRect region) => Task.FromResult(new byte[] { 4, 5, 6 });
}

public class TranslationWorkflowTests
{
    [Fact]
    public async Task Workflow_EndToEnd_ProcessSnippedAndTranslate()
    {
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"s_{Guid.NewGuid():N}.json"));
        await settingsService.LoadSettingsAsync();

        var captureService = new MockCaptureService();
        var ocrProvider = new MockOcrProvider();
        var ocrService = new OcrService(settingsService, new[] { ocrProvider });

        var transProvider = new PublicFallbackTranslationProvider();
        var transService = new TranslationService(settingsService, new[] { transProvider });

        var workflow = new TranslationWorkflow(captureService, ocrService, transService, settingsService);

        var statesObserved = new List<WorkflowState>();
        workflow.StateChanged += (s, state) => statesObserved.Add(state);

        Assert.Equal(WorkflowState.Idle, workflow.State);

        // 1. Process Region
        var ocrResult = await workflow.ProcessSnippedRegionAsync(new byte[] { 1, 2, 3 }, new ScreenRect(0, 0, 100, 100));

        Assert.True(ocrResult.Success);
        Assert.Contains(WorkflowState.Recognizing, statesObserved);
        Assert.Equal(WorkflowState.Reviewing, workflow.State);
        Assert.False(string.IsNullOrWhiteSpace(workflow.RecognizedText));

        // 2. Translate Text
        var transResult = await workflow.TranslateTextAsync("Hello world", "en", "zh-Hans");
        Assert.True(transResult.Success);
        Assert.Equal(WorkflowState.Completed, workflow.State);
        Assert.False(string.IsNullOrWhiteSpace(workflow.TranslatedText));
    }

    [Fact]
    public void Workflow_Cancel_TransitionsToCancelledState()
    {
        var settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"s_{Guid.NewGuid():N}.json"));
        var captureService = new MockCaptureService();
        var ocrService = new OcrService(settingsService, new[] { new MockOcrProvider() });
        var transService = new TranslationService(settingsService, new[] { new PublicFallbackTranslationProvider() });

        var workflow = new TranslationWorkflow(captureService, ocrService, transService, settingsService);

        workflow.CancelCurrent();
        Assert.Equal(WorkflowState.Cancelled, workflow.State);
    }
}
