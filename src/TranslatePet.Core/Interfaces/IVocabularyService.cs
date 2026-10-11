using TranslatePet.Core.Models;

namespace TranslatePet.Core.Interfaces;

public interface IVocabularyService
{
    event EventHandler? VocabularyChanged;

    IReadOnlyList<VocabularyItem> GetAllItems();

    IReadOnlyList<VocabularyItem> GetItemsByBook(string? bookType = null);

    IReadOnlyList<VocabularyItem> GetItemsDueForReview(string? bookType = null);

    Task AddItemAsync(VocabularyItem item);

    Task UpdateItemAsync(VocabularyItem item);

    Task DeleteItemAsync(string id);

    Task RecordReviewResultAsync(string id, bool remembered);

    Task RecordTypingResultAsync(string id, bool completedWithoutError, int mistakeCount);

    VocabularyStats GetStats();

    Task LoadAsync();

    Task SaveAsync();
}
