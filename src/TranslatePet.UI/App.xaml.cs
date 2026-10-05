using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services;
using TranslatePet.Core.Services.Ocr;
using TranslatePet.Core.Services.Translation;
using TranslatePet.UI.Services;
using TranslatePet.UI.Tray;
using TranslatePet.UI.Utils;
using TranslatePet.UI.Views;

namespace TranslatePet.UI;

public partial class App : Application
{
    private ISettingsService _settingsService = null!;
    private ICaptureService _captureService = null!;
    private IOcrService _ocrService = null!;
    private ITranslationService _translationService = null!;
    private TranslationWorkflow _workflow = null!;
    private GlobalHotkeyHelper _hotkeyService = null!;
    private TrayIconManager _trayManager = null!;

    private PetWindow _petWindow = null!;
    private ResultWindow? _currentResultWindow;
    private SettingsWindow? _currentSettingsWindow;

    private bool _isCapturingWorkflowRunning;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. 初始化设置服务
        _settingsService = new SettingsService();
        await _settingsService.LoadSettingsAsync();

        // 2. 初始化核心与服务
        _captureService = new WpfCaptureService();

        var ocrProviders = new IOcrProvider[]
        {
            new BaiduCloudOcrProvider(_settingsService),
            new MockOcrProvider()
        };
        _ocrService = new OcrService(_settingsService, ocrProviders);

        var translationProviders = new ITranslationProvider[]
        {
            new OpenAiCompatibleTranslationProvider(_settingsService),
            new PublicFallbackTranslationProvider()
        };
        _translationService = new TranslationService(_settingsService, translationProviders);

        _workflow = new TranslationWorkflow(_captureService, _ocrService, _translationService, _settingsService);

        // 3. 初始化桌宠与窗口句柄
        _petWindow = new PetWindow(_settingsService);
        _petWindow.RequestCapture += (s, ev) => StartCaptureWorkflow();
        _petWindow.RequestSettings += (s, ev) => ShowSettings();
        _petWindow.RequestExit += (s, ev) => ExitApplication();

        var helper = new WindowInteropHelper(_petWindow);
        var hWnd = helper.EnsureHandle();

        // 4. 初始化系统托盘
        _trayManager = new TrayIconManager(hWnd);
        _trayManager.RequestCapture += (s, ev) => StartCaptureWorkflow();
        _trayManager.RequestTogglePet += (s, ev) => TogglePet();
        _trayManager.RequestSettings += (s, ev) => ShowSettings();
        _trayManager.RequestExit += (s, ev) => ExitApplication();

