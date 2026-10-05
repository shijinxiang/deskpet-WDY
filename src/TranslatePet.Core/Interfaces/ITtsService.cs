namespace TranslatePet.Core.Interfaces;

/// <summary>
/// 文本转语音 (TTS) 扩展接口契约 (V1 仅定义抽象接口，不绑定具体实现)
/// </summary>
public interface ITtsService
{
    bool IsSupported { get; }
    bool IsPlaying { get; }
    Task SpeakAsync(string text, string language = "zh-Hans", float rate = 1.0f, CancellationToken cancellationToken = default);
    Task StopAsync();
}
