using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services.Ocr;
using TranslatePet.Core.Services.Translation;
using TranslatePet.UI.Services;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly IHotkeyService _hotkeyService;
    private readonly Action _onResetPetPosition;

    private bool _isRecordingHotkey;

    public event EventHandler? SettingsSaved;

    private record ComboItem(string Display, string Value);
    private record AiPresetItem(string Name, string BaseUrl, string Model, string Hint, bool EnableThinking);

    private readonly List<AiPresetItem> _aiPresets = new()
    {
        new("(选择常用大模型预设快速填入...)", "", "", "", false),
        new("DeepSeek-V3 (推荐极速无思考 / 毫秒直出)", "https://api.deepseek.com/v1", "deepseek-chat", "官方极速模型，毫无思考等待，翻译精准地道", false),
        new("DeepSeek-R1 (深度思维链版 / 思考较慢)", "https://api.deepseek.com/v1", "deepseek-reasoner", "官方满血R1深度思考，耗时约15-30秒", true),
        new("OpenAI 官方 (GPT-4o-mini 极速)", "https://api.openai.com/v1", "gpt-4o-mini", "OpenAI 官方快速端点，需国际网络环境", false),
        new("硅基流动 SiliconFlow (Qwen2.5-7B 极速)", "https://api.siliconflow.cn/v1", "Qwen/Qwen2.5-7B-Instruct", "国内高性价比平台，极速直出", false),
        new("硅基流动 SiliconFlow (DeepSeek-R1 思考版)", "https://api.siliconflow.cn/v1", "deepseek-ai/DeepSeek-R1", "国内满血R1长思维链", true),
        new("月之暗面 Moonshot (Kimi 极速)", "https://api.moonshot.cn/v1", "moonshot-v1-8k", "Moonshot 官方开放平台，国内直连", false),
        new("Ollama 本地大模型 (Qwen2.5 极速)", "http://localhost:11434/v1", "qwen2.5:latest", "本地运行大模型，无需填写 API Key", false)
    };

    private static readonly string[] AllSupportedKeys = new[]
    {
        "F4", "F1", "F2", "F3", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P",
        "A", "S", "D", "F", "G", "H", "J", "K", "L",
        "Z", "X", "C", "V", "B", "N", "M",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        "Space", "~"
    };

    public SettingsWindow(
        ISettingsService settingsService,
        IOcrService ocrService,
        ITranslationService translationService,
        IHotkeyService hotkeyService,
        Action onResetPetPosition)
    {
        InitializeComponent();
        WindowBlurHelper.EnableBlur(this);
        _settingsService = settingsService;
        _ocrService = ocrService;
        _translationService = translationService;
        _hotkeyService = hotkeyService;
        _onResetPetPosition = onResetPetPosition;

        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.CurrentSettings;

        // 1. OCR 提供方
        var ocrItems = _ocrService.AvailableProviders.Select(p => new ComboItem(p.DisplayName, p.ProviderId)).ToList();
        OcrProviderComboBox.ItemsSource = ocrItems;
        OcrProviderComboBox.DisplayMemberPath = "Display";
        OcrProviderComboBox.SelectedValuePath = "Value";
        OcrProviderComboBox.SelectedValue = settings.OcrProvider;

        if (WindowsNativeOcrProvider.IsSupported)
        {
            var langs = WindowsNativeOcrProvider.GetSupportedLanguages();
            WindowsOcrStatusTextBlock.Text = $"✅ Windows 本地 OCR 引擎已就绪。\n支持语言: {string.Join(", ", langs)}";
            WindowsOcrStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
        }
        else
        {
            WindowsOcrStatusTextBlock.Text = "⚠️ 系统当前未检测到 Windows OCR 语言包，建议在系统「设置 - 语言」中安装语言包，或使用百度云 OCR。";
            WindowsOcrStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
        }

        BaiduApiKeyTextBox.Text = settings.BaiduOcrApiKey;
        BaiduSecretKeyPasswordBox.Password = settings.BaiduOcrSecretKey;

        // 2. 翻译引擎
        var transItems = _translationService.AvailableProviders.Select(p => new ComboItem(p.DisplayName, p.ProviderId)).ToList();
        TransProviderComboBox.ItemsSource = transItems;
        TransProviderComboBox.DisplayMemberPath = "Display";
        TransProviderComboBox.SelectedValuePath = "Value";
        TransProviderComboBox.SelectedValue = settings.TranslationProvider;

        AiPresetComboBox.ItemsSource = _aiPresets;
        AiPresetComboBox.DisplayMemberPath = "Name";
        AiPresetComboBox.SelectedIndex = 0;

        OpenAiBaseUrlTextBox.Text = settings.OpenAiBaseUrl;
        OpenAiApiKeyPasswordBox.Password = settings.OpenAiApiKey;
        OpenAiModelTextBox.Text = settings.OpenAiModel;
        DeepThinkingCheckBox.IsChecked = settings.EnableDeepThinking;
        UpdateDeepThinkingUI(settings.EnableDeepThinking);

        // 3. 专属提示词
        PromptConversationalTextBox.Text = settings.PromptConversational;
        PromptStandardTextBox.Text = settings.PromptStandard;
        PromptFormalTextBox.Text = settings.PromptFormal;
        PromptWordExtractionTextBox.Text = settings.PromptWordExtraction;
        PromptWordExplanationTextBox.Text = settings.PromptWordExplanation;

        // 4. 快捷键
        HotkeyCharComboBox.ItemsSource = AllSupportedKeys;

        string currentKey = settings.HotkeyKey?.Trim().ToUpperInvariant() ?? "F4";
        if (AllSupportedKeys.Contains(currentKey))
        {
            HotkeyCharComboBox.SelectedItem = currentKey;
        }
        else
        {
            HotkeyCharComboBox.SelectedItem = "F4";
        }

        AltCheckBox.IsChecked = settings.HotkeyAlt;
        CtrlCheckBox.IsChecked = settings.HotkeyCtrl;
        ShiftCheckBox.IsChecked = settings.HotkeyShift;
        WinCheckBox.IsChecked = settings.HotkeyWin;

        // 5. 每日背单词提醒
        DailyReminderCheckBox.IsChecked = settings.EnableDailyWordReminder;
        DailyReminderTimeTextBox.Text = string.IsNullOrWhiteSpace(settings.DailyReminderTime) ? "20:00" : settings.DailyReminderTime;
        DailyReviewGoalTextBox.Text = settings.DailyReviewGoal.ToString();

        // 6. 桌宠配置
        TopMostCheckBox.IsChecked = settings.PetTopMost;
        EdgeSnapCheckBox.IsChecked = settings.EnableEdgeSnap;

        UpdatePanelsVisibility();
        UpdateHotkeyDisplayAndValidation();
    }

    private void UpdatePanelsVisibility()
    {
        var selectedOcr = OcrProviderComboBox.SelectedValue as string;
        WindowsOcrSettingsPanel.Visibility = selectedOcr == "WindowsNative" ? Visibility.Visible : Visibility.Collapsed;
        BaiduOcrSettingsPanel.Visibility = selectedOcr == "BaiduCloud" ? Visibility.Visible : Visibility.Collapsed;

        var selectedTrans = TransProviderComboBox.SelectedValue as string;
        OpenAiSettingsPanel.Visibility = selectedTrans == "OpenAiCompatible" ? Visibility.Visible : Visibility.Collapsed;
        PublicFallbackSettingsPanel.Visibility = selectedTrans == "PublicMyMemory" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOcrProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePanelsVisibility();
        OcrTestResultBorder.Visibility = Visibility.Collapsed;
    }

    private void OnTransProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePanelsVisibility();
        TransTestResultBorder.Visibility = Visibility.Collapsed;
    }

    private void OnDeepThinkingChanged(object sender, RoutedEventArgs e)
    {
        bool isEnabled = DeepThinkingCheckBox.IsChecked == true;
        UpdateDeepThinkingUI(isEnabled);

        // 如果用户主动取消深度思考，并且当前输入框中是推理模型，自动优化为对应的极速直出模型
        if (!isEnabled)
        {
            var curModel = OpenAiModelTextBox.Text.Trim();
            if (curModel.Equals("deepseek-reasoner", StringComparison.OrdinalIgnoreCase))
            {
                OpenAiModelTextBox.Text = "deepseek-chat";
            }
            else if (curModel.Equals("deepseek-ai/DeepSeek-R1", StringComparison.OrdinalIgnoreCase))
            {
                OpenAiModelTextBox.Text = "deepseek-ai/DeepSeek-V3";
            }
        }
    }

    private void UpdateDeepThinkingUI(bool isEnabled)
    {
        if (isEnabled)
        {
            DeepThinkingHintTextBlock.Text = "⚠️ 深度思考已开启: 模型将在翻译前进行长思维链推演，通常耗时 15~30 秒。";
            DeepThinkingHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));
        }
        else
        {
            DeepThinkingHintTextBlock.Text = "💡 当前已关闭 (极速秒级直出): 跳过推理链直接出译文 (约 1~2 秒)，日常翻译推荐关闭以防卡慢。";
            DeepThinkingHintTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
        }
    }

    private void OnAiPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AiPresetComboBox.SelectedItem is AiPresetItem preset && !string.IsNullOrEmpty(preset.BaseUrl))
        {
            OpenAiBaseUrlTextBox.Text = preset.BaseUrl;
            OpenAiModelTextBox.Text = preset.Model;
            DeepThinkingCheckBox.IsChecked = preset.EnableThinking;
            UpdateDeepThinkingUI(preset.EnableThinking);

            if (preset.BaseUrl.Contains("localhost") || preset.BaseUrl.Contains("127.0.0.1"))
            {
                OpenAiApiKeyPasswordBox.Password = string.Empty;
            }
        }
    }

    #region 模型与接口连通性测试

    private void OnTestWindowsOcrClick(object sender, RoutedEventArgs e)
    {
        if (WindowsNativeOcrProvider.IsSupported)
        {
            var langs = WindowsNativeOcrProvider.GetSupportedLanguages();
            ShowOcrTestResult(true, $"✅ Windows 本地原生 OCR 就绪！\n系统共检测到 {langs.Count} 种可用语言组件:\n" + string.Join(Environment.NewLine, langs));
        }
        else
        {
            ShowOcrTestResult(false, "❌ 当前 Windows 系统未检测到 OCR 语言包组件。建议在 Windows「设置 - 时间和语言 - 语言」中安装语言组件，或切换使用百度云 OCR。");
        }
    }

    private async void OnTestBaiduOcrClick(object sender, RoutedEventArgs e)
    {
        var apiKey = BaiduApiKeyTextBox.Text.Trim();
        var secretKey = BaiduSecretKeyPasswordBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            ShowOcrTestResult(false, "❌ 请先在上方填入完整的百度 API Key 与 Secret Key。");
            return;
        }

        BtnTestBaiduOcr.IsEnabled = false;
        BtnTestBaiduOcr.Content = "⏳ 正在测试百度鉴权...";
        ShowOcrTestResult(true, "⏳ 正在与百度智能云服务器通信校验凭据...", isWarning: true);

        try
        {
            var provider = _ocrService.AvailableProviders.OfType<BaiduCloudOcrProvider>().FirstOrDefault()
                        ?? new BaiduCloudOcrProvider(_settingsService);

            var (ok, msg) = await provider.TestConnectionAsync(apiKey, secretKey);
            ShowOcrTestResult(ok, msg);
        }
        catch (Exception ex)
        {
            ShowOcrTestResult(false, $"❌ 百度 OCR 测试异常: {ex.Message}");
        }
        finally
        {
            BtnTestBaiduOcr.IsEnabled = true;
            BtnTestBaiduOcr.Content = "🔌 测试百度 OCR 凭据连通性";
        }
    }

    private void ShowOcrTestResult(bool success, string message, bool isWarning = false)
    {
        OcrTestResultBorder.Visibility = Visibility.Visible;
        if (isWarning)
        {
            OcrTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xE6, 0x51, 0x00));
            OcrTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            OcrTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80));
        }
        else if (success)
        {
            OcrTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0x1B, 0x5E, 0x20));
            OcrTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            OcrTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
        }
        else
        {
            OcrTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xBF, 0x36, 0x0C));
            OcrTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22));
            OcrTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAB, 0x91));
        }
        OcrTestResultTextBlock.Text = message;
    }

    private async void OnTestAiTranslationClick(object sender, RoutedEventArgs e)
    {
        var baseUrl = OpenAiBaseUrlTextBox.Text.Trim();
        var apiKey = OpenAiApiKeyPasswordBox.Password.Trim();
        var model = OpenAiModelTextBox.Text.Trim();

        BtnTestAiTranslation.IsEnabled = false;
        BtnTestAiTranslation.Content = "⏳ 正在测试大模型连通性...";
        ShowTransTestResult(true, "⏳ 正在向大模型端点发送探测请求，请稍候...", isWarning: true);

        try
        {
            var provider = _translationService.AvailableProviders.OfType<OpenAiCompatibleTranslationProvider>().FirstOrDefault()
                        ?? new OpenAiCompatibleTranslationProvider(_settingsService);

            var (ok, msg) = await provider.TestConnectionAsync(baseUrl, apiKey, model);
            ShowTransTestResult(ok, msg);
        }
        catch (Exception ex)
        {
            ShowTransTestResult(false, $"❌ 测试异常: {ex.Message}");
        }
        finally
        {
            BtnTestAiTranslation.IsEnabled = true;
            BtnTestAiTranslation.Content = "🔌 测试大模型翻译连通性";
        }
    }

    private async void OnTestPublicTranslationClick(object sender, RoutedEventArgs e)
    {
        BtnTestPublicTranslation.IsEnabled = false;
        BtnTestPublicTranslation.Content = "⏳ 正在测试公共通道...";
        ShowTransTestResult(true, "⏳ 正在探测公共翻译服务连通性...", isWarning: true);

        try
        {
            var provider = _translationService.AvailableProviders.OfType<PublicFallbackTranslationProvider>().FirstOrDefault()
                        ?? new PublicFallbackTranslationProvider();

            var (ok, msg) = await provider.TestConnectionAsync();
            ShowTransTestResult(ok, msg);
        }
        catch (Exception ex)
        {
            ShowTransTestResult(false, $"❌ 公共通道测试异常: {ex.Message}");
        }
        finally
        {
            BtnTestPublicTranslation.IsEnabled = true;
            BtnTestPublicTranslation.Content = "🔌 测试公共翻译连通性";
        }
    }

    private void ShowTransTestResult(bool success, string message, bool isWarning = false)
    {
        TransTestResultBorder.Visibility = Visibility.Visible;
        if (isWarning)
        {
            TransTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xE6, 0x51, 0x00));
            TransTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            TransTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x80));
        }
        else if (success)
        {
            TransTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0x1B, 0x5E, 0x20));
            TransTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            TransTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
        }
        else
        {
            TransTestResultBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xBF, 0x36, 0x0C));
            TransTestResultBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22));
            TransTestResultTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAB, 0x91));
        }
        TransTestResultTextBlock.Text = message;
    }

    #endregion

    #region 提示词重置按钮

    private void OnResetConversationalPromptClick(object sender, RoutedEventArgs e)
    {
        PromptConversationalTextBox.Text = new AppSettings().PromptConversational;
    }

    private void OnResetStandardPromptClick(object sender, RoutedEventArgs e)
    {
        PromptStandardTextBox.Text = new AppSettings().PromptStandard;
    }

    private void OnResetFormalPromptClick(object sender, RoutedEventArgs e)
    {
        PromptFormalTextBox.Text = new AppSettings().PromptFormal;
    }

    private void OnResetWordExtractionPromptClick(object sender, RoutedEventArgs e)
    {
        PromptWordExtractionTextBox.Text = new AppSettings().PromptWordExtraction;
    }

    private void OnResetWordExplanationPromptClick(object sender, RoutedEventArgs e)
    {
        PromptWordExplanationTextBox.Text = new AppSettings().PromptWordExplanation;
    }

    #endregion

    #region 快捷键交互与录制

    private void OnHotkeyRecorderGotFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = true;
        HotkeyRecorderBox.Text = "⌨️ 请在键盘上直接按下新快捷键...";
        HotkeyRecorderBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
    }

    private void OnHotkeyRecorderLostFocus(object sender, RoutedEventArgs e)
    {
        _isRecordingHotkey = false;
        HotkeyRecorderBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x42, 0x59));
        UpdateHotkeyDisplayAndValidation();
    }

    private void OnHotkeyRecorderPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // 单独按下修饰键时不作为主键，等待组合
        if (key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool win = (Keyboard.Modifiers & ModifierKeys.Windows) != 0;

        string keyStr = KeyToFriendlyString(key);

        AltCheckBox.IsChecked = alt;
        CtrlCheckBox.IsChecked = ctrl;
        ShiftCheckBox.IsChecked = shift;
        WinCheckBox.IsChecked = win;

        if (AllSupportedKeys.Contains(keyStr))
        {
            HotkeyCharComboBox.SelectedItem = keyStr;
        }

        UpdateHotkeyDisplayAndValidation();

        // 移开焦点，完成录制
        FocusManager.SetFocusedElement(this, this);
    }

    private void OnQuickHotkeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            ApplyHotkeyPreset(tag);
        }
    }

    private void ApplyHotkeyPreset(string preset)
    {
        switch (preset.ToUpperInvariant())
        {
            case "F4":
                AltCheckBox.IsChecked = false;
                CtrlCheckBox.IsChecked = false;
                ShiftCheckBox.IsChecked = false;
                WinCheckBox.IsChecked = false;
                HotkeyCharComboBox.SelectedItem = "F4";
                break;
            case "ALT+Q":
                AltCheckBox.IsChecked = true;
                CtrlCheckBox.IsChecked = false;
                ShiftCheckBox.IsChecked = false;
                WinCheckBox.IsChecked = false;
                HotkeyCharComboBox.SelectedItem = "Q";
                break;
            case "F1":
                AltCheckBox.IsChecked = false;
                CtrlCheckBox.IsChecked = false;
                ShiftCheckBox.IsChecked = false;
                WinCheckBox.IsChecked = false;
                HotkeyCharComboBox.SelectedItem = "F1";
                break;
            case "ALT+W":
                AltCheckBox.IsChecked = true;
                CtrlCheckBox.IsChecked = false;
                ShiftCheckBox.IsChecked = false;
                WinCheckBox.IsChecked = false;
                HotkeyCharComboBox.SelectedItem = "W";
                break;
            case "ALT+C":
                AltCheckBox.IsChecked = true;
                CtrlCheckBox.IsChecked = false;
                ShiftCheckBox.IsChecked = false;
                WinCheckBox.IsChecked = false;
                HotkeyCharComboBox.SelectedItem = "C";
                break;
        }

        UpdateHotkeyDisplayAndValidation();
    }

    private void OnResetDefaultHotkeyClick(object sender, RoutedEventArgs e)
    {
        ApplyHotkeyPreset("F4");
    }

    private void OnHotkeyManualChanged(object sender, RoutedEventArgs e)
    {
        UpdateHotkeyDisplayAndValidation();
    }

    private void OnHotkeyCharSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateHotkeyDisplayAndValidation();
    }

    private void UpdateHotkeyDisplayAndValidation()
    {
        if (_isRecordingHotkey) return;

        bool alt = AltCheckBox.IsChecked == true;
        bool ctrl = CtrlCheckBox.IsChecked == true;
        bool shift = ShiftCheckBox.IsChecked == true;
        bool win = WinCheckBox.IsChecked == true;
        string keyStr = (HotkeyCharComboBox.SelectedItem as string) ?? "F4";

        string formatted = GlobalHotkeyHelper.FormatKey(alt, ctrl, shift, win, keyStr);
        HotkeyRecorderBox.Text = formatted;

        // 实时探测校验
        var (ok, msg) = _hotkeyService.ValidateHotkey(alt, ctrl, shift, win, keyStr);
        if (ok)
        {
            HotkeyStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x1B, 0x5E, 0x20));
            HotkeyStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
            HotkeyStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));
            HotkeyStatusTextBlock.Text = msg;
        }
        else
        {
            HotkeyStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x35, 0xBF, 0x36, 0x0C));
            HotkeyStatusBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x57, 0x22));
            HotkeyStatusTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAB, 0x91));
            HotkeyStatusTextBlock.Text = msg;
        }
    }

    private static string KeyToFriendlyString(Key key)
    {
        if (key >= Key.F1 && key <= Key.F24)
        {
            return key.ToString();
        }

        if (key >= Key.D0 && key <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString();
        }

        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((int)key - (int)Key.NumPad0).ToString();
        }

        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString();
        }

        return key switch
        {
            Key.Space => "Space",
            Key.Oem3 or Key.OemTilde => "~",
            Key.Tab => "Tab",
            _ => key.ToString()
        };
    }

    #endregion

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        bool alt = AltCheckBox.IsChecked == true;
        bool ctrl = CtrlCheckBox.IsChecked == true;
        bool shift = ShiftCheckBox.IsChecked == true;
        bool win = WinCheckBox.IsChecked == true;
        string keyStr = (HotkeyCharComboBox.SelectedItem as string) ?? "F4";

        // 保存前强制校验快捷键是否真的可用
        var validation = _hotkeyService.ValidateHotkey(alt, ctrl, shift, win, keyStr);
        if (!validation.Success)
        {
            MessageBox.Show(
                $"快捷键配置未通过！\n\n{validation.Message}\n\n请在界面中调整组合，或直接点击【F4 (单键)】/【Alt+Q】快速选用。",
                "快捷键冲突或无效",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var settings = _settingsService.CurrentSettings;

        settings.OcrProvider = (OcrProviderComboBox.SelectedValue as string) ?? "WindowsNative";
        settings.BaiduOcrApiKey = BaiduApiKeyTextBox.Text.Trim();
        settings.BaiduOcrSecretKey = BaiduSecretKeyPasswordBox.Password.Trim();

        settings.TranslationProvider = (TransProviderComboBox.SelectedValue as string) ?? "OpenAiCompatible";
        settings.OpenAiBaseUrl = OpenAiBaseUrlTextBox.Text.Trim();
        settings.OpenAiApiKey = OpenAiApiKeyPasswordBox.Password.Trim();
        settings.OpenAiModel = OpenAiModelTextBox.Text.Trim();
        settings.EnableDeepThinking = DeepThinkingCheckBox.IsChecked == true;

        // 同步应用到当前服务实例
        _ocrService.SetCurrentProvider(settings.OcrProvider);
        _translationService.SetCurrentProvider(settings.TranslationProvider);

        // 保存 5 项专属提示词
        settings.PromptConversational = PromptConversationalTextBox.Text.Trim();
        settings.PromptStandard = PromptStandardTextBox.Text.Trim();
        settings.PromptFormal = PromptFormalTextBox.Text.Trim();
        settings.PromptWordExtraction = PromptWordExtractionTextBox.Text.Trim();
        settings.PromptWordExplanation = PromptWordExplanationTextBox.Text.Trim();

        // 快捷键
        settings.HotkeyAlt = alt;
        settings.HotkeyCtrl = ctrl;
        settings.HotkeyShift = shift;
        settings.HotkeyWin = win;
        settings.HotkeyKey = keyStr;

        // 每日背单词提醒
        settings.EnableDailyWordReminder = DailyReminderCheckBox.IsChecked == true;
        settings.DailyReminderTime = DailyReminderTimeTextBox.Text.Trim();
        if (int.TryParse(DailyReviewGoalTextBox.Text.Trim(), out int goal) && goal > 0)
        {
            settings.DailyReviewGoal = goal;
        }

        settings.PetTopMost = TopMostCheckBox.IsChecked == true;
        settings.EnableEdgeSnap = EdgeSnapCheckBox.IsChecked == true;

        await _settingsService.SaveSettingsAsync();
        SettingsSaved?.Invoke(this, EventArgs.Empty);

        MessageBox.Show(
            $"偏好设置保存成功！\n\n• OCR 识别引擎与翻译模型配置已同步生效。\n• 专属提示词已同步生效。\n• 截图翻译全局快捷键 [{GlobalHotkeyHelper.FormatKey(alt, ctrl, shift, win, keyStr)}] 已生效。",
            "保存成功",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Close();
    }

    private void OnResetPetPositionClick(object sender, RoutedEventArgs e)
    {
        _onResetPetPosition?.Invoke();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox && e.OriginalSource is not PasswordBox && e.OriginalSource is not Button && e.OriginalSource is not ComboBox)
        {
            DragMove();
        }
    }
}
