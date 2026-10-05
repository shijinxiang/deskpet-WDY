using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface IOcrService
{
    Task<OcrResult> RecognizeAsync(byte[] imageBytes, string language = "auto", CancellationToken cancellationToken = default);
    IEnumerable<IOcrProvider> AvailableProviders { get; }
    void SetCurrentProvider(string providerId);
}
