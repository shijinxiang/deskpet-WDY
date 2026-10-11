using System.Text.Json;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;

namespace TranslatePet.Core.Services.Vocabulary;

public class VocabularyService : IVocabularyService
{
    private readonly string _filePath;
    private readonly List<VocabularyItem> _items = new();
    private readonly object _lock = new();

    public event EventHandler? VocabularyChanged;

    public VocabularyService(string? customPath = null)
    {
        if (!string.IsNullOrEmpty(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appDir = Path.Combine(appData, "TranslatePet");
            Directory.CreateDirectory(appDir);
            _filePath = Path.Combine(appDir, "vocabulary.json");
        }

        Load();
    }

    public IReadOnlyList<VocabularyItem> GetAllItems()
    {
        lock (_lock)
        {
            return _items.OrderByDescending(i => i.CreatedAt).ToList();
        }
    }

    public IReadOnlyList<VocabularyItem> GetItemsByBook(string? bookType = null)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(bookType) || bookType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                return _items.OrderByDescending(i => i.CreatedAt).ToList();
            }

            return _items
                .Where(i => i.BookType.Equals(bookType, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.CreatedAt)
                .ToList();
        }
    }

    public IReadOnlyList<VocabularyItem> GetItemsDueForReview(string? bookType = null)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var query = _items.Where(i => !i.IsMastered && i.NextReviewDate <= now);
            if (!string.IsNullOrEmpty(bookType) && !bookType.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(i => i.BookType.Equals(bookType, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderBy(i => i.NextReviewDate).ToList();
        }
    }

    public async Task AddItemAsync(VocabularyItem item)
    {
        lock (_lock)
        {
            // 如果单词已存在，更新释义与例句及本子分类
            var existing = _items.FirstOrDefault(i => i.Word.Equals(item.Word.Trim(), StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Meaning = item.Meaning;
                if (!string.IsNullOrWhiteSpace(item.Phonetic)) existing.Phonetic = item.Phonetic;
                if (!string.IsNullOrWhiteSpace(item.ContextSentence)) existing.ContextSentence = item.ContextSentence;
                if (!string.IsNullOrWhiteSpace(item.ContextTranslation)) existing.ContextTranslation = item.ContextTranslation;
                if (!string.IsNullOrWhiteSpace(item.MemoryTip)) existing.MemoryTip = item.MemoryTip;
                if (!string.IsNullOrWhiteSpace(item.BookType)) existing.BookType = item.BookType;
            }
            else
            {
                item.Word = item.Word.Trim();
                if (string.IsNullOrWhiteSpace(item.BookType)) item.BookType = "Vocabulary";
                item.CreatedAt = DateTime.UtcNow;
                item.NextReviewDate = DateTime.UtcNow; // 新加入单词立即可复习
                _items.Add(item);
            }
        }

        await SaveAsync();
        VocabularyChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task UpdateItemAsync(VocabularyItem item)
    {
        lock (_lock)
        {
            var index = _items.FindIndex(i => i.Id == item.Id);
            if (index >= 0)
            {
                _items[index] = item;
            }
        }

        await SaveAsync();
        VocabularyChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteItemAsync(string id)
    {
        lock (_lock)
        {
            _items.RemoveAll(i => i.Id == id);
        }

        await SaveAsync();
        VocabularyChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RecordReviewResultAsync(string id, bool remembered)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            item.ReviewCount++;

            if (remembered)
            {
                item.MemoryStage++;
                int daysToAdd = item.MemoryStage switch
                {
                    1 => 1,  // 1天后第1次复习
                    2 => 2,  // 2天后第2次复习
                    3 => 4,  // 4天后第3次复习
                    4 => 7,  // 7天后第4次复习
                    5 => 15, // 15天后第5次复习
                    _ => 30  // 30天后第6次巩固，达标后掌握
                };

                if (item.MemoryStage >= 6)
                {
                    item.IsMastered = true;
                }

                item.NextReviewDate = DateTime.UtcNow.AddDays(daysToAdd);
            }
            else
            {
                // 没记住，回退记忆阶段并安排近期重温 (12小时后)
                item.MemoryStage = Math.Max(0, item.MemoryStage - 1);
                item.IsMastered = false;
                item.NextReviewDate = DateTime.UtcNow.AddHours(12);
            }
        }

        await SaveAsync();
        VocabularyChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task RecordTypingResultAsync(string id, bool completedWithoutError, int mistakeCount)
    {
        lock (_lock)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item == null) return;

            item.TypingPracticedCount++;
            item.TypingMistakes += mistakeCount;
            item.LastTypingPracticedAt = DateTime.UtcNow;

            if (completedWithoutError)
            {
                // 无错全对完成打字，促进记忆
                item.MemoryStage = Math.Min(6, item.MemoryStage + 1);
                if (item.MemoryStage >= 6) item.IsMastered = true;
                item.NextReviewDate = DateTime.UtcNow.AddDays(Math.Max(1, item.MemoryStage * 2));
            }
        }

        await SaveAsync();
        VocabularyChanged?.Invoke(this, EventArgs.Empty);
    }

    public VocabularyStats GetStats()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);

            return new VocabularyStats
            {
                TotalWords = _items.Count,
                VocabularyBookWords = _items.Count(i => i.BookType == "Vocabulary"),
                MemoryBookWords = _items.Count(i => i.BookType == "Memory"),
                DueReviewWords = _items.Count(i => !i.IsMastered && i.NextReviewDate <= now),
                MasteredWords = _items.Count(i => i.IsMastered),
                ReviewedTodayWords = _items.Count(i => i.ReviewCount > 0 && i.NextReviewDate.Date > today),
                AddedThisWeekWords = _items.Count(i => i.CreatedAt >= weekAgo),
                TotalTypingPracticed = _items.Sum(i => i.TypingPracticedCount)
            };
        }
    }

    public void Load()
    {
        lock (_lock)
        {
            _items.Clear();
            try
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var loaded = JsonSerializer.Deserialize<List<VocabularyItem>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        _items.AddRange(loaded);
                        return;
                    }
                }
            }
            catch
            {
                // Ignore and seed defaults
            }

            // 初始化种子示范生词 (地道社交与日常口语常用)
            SeedSampleWords();
        }
    }

    public Task LoadAsync()
    {
        Load();
        return Task.CompletedTask;
    }

    private void SeedSampleWords()
    {
        _items.Add(new VocabularyItem
        {
            Word = "hang out",
            Phonetic = "/hæŋ aʊt/",
            Meaning = "v. (和朋友) 闲逛；出去玩；聚在一起消磨时光",
            ContextSentence = "Hey! Are you free this weekend? Let's hang out at the new cafe downtown.",
            ContextTranslation = "嘿！这个周末有空吗？咱们去市中心新开的咖啡馆聚聚吧。",
            MemoryTip = "【社交高频】老外日常极高频口语，比严肃的 'meet' 或 'play' 更加地道自然。",
            Tags = "地道口语",
            BookType = "Vocabulary",
            NextReviewDate = DateTime.UtcNow
        });

        _items.Add(new VocabularyItem
        {
            Word = "catch up",
            Phonetic = "/kætʃ ʌp/",
            Meaning = "v. 叙旧；交流近况；赶上",
            ContextSentence = "Long time no see! We definitely need to catch up over coffee soon.",
            ContextTranslation = "好久不见啦！我们真的得找机会喝杯咖啡好好叙叙旧了。",
            MemoryTip = "【老友重逢】外国友人好久不见时必说短语，表示更新彼此的生活动态。",
            Tags = "地道口语",
            BookType = "Memory",
            NextReviewDate = DateTime.UtcNow
        });

        _items.Add(new VocabularyItem
        {
            Word = "no biggie",
            Phonetic = "/noʊ ˈbɪɡi/",
            Meaning = "phr. 没事啦；小事一桩；不用客气 (口语简化版 No big deal)",
            ContextSentence = "Thanks for helping me move the desk! — No biggie, anytime!",
            ContextTranslation = "谢谢你帮我搬桌子！—— 小事一桩啦，随叫随到！",
            MemoryTip = "【生活俚语】轻快活泼的口语回应，比标准的 'You are welcome' 更加亲近自然。",
            Tags = "日常俚语",
            BookType = "Vocabulary",
            NextReviewDate = DateTime.UtcNow
        });
    }

    public async Task SaveAsync()
    {
        string json;
        lock (_lock)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            json = JsonSerializer.Serialize(_items, options);
        }

        try
        {
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save vocabulary: {ex.Message}");
        }
    }
}