        // 5. 注册全局快捷键与消息循环
        var hwndSource = HwndSource.FromHwnd(hWnd);
        hwndSource?.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            _trayManager.HandleWindowMessage(msg, wParam, lParam);
            return IntPtr.Zero;
        });

        _hotkeyService = new GlobalHotkeyHelper();
        _hotkeyService.Initialize(hWnd);
        _hotkeyService.HotkeyPressed += (s, ev) => StartCaptureWorkflow();
        RegisterConfiguredHotkey();

        // 6. 显示桌宠
        if (_settingsService.CurrentSettings.PetVisible)
        {
            _petWindow.Show();
        }
    }

    private void RegisterConfiguredHotkey()
    {
        var s = _settingsService.CurrentSettings;
        var success = _hotkeyService.Register(s.HotkeyAlt, s.HotkeyCtrl, s.HotkeyShift, s.HotkeyWin, s.HotkeyKey);
        if (!success)
        {
            _trayManager.ShowNotification("快捷键冲突提示", $"未能注册全局快捷键 [{FormatHotkey(s)}]，可能被其他软件占用，请在设置中更改。");
        }
    }

    private static string FormatHotkey(AppSettings s)
    {
        var list = new List<string>();
        if (s.HotkeyAlt) list.Add("Alt");
        if (s.HotkeyCtrl) list.Add("Ctrl");
        if (s.HotkeyShift) list.Add("Shift");
        if (s.HotkeyWin) list.Add("Win");
        list.Add(s.HotkeyKey);
        return string.Join("+", list);
    }

    public async void StartCaptureWorkflow()
    {
        if (_isCapturingWorkflowRunning) return;
        _isCapturingWorkflowRunning = true;

        try
        {
            _currentResultWindow?.Close();

            // 1. 隐藏桌宠，防止桌宠出现在截图中
            var petWasVisible = _petWindow.IsVisible;
            if (petWasVisible)
            {
                _petWindow.Hide();
                // 留出短暂时间等待 DWM 刷新合成
                await Task.Delay(60);
            }

            // 2. 截取全虚拟桌面 (内存中短暂处理，不落盘)
            var bounds = VirtualScreenHelper.GetVirtualScreenBounds();
            var fullScreenBytes = await _captureService.CaptureVirtualScreenAsync();

            // 3. 开启选区遮罩
            var overlay = new SnippingOverlayWindow(fullScreenBytes, bounds);
            var result = overlay.ShowDialog();

            // 4. 恢复桌宠显示状态
            if (petWasVisible && _settingsService.CurrentSettings.PetVisible)
            {
                _petWindow.Show();
            }

            if (result == true && overlay.SelectedPhysicalRegion != null && !overlay.SelectedPhysicalRegion.IsEmpty)
            {
                var region = overlay.SelectedPhysicalRegion;

                // 打开翻译结果展示窗，提供即时状态
                _currentResultWindow = new ResultWindow(_workflow, _settingsService);
                _currentResultWindow.PositionNearRegion(
                    overlay.SelectedDipRect.X,
                    overlay.SelectedDipRect.Y,
                    overlay.SelectedDipRect.Width,
                    overlay.SelectedDipRect.Height);
                _currentResultWindow.Show();

                // 异步处理选区 OCR
                var ocrResult = await _workflow.ProcessSnippedRegionAsync(fullScreenBytes, region);

                if (ocrResult.Success)
                {
                    await _currentResultWindow.LoadAndTranslateAsync(ocrResult.Text);
                }
                else
                {
                    await _currentResultWindow.LoadAndTranslateAsync(string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动截图翻译流程异常: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            if (_settingsService.CurrentSettings.PetVisible)
            {
                _petWindow.Show();
            }
        }
        finally
        {
            _isCapturingWorkflowRunning = false;
        }
    }

    private void TogglePet()
    {
        if (_petWindow.IsVisible)
        {
            _petWindow.Hide();
            _settingsService.CurrentSettings.PetVisible = false;
        }
        else
        {
            _petWindow.Show();
            _settingsService.CurrentSettings.PetVisible = true;
        }
        _ = _settingsService.SaveSettingsAsync();
    }

    private void ShowSettings()
    {
        if (_currentSettingsWindow != null && _currentSettingsWindow.IsLoaded)
        {
            _currentSettingsWindow.Activate();
            return;
        }

        _currentSettingsWindow = new SettingsWindow(
            _settingsService,
            _ocrService,
            _translationService,
            onResetPetPosition: ResetPetPosition
        );

        _currentSettingsWindow.SettingsSaved += (s, e) =>
        {
            RegisterConfiguredHotkey();
            _petWindow.Topmost = _settingsService.CurrentSettings.PetTopMost;
        };

        _currentSettingsWindow.Show();
    }

    private void ResetPetPosition()
    {
        _petWindow.Left = SystemParameters.WorkArea.Right - _petWindow.Width - 30;
        _petWindow.Top = SystemParameters.WorkArea.Bottom - _petWindow.Height - 50;
        _settingsService.CurrentSettings.PetPositionX = _petWindow.Left;
        _settingsService.CurrentSettings.PetPositionY = _petWindow.Top;
        _ = _settingsService.SaveSettingsAsync();
    }

    private void ExitApplication()
    {
        _hotkeyService.Dispose();
        _trayManager.Dispose();
        _petWindow.Close();
        _currentResultWindow?.Close();
        _currentSettingsWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayManager?.Dispose();
        base.OnExit(e);
    }
}
