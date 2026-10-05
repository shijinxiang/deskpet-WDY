using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Services;

namespace TranslatePet.UI.Views;

public partial class ResultWindow : Window
{
    private readonly TranslationWorkflow _workflow;
    private readonly ISettingsService _settingsService;
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

    public ResultWindow(TranslationWorkflow workflow, ISettingsService settingsService)
    {
        InitializeComponent();
        _workflow = workflow;
        _settingsService = settingsService;

        InitializeLanguages();
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

    public async Task LoadAndTranslateAsync(string recognizedText)
    {
        OriginalTextBox.Text = recognizedText;
        if (!string.IsNullOrWhiteSpace(recognizedText))
        {
            await ExecuteTranslationAsync();
        }
        else
        {
            StatusTextBlock.Text = "选区内未识别到文字，请编辑原文或重新框选";
        }
    }

    private async Task ExecuteTranslationAsync()
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
        StatusTextBlock.Text = "⏳ 正在翻译中...";
        TranslationTextBox.Text = "翻译中，请稍候...";

        try
        {
            var result = await _workflow.TranslateTextAsync(text, src, tgt);
            if (result.Success)
            {
                TranslationTextBox.Text = result.TranslatedText;
                StatusTextBlock.Text = "✅ 翻译完成 (按 Esc 关闭)";
            }
            else
            {
                TranslationTextBox.Text = $"【翻译失败】\n{result.ErrorMessage}";
                StatusTextBlock.Text = "❌ 翻译失败，可点击重新翻译重试";
            }
        }
        catch (Exception ex)
        {
            TranslationTextBox.Text = $"异常: {ex.Message}";
            StatusTextBlock.Text = "❌ 发生异常";
        }
        finally
        {
            TranslateButton.IsEnabled = true;
        }
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
        await Task.Delay(1500);
        CopiedHintBlock.Visibility = Visibility.Collapsed;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox && e.OriginalSource is not Button)
        {
            DragMove();
        }
    }
}
