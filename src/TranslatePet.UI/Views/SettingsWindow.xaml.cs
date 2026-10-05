using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TranslatePet.Core.Interfaces;

namespace TranslatePet.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IOcrService _ocrService;
    private readonly ITranslationService _translationService;
    private readonly Action _onResetPetPosition;

    public event EventHandler? SettingsSaved;

    private record ComboItem(string Display, string Value);

    public SettingsWindow(
        ISettingsService settingsService,
        IOcrService ocrService,
        ITranslationService translationService,
        Action onResetPetPosition)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _ocrService = ocrService;
        _translationService = translationService;
        _onResetPetPosition = onResetPetPosition;

        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var settings = _settingsService.CurrentSettings;

        // OCR
        var ocrItems = _ocrService.AvailableProviders.Select(p => new ComboItem(p.DisplayName, p.ProviderId)).ToList();
        OcrProviderComboBox.ItemsSource = ocrItems;
        OcrProviderComboBox.DisplayMemberPath = "Display";
        OcrProviderComboBox.SelectedValuePath = "Value";
        OcrProviderComboBox.SelectedValue = settings.OcrProvider;

        BaiduApiKeyTextBox.Text = settings.BaiduOcrApiKey;
        BaiduSecretKeyPasswordBox.Password = settings.BaiduOcrSecretKey;

        // Translation
        var transItems = _translationService.AvailableProviders.Select(p => new ComboItem(p.DisplayName, p.ProviderId)).ToList();
        TransProviderComboBox.ItemsSource = transItems;
        TransProviderComboBox.DisplayMemberPath = "Display";
        TransProviderComboBox.SelectedValuePath = "Value";
        TransProviderComboBox.SelectedValue = settings.TranslationProvider;

        OpenAiBaseUrlTextBox.Text = settings.OpenAiBaseUrl;
        OpenAiApiKeyPasswordBox.Password = settings.OpenAiApiKey;
        OpenAiModelTextBox.Text = settings.OpenAiModel;

        // Hotkey
        AltCheckBox.IsChecked = settings.HotkeyAlt;
        CtrlCheckBox.IsChecked = settings.HotkeyCtrl;
        ShiftCheckBox.IsChecked = settings.HotkeyShift;
        WinCheckBox.IsChecked = settings.HotkeyWin;

        var keys = new[] { "Q", "T", "E", "R", "W", "A", "S", "D", "F", "Z", "X", "C", "F1", "F2", "F3", "F4" };
        HotkeyCharComboBox.ItemsSource = keys;
        HotkeyCharComboBox.SelectedItem = keys.Contains(settings.HotkeyKey.ToUpper()) ? settings.HotkeyKey.ToUpper() : "Q";

        // Pet
        TopMostCheckBox.IsChecked = settings.PetTopMost;

        UpdatePanelsVisibility();
    }

    private void UpdatePanelsVisibility()
    {
        var selectedOcr = OcrProviderComboBox.SelectedValue as string;
        BaiduOcrSettingsPanel.Visibility = selectedOcr == "BaiduCloud" ? Visibility.Visible : Visibility.Collapsed;

        var selectedTrans = TransProviderComboBox.SelectedValue as string;
        OpenAiSettingsPanel.Visibility = selectedTrans == "OpenAiCompatible" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOcrProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePanelsVisibility();
    }

    private void OnTransProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePanelsVisibility();
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var settings = _settingsService.CurrentSettings;

        settings.OcrProvider = (OcrProviderComboBox.SelectedValue as string) ?? "BaiduCloud";
        settings.BaiduOcrApiKey = BaiduApiKeyTextBox.Text.Trim();
        settings.BaiduOcrSecretKey = BaiduSecretKeyPasswordBox.Password.Trim();

        settings.TranslationProvider = (TransProviderComboBox.SelectedValue as string) ?? "OpenAiCompatible";
        settings.OpenAiBaseUrl = OpenAiBaseUrlTextBox.Text.Trim();
        settings.OpenAiApiKey = OpenAiApiKeyPasswordBox.Password.Trim();
        settings.OpenAiModel = OpenAiModelTextBox.Text.Trim();

        settings.HotkeyAlt = AltCheckBox.IsChecked == true;
        settings.HotkeyCtrl = CtrlCheckBox.IsChecked == true;
        settings.HotkeyShift = ShiftCheckBox.IsChecked == true;
        settings.HotkeyWin = WinCheckBox.IsChecked == true;
        settings.HotkeyKey = (HotkeyCharComboBox.SelectedItem as string) ?? "Q";

        settings.PetTopMost = TopMostCheckBox.IsChecked == true;

        await _settingsService.SaveSettingsAsync();
        SettingsSaved?.Invoke(this, EventArgs.Empty);

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
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox && e.OriginalSource is not PasswordBox && e.OriginalSource is not Button)
        {
            DragMove();
        }
    }
}
