using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Views;

public partial class ResultWindow : Window
{
    private readonly TranslationWorkflow _workflow;
    private readonly ISettingsService _settingsService;
    private readonly IVocabularyService _vocabularyService;
    private readonly ITranslationService _translationService;
    private readonly Action? _onOpenVocabularyWindow;
    private readonly Action? _onOpenSettingsWindow;
    private bool _isInitializing = true;

    private record LangItem(string Name, string Code);

    private readonly List<LangItem> _sourceLangs = new()
    {
        new("自动检测", "auto"),
        new("英语 (EN)", "en"),
        new("中文 (ZH)", "zh"),
        new("日语 (JA)", "ja"),
        new("韩语 (KO)", "ko"),
        new("法语 (FR)", "fr"),
        new("德语 (DE)", "de"),
        new("俄语 (RU)", "ru")
    };

    private readonly List<LangItem> _targetLangs = new()
    {
        new("简体中文", "zh-Hans"),
        new("英语 (EN)", "en"),
        new("日语 (JA)", "ja"),
        new("韩语 (KO)", "ko"),
        new("繁体中文", "zh-Hant"),
        new("法语 (FR)", "fr"),
        new("德语 (DE)", "de"),
        new("俄语 (RU)", "ru")
    };

    public ResultWindow(
        TranslationWorkflow workflow,
        ISettingsService settingsService,
        IVocabularyService vocabularyService,
        ITranslationService translationService,
        Action? onOpenVocabularyWindow = null,
        Action? onOpenSettingsWindow = null)
    {
        InitializeComponent();
        WindowBlurHelper.EnableBlur(this);
        _workflow = workflow;
        _settingsService = settingsService;
        _vocabularyService = vocabularyService;
        _translationService = translationService;
        _onOpenVocabularyWindow = onOpenVocabularyWindow;
        _onOpenSettingsWindow = onOpenSettingsWindow;

        InitializeLanguages();
        UpdateModePillsUI();
    }

    private void InitializeLanguages()
    {
        SourceLangComboBox.ItemsSource = _sourceLangs;
        SourceLangComboBox.DisplayMemberPath = "Name";
        SourceLangComboBox.SelectedValuePath = "Code";

        TargetLangComboBox.ItemsSource = _targetLangs;
        TargetLangComboBox.DisplayMemberPath = "Name";
        TargetLangComboBox.SelectedValuePath = "Code";

        var defaultSrc = _settingsService.CurrentSettings.DefaultSourceLanguage;
        var defaultTgt = _settingsService.CurrentSettings.DefaultTargetLanguage;

        SourceLangComboBox.SelectedValue = _sourceLangs.Any(l => l.Code == defaultSrc) ? defaultSrc : "auto";
        TargetLangComboBox.SelectedValue = _targetLangs.Any(l => l.Code == defaultTgt) ? defaultTgt : "zh-Hans";

        _isInitializing = false;
    }

    private void UpdateModePillsUI()
    {
        var style = _settingsService.CurrentSettings.TranslationStyle;

        var activeBg = new SolidColorBrush(Color.FromRgb(0x4F, 0x46, 0xE5));
        var inactiveBg = Brushes.Transparent;
        var activeFg = Brushes.White;
        var inactiveFg = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        BtnModeConversational.Background = style == "Conversational" ? activeBg : inactiveBg;
        BtnModeConversational.Foreground = style == "Conversational" ? activeFg : inactiveFg;

        BtnModeStandard.Background = style == "Standard" ? activeBg : inactiveBg;
        BtnModeStandard.Foreground = style == "Standard" ? activeFg : inactiveFg;

        BtnModeFormal.Background = style == "Formal" ? activeBg : inactiveBg;
        BtnModeFormal.Foreground = style == "Formal" ? activeFg : inactiveFg;

        TranslationHeaderBlock.Text = style switch
        {
            "Conversational" => "译文 (🗣️ 地道口语对话模式):",
            "Formal" => "译文 (💼 商务正式公文模式):",
            _ => "译文 (📖 通用标准准确模式):"
        };
    }

    private void OnModeConversationalClick(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.TranslationStyle = "Conversational";
        _ = _settingsService.SaveSettingsAsync();
        UpdateModePillsUI();
        _ = ExecuteTranslationAsync();
    }

