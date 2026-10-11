using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services;

public enum WorkflowState
{
    Idle,
    Capturing,
    Selecting,
    Recognizing,
    Reviewing,
    Translating,
    Completed,
    Cancelled,
    Error
}

public class TranslationWorkflow
{
    private readonly ICaptureService _captureService;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly ISettingsService _settingsService;

    private CancellationTokenSource? _currentCts;

    public WorkflowState State { get; private set; } = WorkflowState.Idle;
    public string? LastErrorMessage { get; private set; }
    public string RecognizedText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public byte[]? CapturedImageBytes { get; private set; }

    public event EventHandler<WorkflowState>? StateChanged;

    public TranslationWorkflow(
        ICaptureService captureService,
        IOcrService ocrService,
        ITranslationService translationService,
        ISettingsService settingsService)
    {
        _captureService = captureService;
        _ocrService = ocrService;
        _translationService = translationService;
        _settingsService = settingsService;
    }

    private void SetState(WorkflowState newState, string? error = null)
    {
        State = newState;
        LastErrorMessage = error;
        StateChanged?.Invoke(this, newState);
    }

    public void CancelCurrent()
    {
        _currentCts?.Cancel();
        CapturedImageBytes = null;
        SetState(WorkflowState.Cancelled);
    }

    public void Reset()
    {
        _currentCts?.Cancel();
        CapturedImageBytes = null;
        RecognizedText = string.Empty;
        TranslatedText = string.Empty;
        LastErrorMessage = null;
        SetState(WorkflowState.Idle);
    }

    public async Task<OcrResult> ProcessSnippedRegionAsync(byte[] fullScreenBytes, ScreenRect region)
    {
        _currentCts?.Cancel();
        _currentCts = new CancellationTokenSource();
        var ct = _currentCts.Token;

        try
        {
            SetState(WorkflowState.Recognizing);
            // 裁剪指定区域
            CapturedImageBytes = await _captureService.CropRegionAsync(fullScreenBytes, region);

            var ocrResult = await _ocrService.RecognizeAsync(CapturedImageBytes, _settingsService.CurrentSettings.DefaultSourceLanguage, ct);
            if (!ocrResult.Success)
            {
                SetState(WorkflowState.Error, ocrResult.ErrorMessage);
                return ocrResult;
            }

            RecognizedText = ocrResult.Text;
            SetState(WorkflowState.Reviewing);
            return ocrResult;
        }
        catch (OperationCanceledException)
        {
            SetState(WorkflowState.Cancelled);
            return OcrResult.Failed("操作已取消");
        }
        catch (Exception ex)
        {
            SetState(WorkflowState.Error, ex.Message);
            return OcrResult.Failed(ex.Message);
        }
    }

    public async Task<TranslationResult> TranslateTextAsync(string text, string sourceLang, string targetLang, string? customPrompt = null)
    {
        _currentCts?.Cancel();
        _currentCts = new CancellationTokenSource();
        var ct = _currentCts.Token;

        try
        {
            SetState(WorkflowState.Translating);
            RecognizedText = text;

            var result = await _translationService.TranslateAsync(text, sourceLang, targetLang, customPrompt, ct);
            if (!result.Success)
            {
                SetState(WorkflowState.Error, result.ErrorMessage);
                return result;
            }

            TranslatedText = result.TranslatedText;
            SetState(WorkflowState.Completed);
            return result;
        }
        catch (OperationCanceledException)
        {
            SetState(WorkflowState.Cancelled);
            return TranslationResult.Failed(text, "翻译已取消", sourceLang, targetLang);
        }
        catch (Exception ex)
        {
            SetState(WorkflowState.Error, ex.Message);
            return TranslationResult.Failed(text, ex.Message, sourceLang, targetLang);
        }
    }
}
