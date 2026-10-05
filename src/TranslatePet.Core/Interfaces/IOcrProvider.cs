using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface IOcrProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    Task<OcrResult> RecognizeTextAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default);
}
