using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.UI.Services;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Views;

public partial class VocabularyWindow : Window
{
    private readonly IVocabularyService _vocabularyService;
    private readonly ITranslationService _translationService;
    private readonly ISettingsService _settingsService;

    // 当前筛选本子: "All", "Vocabulary", "Memory"
    private string _currentBookType = "All";

    // 打字核心状态 (TypeWords)
    private bool _isDictationMode = false;
    private List<VocabularyItem> _typingQueue = new();
    private int _currentTypingIndex = 0;
    private string _targetWord = string.Empty;
    private int _cursorIndex = 0;
    private int _currentWordMistakes = 0;
    private int _streak = 0;
    private bool _isWordCompleted = false;
    private bool _isPeeking = false;

    // 艾宾浩斯复习卡片状态
    private List<VocabularyItem> _dueReviewItems = new();
    private int _currentReviewIndex = 0;
    private bool _isInitialized = false;

    public VocabularyWindow(
        IVocabularyService vocabularyService,
        ITranslationService translationService,
        ISettingsService settingsService)
    {
        _vocabularyService = vocabularyService;
        _translationService = translationService;
        _settingsService = settingsService;

        InitializeComponent();
        WindowBlurHelper.EnableBlur(this);

        _vocabularyService.VocabularyChanged += (s, e) => Dispatcher.Invoke(RefreshAllViews);

        _isInitialized = true;
        RefreshAllViews();
    }

    private void RefreshAllViews()
    {
        UpdateHeaderStats();
        ReloadTypingQueue();
        LoadReviewQueue();
        RefreshWordsList();
    }

    private void UpdateHeaderStats()
    {
        var stats = _vocabularyService.GetStats();
        HeaderStatsTextBlock.Text = $"📊 词库: {stats.TotalWords} | 📗生词: {stats.VocabularyBookWords} | 🧠记忆: {stats.MemoryBookWords}";
        TypingTotalCountTextBlock.Text = $"累计击键练习: {stats.TotalTypingPracticed} 次";
    }

    #region Tab 1: TypeWords 核心打字引擎

    private void ReloadTypingQueue(bool resetIndex = true)
    {
        _typingQueue = _vocabularyService.GetItemsByBook(_currentBookType).ToList();
        if (resetIndex)
        {
            _currentTypingIndex = 0;
        }
        LoadCurrentTypingWord();
    }

