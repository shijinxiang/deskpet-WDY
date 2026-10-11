using TranslatePet.Core.Models;
using TranslatePet.Core.Services.Vocabulary;
using Xunit;

namespace TranslatePet.Tests;

public class VocabularyServiceTests : IDisposable
{
    private readonly string _tempFile;

    public VocabularyServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"vocab_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public async Task VocabularyService_AddAndRetrieveItems()
    {
        var service = new VocabularyService(_tempFile);

        var item = new VocabularyItem
        {
            Word = "catch up",
            Meaning = "叙旧，交流近况",
            Tags = "口语"
        };

        await service.AddItemAsync(item);
        var all = service.GetAllItems();

        Assert.Contains(all, i => i.Word == "catch up");
    }

    [Fact]
    public async Task VocabularyService_SpacedRepetition_Progression()
    {
        var service = new VocabularyService(_tempFile);

        var item = new VocabularyItem
        {
            Word = "serendipity",
            Meaning = "意外发现珍奇事物的本领/机缘",
            NextReviewDate = DateTime.UtcNow.AddMinutes(-5)
        };

        await service.AddItemAsync(item);
        var saved = service.GetAllItems().First(i => i.Word == "serendipity");

        // Mark as remembered -> memory stage advances
        await service.RecordReviewResultAsync(saved.Id, true);
        var updated = service.GetAllItems().First(i => i.Word == "serendipity");

        Assert.Equal(1, updated.ReviewCount);
        Assert.Equal(1, updated.MemoryStage);
        Assert.True(updated.NextReviewDate > DateTime.UtcNow);

        // Mark as forgotten -> memory stage resets to 0
        await service.RecordReviewResultAsync(saved.Id, false);
        var forgotten = service.GetAllItems().First(i => i.Word == "serendipity");

        Assert.Equal(2, forgotten.ReviewCount);
        Assert.Equal(0, forgotten.MemoryStage);
        Assert.False(forgotten.IsMastered);
    }

    [Fact]
    public async Task VocabularyService_BookType_Filtering()
    {
        var service = new VocabularyService(_tempFile);

        await service.AddItemAsync(new VocabularyItem { Word = "apple", BookType = "Vocabulary" });
        await service.AddItemAsync(new VocabularyItem { Word = "banana", BookType = "Memory" });

        var vocabList = service.GetItemsByBook("Vocabulary");
        var memoryList = service.GetItemsByBook("Memory");

        Assert.Contains(vocabList, i => i.Word == "apple");
        Assert.DoesNotContain(vocabList, i => i.Word == "banana");

        Assert.Contains(memoryList, i => i.Word == "banana");
        Assert.DoesNotContain(memoryList, i => i.Word == "apple");
    }

    [Fact]
    public async Task VocabularyService_RecordTypingResult_UpdatesStats()
    {
        var service = new VocabularyService(_tempFile);

        var item = new VocabularyItem { Word = "keyboard", BookType = "Vocabulary" };
        await service.AddItemAsync(item);
        var saved = service.GetAllItems().First(i => i.Word == "keyboard");

        await service.RecordTypingResultAsync(saved.Id, completedWithoutError: true, mistakeCount: 0);
        var updated = service.GetAllItems().First(i => i.Word == "keyboard");

        Assert.Equal(1, updated.TypingPracticedCount);
        Assert.Equal(0, updated.TypingMistakes);
        Assert.NotNull(updated.LastTypingPracticedAt);
        Assert.True(updated.MemoryStage > 0);
    }
}