    private void OnModeStandardClick(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.TranslationStyle = "Standard";
        _ = _settingsService.SaveSettingsAsync();
        UpdateModePillsUI();
        _ = ExecuteTranslationAsync();
    }

    private void OnModeFormalClick(object sender, RoutedEventArgs e)
    {
        _settingsService.CurrentSettings.TranslationStyle = "Formal";
        _ = _settingsService.SaveSettingsAsync();
        UpdateModePillsUI();
        _ = ExecuteTranslationAsync();
    }

    public async Task LoadAndTranslateAsync(string recognizedText, string? ocrErrorMessage = null)
    {
        OriginalTextBox.Text = recognizedText;
        if (!string.IsNullOrWhiteSpace(recognizedText))
        {
            await ExecuteTranslationAsync();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(ocrErrorMessage))
            {
                StatusTextBlock.Text = $"❌ 识别提示: {ocrErrorMessage}";
                TranslationTextBox.Text = $"【未识别到有效文字】\n{ocrErrorMessage}\n\n💡 提示：若使用的是百度云 OCR 且未配置 API Key，请在桌宠设置中切换为「Windows 本地原生 OCR (免配置)」或填入凭据。";
            }
            else
            {
                StatusTextBlock.Text = "选区内未识别到文字，请编辑原文或重新框选";
                TranslationTextBox.Text = "选区内未检测到文字内容。请尝试重新截图或直接在上方原文框中手动输入待翻译内容。";
            }
        }
    }

    private async Task ExecuteTranslationAsync(string? promptOverride = null)
    {
        var text = OriginalTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            StatusTextBlock.Text = "待翻译内容为空";
            return;
        }

        var src = (SourceLangComboBox.SelectedValue as string) ?? "auto";
        var tgt = (TargetLangComboBox.SelectedValue as string) ?? "zh-Hans";

        TranslateButton.IsEnabled = false;
        FallbackActionsBorder.Visibility = Visibility.Collapsed;
        StatusTextBlock.Text = "⏳ 正在翻译中...";
        TranslationTextBox.Text = "正在根据选定模式地道翻译中，请稍候...";

        try
        {
            var result = await _workflow.TranslateTextAsync(text, src, tgt, promptOverride);
            if (result.Success)
            {
                TranslationTextBox.Text = result.TranslatedText;
                StatusTextBlock.Text = "✅ 翻译完成 (按 Esc 关闭)";
            }
            else
            {
                TranslationTextBox.Text = $"【翻译未完成】\n{result.ErrorMessage}";
                StatusTextBlock.Text = "❌ 翻译未成功";

                var err = result.ErrorMessage ?? string.Empty;
                if (err.Contains("密钥") || err.Contains("API Key") || err.Contains("未配置") || err.Contains("HTTP"))
                {
                    FallbackActionsBorder.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            TranslationTextBox.Text = $"异常: {ex.Message}";
            StatusTextBlock.Text = "❌ 发生异常";
            FallbackActionsBorder.Visibility = Visibility.Visible;
        }
        finally
        {
            TranslateButton.IsEnabled = true;
        }
    }

    private async void OnUsePublicFallbackClick(object sender, RoutedEventArgs e)
    {
        var text = OriginalTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var src = (SourceLangComboBox.SelectedValue as string) ?? "auto";
        var tgt = (TargetLangComboBox.SelectedValue as string) ?? "zh-Hans";

        StatusTextBlock.Text = "⏳ 正在通过公共免密通道翻译...";
        TranslationTextBox.Text = "正在使用公共免密通道翻译，请稍候...";
        FallbackActionsBorder.Visibility = Visibility.Collapsed;

        try
        {
            var publicProvider = _translationService.AvailableProviders.FirstOrDefault(p => p.ProviderId == "PublicMyMemory");
            if (publicProvider != null)
            {
                var res = await publicProvider.TranslateAsync(text, src, tgt);
                if (res.Success)
                {
                    TranslationTextBox.Text = $"【公共免密通道译文】\n{res.TranslatedText}";
                    StatusTextBlock.Text = "✅ 公共免密翻译完成";
                }
                else
                {
                    TranslationTextBox.Text = $"【公共翻译失败】\n{res.ErrorMessage}";
                    StatusTextBlock.Text = "❌ 翻译失败";
                    FallbackActionsBorder.Visibility = Visibility.Visible;
                }
            }
        }
        catch (Exception ex)
        {
            TranslationTextBox.Text = $"公共翻译异常: {ex.Message}";
            FallbackActionsBorder.Visibility = Visibility.Visible;
        }
    }

    private void OnOpenSettingsFromErrorClick(object sender, RoutedEventArgs e)
    {
        _onOpenSettingsWindow?.Invoke();
    }

    private async void OnTranslateClick(object sender, RoutedEventArgs e)
    {
        await ExecuteTranslationAsync();
    }

    private void OnLangSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        if (!string.IsNullOrWhiteSpace(OriginalTextBox.Text))
        {
            _ = ExecuteTranslationAsync();
        }
    }

    private void OnOriginalSelectionChanged(object sender, RoutedEventArgs e)
    {
        string selected = OriginalTextBox.SelectedText.Trim();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SelectedPreviewTextBlock.Text = selected;
            SelectionActionBarBorder.Visibility = Visibility.Visible;
        }
        else
        {
            SelectionActionBarBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void OnOriginalTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateWordChips();
    }

    private void UpdateWordChips()
    {
        var text = OriginalTextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            WordChipsBorder.Visibility = Visibility.Collapsed;
            WordChipsPanel.Children.Clear();
            return;
        }

        // 提取句中的英文单词
        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"\b[a-zA-Z]{2,}(?:'[a-zA-Z]+)?\b");
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string w = match.Value.Trim();
            if (w.Length >= 2)
            {
                words.Add(w);
            }
        }

        if (words.Count == 0)
        {
            WordChipsBorder.Visibility = Visibility.Collapsed;
            WordChipsPanel.Children.Clear();
            return;
        }

        WordChipsPanel.Children.Clear();
        foreach (var word in words.Take(12)) // 最多展示前 12 个重点词
        {
            var chipBtn = new Button
            {
                Content = word,
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x27, 0x3C)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFC)),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 6, 6),
                FontSize = 11.5,
                ToolTip = "点击选择收录进【生词本】或【记忆本】"
            };

            string currentWord = word;
            chipBtn.Click += (s, e) =>
            {
                var cm = new ContextMenu();
                var miVocab = new MenuItem
                {
                    Header = $"📗 将 [{currentWord}] 存入生词本 (待打字练习)",
                    FontSize = 12
                };
                miVocab.Click += async (_, _) => await SaveWordAsync(currentWord, "Vocabulary");

                var miMemory = new MenuItem
                {
                    Header = $"🧠 将 [{currentWord}] 存入记忆本 (加入艾宾浩斯复习)",
                    FontSize = 12
                };
                miMemory.Click += async (_, _) => await SaveWordAsync(currentWord, "Memory");

                cm.Items.Add(miVocab);
                cm.Items.Add(miMemory);
                cm.PlacementTarget = chipBtn;
                cm.IsOpen = true;
            };

            WordChipsPanel.Children.Add(chipBtn);
        }

        WordChipsBorder.Visibility = Visibility.Visible;
    }

    #region 生词本与记忆本收录

    private async void OnAddSelectionToVocabClick(object sender, RoutedEventArgs e)
    {
        string selected = OriginalTextBox.SelectedText.Trim();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            await SaveWordAsync(selected, "Vocabulary");
        }
    }

    private async void OnAddSelectionToMemoryClick(object sender, RoutedEventArgs e)
    {
        string selected = OriginalTextBox.SelectedText.Trim();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            await SaveWordAsync(selected, "Memory");
        }
    }

    private async void OnAddToVocabBookClick(object sender, RoutedEventArgs e)
    {
        string target = GetTargetWordForSaving();
        if (!string.IsNullOrWhiteSpace(target))
        {
            await SaveWordAsync(target, "Vocabulary");
        }
    }

    private async void OnAddToMemoryBookClick(object sender, RoutedEventArgs e)
    {
        string target = GetTargetWordForSaving();
        if (!string.IsNullOrWhiteSpace(target))
        {
            await SaveWordAsync(target, "Memory");
        }
    }

    private string GetTargetWordForSaving()
    {
        string selected = OriginalTextBox.SelectedText.Trim();
        if (!string.IsNullOrEmpty(selected)) return selected;

        // 如果没有划选，尝试取第一个单词或整句
        var text = OriginalTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            FlashCopiedHint("原文为空，请先输入或划选单词！");
            return string.Empty;
        }

        var match = System.Text.RegularExpressions.Regex.Match(text, @"\b[a-zA-Z]{2,}\b");
        if (match.Success)
        {
            return match.Value;
        }

        return text.Length > 50 ? text.Substring(0, 50) : text;
    }

    private async Task SaveWordAsync(string targetWord, string bookType)
    {
        targetWord = targetWord.Trim();
        if (string.IsNullOrWhiteSpace(targetWord)) return;

        string fullSentence = OriginalTextBox.Text.Trim();
        string translation = TranslationTextBox.Text.Trim();

        var item = new VocabularyItem
        {
            Word = targetWord,
            BookType = bookType,
            ContextSentence = fullSentence,
            ContextTranslation = translation,
            Meaning = !string.IsNullOrWhiteSpace(translation) ? translation : "截图收录词汇",
            Tags = "截图收录",
            CreatedAt = DateTime.UtcNow,
            NextReviewDate = DateTime.UtcNow
        };

        await _vocabularyService.AddItemAsync(item);
        string bookName = bookType == "Memory" ? "【记忆本】" : "【生词本】";
        string icon = bookType == "Memory" ? "🧠" : "📗";
        FlashCopiedHint($"{icon} 已将 [{targetWord}] 收录至 {bookName}！");
    }

    private async void OnExtractKeywordsClick(object sender, RoutedEventArgs e)
    {
        var text = OriginalTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text))
        {
            FlashCopiedHint("原文为空，无法提炼词汇！");
            return;
        }

        StatusTextBlock.Text = "🔍 正在让 AI 提炼句中重点生词与高频搭配...";
        try
        {
            var prompt = _settingsService.CurrentSettings.PromptWordExtraction;
            var result = await _translationService.TranslateAsync(text, "en", "zh-Hans", prompt);
            if (result.Success && !string.IsNullOrWhiteSpace(result.TranslatedText))
            {
                TranslationTextBox.Text = $"【✨ AI 智能提取核心词汇与地道搭配】\n\n{result.TranslatedText}\n\n---\n【完整译文参考】\n{_workflow.TranslatedText}";
                StatusTextBlock.Text = "✅ 重点生词提炼完成！点击胶囊或划选可一键存入生词本/记忆本。";
                UpdateWordChips();
            }
            else
            {
                StatusTextBlock.Text = "生词提取失败，请重试。";
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"生词提取异常: {ex.Message}";
        }
    }

    private void OnOpenVocabularyClick(object sender, RoutedEventArgs e)
    {
        _onOpenVocabularyWindow?.Invoke();
    }

    #endregion

    private void OnCopyOriginalClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(OriginalTextBox.Text))
        {
            Clipboard.SetText(OriginalTextBox.Text);
            FlashCopiedHint("已复制原文！");
        }
    }

    private void OnCopyTranslationClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TranslationTextBox.Text))
        {
            Clipboard.SetText(TranslationTextBox.Text);
            FlashCopiedHint("已复制译文！");
        }
    }

    private async void FlashCopiedHint(string msg)
    {
        CopiedHintBlock.Text = msg;
        CopiedHintBlock.Visibility = Visibility.Visible;
        await Task.Delay(1800);
        CopiedHintBlock.Visibility = Visibility.Collapsed;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    public void PositionNearRegion(double dipX, double dipY, double dipW, double dipH)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;

        double targetX = dipX;
        double targetY = dipY + dipH + 12;

        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenRight = screenLeft + SystemParameters.VirtualScreenWidth;
        var screenBottom = screenTop + SystemParameters.VirtualScreenHeight;

        if (targetY + Height > screenBottom - 20)
        {
            targetY = dipY - Height - 12;
        }

        if (targetY < screenTop + 20)
        {
            targetY = screenTop + 20;
        }

        if (targetX + Width > screenRight - 20)
        {
            targetX = screenRight - Width - 20;
        }

        if (targetX < screenLeft + 20)
        {
            targetX = screenLeft + 20;
        }

        Left = targetX;
        Top = targetY;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            _ = ExecuteTranslationAsync();
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox && e.OriginalSource is not Button && e.OriginalSource is not ComboBox)
        {
            DragMove();
        }
    }
}