    private void LoadCurrentTypingWord()
    {
        if (_typingQueue.Count == 0 || _currentTypingIndex >= _typingQueue.Count)
        {
            TypingStageBorder.Visibility = Visibility.Collapsed;
            TypingCompletedBorder.Visibility = Visibility.Visible;
            TypingWordIndexTextBlock.Text = $"全部完成 ({_typingQueue.Count}/{_typingQueue.Count})";
            CompletedSummaryTextBlock.Text = _typingQueue.Count == 0
                ? "当前本子暂无词汇，可去词库管理添加或在截图翻译中一键收录！"
                : $"手指肌肉记忆已稳固建立！当前连对: {_streak} 🔥";
            return;
        }

        TypingCompletedBorder.Visibility = Visibility.Collapsed;
        TypingStageBorder.Visibility = Visibility.Visible;

        var item = _typingQueue[_currentTypingIndex];
        _targetWord = item.Word.Trim();
        _cursorIndex = 0;
        _currentWordMistakes = 0;
        _isWordCompleted = false;
        _isPeeking = false;

        TypingWordIndexTextBlock.Text = $"第 {_currentTypingIndex + 1} / {_typingQueue.Count} 词";
        TypingStreakTextBlock.Text = $"🔥 连对: {_streak}";

        // 音标与本子标识
        TypePhoneticTextBlock.Text = string.IsNullOrWhiteSpace(item.Phonetic) ? "" : item.Phonetic;
        bool isVocab = item.BookType != "Memory";
        CurrentBookTextBlock.Text = isVocab ? "📗 生词本" : "🧠 记忆本";
        CurrentBookBadge.Background = isVocab ? new SolidColorBrush(Color.FromRgb(0x1E, 0x2B, 0x25)) : new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x42));
        CurrentBookTextBlock.Foreground = isVocab ? new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99)) : new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
        SwitchBookButton.Content = isVocab ? "⇄ 移至记忆本" : "⇄ 移至生词本";

        // 释义与例句
        TypeMeaningTextBlock.Text = item.Meaning;
        TypeContextTransTextBlock.Text = string.IsNullOrWhiteSpace(item.ContextTranslation) ? "(暂无翻译)" : item.ContextTranslation;
        TypeMemoryTipTextBlock.Text = string.IsNullOrWhiteSpace(item.MemoryTip)
            ? "【地道小贴士】通过键盘拼写输入形成肌肉记忆，效果提升数倍。"
            : item.MemoryTip;

        // 例句展示 (默写模式下挖空目标词)
        if (string.IsNullOrWhiteSpace(item.ContextSentence))
        {
            TypeContextSentenceTextBlock.Text = "(暂无例句)";
        }
        else if (_isDictationMode)
        {
            // 挖空替换
            string masked = ReplaceWordWithBlanks(item.ContextSentence, _targetWord);
            TypeContextSentenceTextBlock.Text = masked;
        }
        else
        {
            TypeContextSentenceTextBlock.Text = item.ContextSentence;
        }

        // 提示文本
        TypingHintTextBlock.Text = _isDictationMode
            ? "默写模式: 根据中文释义敲键盘盲打！按 Space 可提示下一个字母"
            : "跟打模式: 对照字母敲击键盘！敲错时槽位会红闪提示";

        RenderWordSlots();
    }

    private string ReplaceWordWithBlanks(string sentence, string word)
    {
        try
        {
            var pattern = System.Text.RegularExpressions.Regex.Escape(word);
            return System.Text.RegularExpressions.Regex.Replace(
                sentence,
                pattern,
                new string('_', Math.Max(4, word.Length)),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return sentence;
        }
    }

    private void RenderWordSlots()
    {
        WordSlotsPanel.Children.Clear();

        for (int i = 0; i < _targetWord.Length; i++)
        {
            char targetChar = _targetWord[i];
            bool isSpace = targetChar == ' ';

            var border = new Border
            {
                Width = isSpace ? 28 : (targetChar == '\'' || targetChar == '-' ? 32 : 44),
                Height = 52,
                CornerRadius = new CornerRadius(7),
                Margin = new Thickness(3, 2, 3, 2),
                Tag = i
            };

            var textBlock = new TextBlock
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Courier New, Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (isSpace)
            {
                // 空格卡槽
                if (i < _cursorIndex)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x13, 0x30, 0x25));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                    border.BorderThickness = new Thickness(1);
                    textBlock.Text = "·";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                }
                else if (i == _cursorIndex)
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x2E, 0x4A));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                    border.BorderThickness = new Thickness(2);
                    textBlock.Text = "␣";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
                }
                else
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x26));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x3E));
                    border.BorderThickness = new Thickness(1);
                    textBlock.Text = "␣";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                }
            }
            else if (i < _cursorIndex)
            {
                // 已输入正确的字母
                border.Background = new SolidColorBrush(Color.FromRgb(0x13, 0x30, 0x25));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
                border.BorderThickness = new Thickness(1.5);
                textBlock.Text = targetChar.ToString();
                textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x34, 0xD3, 0x99));
            }
            else if (i == _cursorIndex)
            {
                // 当前待输入光标槽位
                border.Background = new SolidColorBrush(Color.FromRgb(0x22, 0x2E, 0x4A));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8));
                border.BorderThickness = new Thickness(2);

                if (_isDictationMode && !_isPeeking)
                {
                    textBlock.Text = "_";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
                }
                else
                {
                    textBlock.Text = targetChar.ToString();
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                }
            }
            else
            {
                // 尚未到达的槽位
                border.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x19, 0x26));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x2B, 0x3E));
                border.BorderThickness = new Thickness(1);

                if (_isDictationMode)
                {
                    textBlock.Text = "_";
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69));
                }
                else
                {
                    textBlock.Text = targetChar.ToString();
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                }
            }

            border.Child = textBlock;
            WordSlotsPanel.Children.Add(border);
        }
    }

    private void FlashCurrentSlotError()
    {
        if (_cursorIndex >= 0 && _cursorIndex < WordSlotsPanel.Children.Count)
        {
            if (WordSlotsPanel.Children[_cursorIndex] is Border border)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x1A, 0x24));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
                border.BorderThickness = new Thickness(2);
                if (border.Child is TextBlock tb)
                {
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0xF4, 0x3F, 0x5E));
                }
            }
        }
    }

    private void ProcessKeyInput(Key key)
    {
        if (_isWordCompleted) return;
        if (_cursorIndex >= _targetWord.Length) return;

        char targetChar = _targetWord[_cursorIndex];

        // 处理退格
        if (key == Key.Back)
        {
            if (_cursorIndex > 0)
            {
                _cursorIndex--;
                TypingSoundService.PlayClick();
                RenderWordSlots();
            }
            return;
        }

        // 处理跳过
        if (key == Key.Escape)
        {
            OnSkipWordClick(this, new RoutedEventArgs());
            return;
        }

        // 处理空格
        if (key == Key.Space)
        {
            if (targetChar == ' ')
            {
                // 正确敲击空格
                _cursorIndex++;
                TypingSoundService.PlayClick();
                CheckIfWordCompleted();
            }
            else if (_isDictationMode)
            {
                // 默写模式下按空格偷看提示
                PeekCurrentCharHint();
            }
            return;
        }

        // 提取按键对应的字符
        char? inputChar = MapKeyToChar(key);
        if (inputChar == null) return;

        // 比对输入字符与目标字符 (大小写不敏感)
        if (char.ToLowerInvariant(inputChar.Value) == char.ToLowerInvariant(targetChar))
        {
            // 匹配正确！
            _cursorIndex++;
            TypingSoundService.PlayClick();
            CheckIfWordCompleted();
        }
        else
        {
            // 输入错误
            _currentWordMistakes++;
            TypingSoundService.PlayError();
            FlashCurrentSlotError();
        }
    }

    private char? MapKeyToChar(Key key)
    {
        if (key >= Key.A && key <= Key.Z)
        {
            return (char)('a' + (key - Key.A));
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return (char)('0' + (key - Key.D0));
        }

        return key switch
        {
            Key.OemMinus => '-',
            Key.OemQuotes => '\'',
            Key.OemComma => ',',
            Key.OemPeriod => '.',
            _ => null
        };
    }

    private async void CheckIfWordCompleted()
    {
        // 自动跳过连续的非字母标点或处理结束
        while (_cursorIndex < _targetWord.Length && _targetWord[_cursorIndex] == ' ')
        {
            // 如果下一个是空格，可自动前进或保留让用户敲
            break;
        }

        if (_cursorIndex >= _targetWord.Length)
        {
            // 拼写全部完成！
            _isWordCompleted = true;
            RenderWordSlots();
            TypingSoundService.PlaySuccess();

            if (_currentWordMistakes == 0)
            {
                _streak++;
            }
            else
            {
                _streak = 0;
            }

            TypingStreakTextBlock.Text = $"🔥 连对: {_streak}";
            TypingHintTextBlock.Text = "✨ 拼写完成！太棒啦！";

            // 记录到词汇服务
            if (_currentTypingIndex < _typingQueue.Count)
            {
                var item = _typingQueue[_currentTypingIndex];
                await _vocabularyService.RecordTypingResultAsync(item.Id, _currentWordMistakes == 0, _currentWordMistakes);
            }

            // 平滑进入下一词
            await Task.Delay(480);
            _currentTypingIndex++;
            LoadCurrentTypingWord();
        }
        else
        {
            RenderWordSlots();
        }
    }

    private async void PeekCurrentCharHint()
    {
        if (_isPeeking) return;
        _isPeeking = true;
        RenderWordSlots();
        await Task.Delay(1200);
        _isPeeking = false;
        RenderWordSlots();
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 如果当前焦点在 TextBox 内，不拦截输入
        if (e.OriginalSource is TextBox) return;

        // 如果在 Tab 1 (打字模式)，全权由 TypeWords 引擎捕获
        if (MainTabControl.SelectedIndex == 0)
        {
            if (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OnRestartCurrentWordClick(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            ProcessKeyInput(e.Key);
            e.Handled = true;
        }
    }

    private void OnPracticeModeClick(object sender, RoutedEventArgs e)
    {
        _isDictationMode = false;
        BtnModePractice.Background = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
        BtnModePractice.Foreground = Brushes.White;
        BtnModeDictation.Background = new SolidColorBrush(Color.FromRgb(0x20, 0x26, 0x38));
        BtnModeDictation.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        LoadCurrentTypingWord();
    }

    private void OnDictationModeClick(object sender, RoutedEventArgs e)
    {
        _isDictationMode = true;
        BtnModeDictation.Background = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
        BtnModeDictation.Foreground = Brushes.White;
        BtnModePractice.Background = new SolidColorBrush(Color.FromRgb(0x20, 0x26, 0x38));
        BtnModePractice.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        LoadCurrentTypingWord();
    }

    private void OnHintPeekClick(object sender, RoutedEventArgs e)
    {
        PeekCurrentCharHint();
    }

    private void OnSkipWordClick(object sender, RoutedEventArgs e)
    {
        _currentTypingIndex++;
        _streak = 0;
        LoadCurrentTypingWord();
    }

    private void OnRestartCurrentWordClick(object sender, RoutedEventArgs e)
    {
        _cursorIndex = 0;
        _currentWordMistakes = 0;
        _isWordCompleted = false;
        RenderWordSlots();
    }

    private void OnRestartAllWordsClick(object sender, RoutedEventArgs e)
    {
        _currentTypingIndex = 0;
        _streak = 0;
        LoadCurrentTypingWord();
    }

    private async void OnSwitchCurrentWordBookClick(object sender, RoutedEventArgs e)
    {
        if (_currentTypingIndex < _typingQueue.Count)
        {
            var item = _typingQueue[_currentTypingIndex];
            item.BookType = item.BookType == "Memory" ? "Vocabulary" : "Memory";
            await _vocabularyService.UpdateItemAsync(item);
            UpdateHeaderStats();
            LoadCurrentTypingWord();
        }
    }

    private void OnBookFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || _vocabularyService == null) return;
        if (BookFilterComboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            _currentBookType = tag;
            ReloadTypingQueue();
            LoadReviewQueue();
            RefreshWordsList();
        }
    }

    private void OnSoundToggleClick(object sender, RoutedEventArgs e)
    {
        TypingSoundService.IsEnabled = !TypingSoundService.IsEnabled;
        SoundToggleButton.Content = TypingSoundService.IsEnabled ? "🔊 音效开" : "🔇 静音";
        SoundToggleButton.Foreground = TypingSoundService.IsEnabled
            ? new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8))
            : new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
    }

    #endregion

    #region Tab 2: 艾宾浩斯复习卡片

    private void LoadReviewQueue()
    {
        _dueReviewItems = _vocabularyService.GetItemsDueForReview(_currentBookType).ToList();
        _currentReviewIndex = 0;
        DisplayCurrentReviewCard();
    }

    private void DisplayCurrentReviewCard()
    {
        if (_dueReviewItems.Count == 0 || _currentReviewIndex >= _dueReviewItems.Count)
        {
            FlashcardBorder.Visibility = Visibility.Collapsed;
            ReviewActionButtonsPanel.Visibility = Visibility.Collapsed;
            CompletedViewBorder.Visibility = Visibility.Visible;
            ReviewProgressTextBlock.Text = $"今日待复习全部达成！(共复习 {_dueReviewItems.Count} 词)";
            return;
        }

        CompletedViewBorder.Visibility = Visibility.Collapsed;
        FlashcardBorder.Visibility = Visibility.Visible;
        ReviewProgressTextBlock.Text = $"今日复习进度: {_currentReviewIndex + 1} / {_dueReviewItems.Count}";

        var item = _dueReviewItems[_currentReviewIndex];
        CardWordTextBlock.Text = item.Word;
        CardPhoneticTextBlock.Text = string.IsNullOrWhiteSpace(item.Phonetic) ? "" : item.Phonetic;
        CardContextSentenceTextBlock.Text = string.IsNullOrWhiteSpace(item.ContextSentence)
            ? "(暂无关联例句)"
            : $"来源例句: \"{item.ContextSentence}\"";

        CardMeaningTextBlock.Text = item.Meaning;
        CardContextTransTextBlock.Text = string.IsNullOrWhiteSpace(item.ContextTranslation) ? "(暂无)" : item.ContextTranslation;
        CardMemoryTipTextBlock.Text = string.IsNullOrWhiteSpace(item.MemoryTip)
            ? "【地道小贴士】多在口语对话中实际使用，加深自然语感。"
            : item.MemoryTip;

        CardBackContentPanel.Visibility = Visibility.Collapsed;
        ReviewActionButtonsPanel.Visibility = Visibility.Collapsed;
        FlipCardButton.Visibility = Visibility.Visible;
    }

    private void OnFlipCardClick(object sender, RoutedEventArgs e)
    {
        CardBackContentPanel.Visibility = Visibility.Visible;
        FlipCardButton.Visibility = Visibility.Collapsed;
        ReviewActionButtonsPanel.Visibility = Visibility.Visible;
    }

    private async void OnReviewForgotClick(object sender, RoutedEventArgs e)
    {
        if (_currentReviewIndex < _dueReviewItems.Count)
        {
            var item = _dueReviewItems[_currentReviewIndex];
            await _vocabularyService.RecordReviewResultAsync(item.Id, false);
            _currentReviewIndex++;
            DisplayCurrentReviewCard();
        }
    }

    private async void OnReviewFuzzyClick(object sender, RoutedEventArgs e)
    {
        if (_currentReviewIndex < _dueReviewItems.Count)
        {
            var item = _dueReviewItems[_currentReviewIndex];
            await _vocabularyService.RecordReviewResultAsync(item.Id, true);
            _currentReviewIndex++;
            DisplayCurrentReviewCard();
        }
    }

    private async void OnReviewMasteredClick(object sender, RoutedEventArgs e)
    {
        if (_currentReviewIndex < _dueReviewItems.Count)
        {
            var item = _dueReviewItems[_currentReviewIndex];
            await _vocabularyService.RecordReviewResultAsync(item.Id, true);
            _currentReviewIndex++;
            DisplayCurrentReviewCard();
        }
    }

    #endregion

    #region Tab 3: 词库与本子管理

    private void RefreshWordsList()
    {
        var all = _vocabularyService.GetItemsByBook(_currentBookType);
        var query = SearchWordTextBox?.Text?.Trim().ToLowerInvariant() ?? "";

        if (!string.IsNullOrEmpty(query))
        {
            all = all.Where(i => i.Word.ToLowerInvariant().Contains(query) || i.Meaning.ToLowerInvariant().Contains(query)).ToList();
        }

        WordsListBox.ItemsSource = all;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized || _vocabularyService == null) return;
        RefreshWordsList();
    }

    private async void OnAddNewWordClick(object sender, RoutedEventArgs e)
    {
        var inputWord = Microsoft.VisualBasic.Interaction.InputBox(
            "请输入要添加的英文生词或短语：\n（哆啦A梦将自动通过 AI 为其生成音标、中文释义与记忆口诀）",
            "添加新词到生词本",
            "");

        if (string.IsNullOrWhiteSpace(inputWord)) return;

        inputWord = inputWord.Trim();

        string meaning = "用户手动添加词汇";
        string phonetic = "";
        string memoryTip = "【记忆诀窍】通过键盘打字拼写建立肌肉记忆。";

        try
        {
            var prompt = _settingsService.CurrentSettings.PromptWordExplanation;
            var aiResult = await _translationService.TranslateAsync(inputWord, "en", "zh-Hans", prompt);
            if (aiResult.Success && !string.IsNullOrWhiteSpace(aiResult.TranslatedText))
            {
                meaning = aiResult.TranslatedText;
            }
        }
        catch
        {
            // Ignore AI failure
        }

        var newItem = new VocabularyItem
        {
            Word = inputWord,
            Phonetic = phonetic,
            Meaning = meaning,
            MemoryTip = memoryTip,
            BookType = _currentBookType == "Memory" ? "Memory" : "Vocabulary",
            Tags = "用户收藏",
            CreatedAt = DateTime.UtcNow,
            NextReviewDate = DateTime.UtcNow
        };

        await _vocabularyService.AddItemAsync(newItem);
        MessageBox.Show($"单词 [{inputWord}] 已成功收录到 [{newItem.BookType}]！", "收录成功", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnItemBookTypeToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            var all = _vocabularyService.GetAllItems();
            var item = all.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.BookType = item.BookType == "Memory" ? "Vocabulary" : "Memory";
                await _vocabularyService.UpdateItemAsync(item);
            }
        }
    }

    private async void OnDeleteWordClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string id)
        {
            if (MessageBox.Show("确定要从词库中移除该单词吗？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _vocabularyService.DeleteItemAsync(id);
            }
        }
    }

    private void OnGoToWordListTabClick(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = 2;
    }

    private void OnGoToManagerTabClick(object sender, RoutedEventArgs e)
    {
        MainTabControl.SelectedIndex = 2;
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != MainTabControl || !_isInitialized) return;
        if (MainTabControl.SelectedIndex == 0)
        {
            Focus();
        }
    }

    #endregion

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox && e.OriginalSource is not Button && e.OriginalSource is not ListBox && e.OriginalSource is not ComboBox)
        {
            DragMove();
        }
    }
}
