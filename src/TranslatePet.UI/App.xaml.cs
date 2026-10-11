using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Application = System.Windows.Application;
using TranslatePet.Core.Interfaces;
using TranslatePet.Core.Models;
using TranslatePet.Core.Services;
using TranslatePet.Core.Services.Ocr;
using TranslatePet.Core.Services.Translation;
using TranslatePet.Core.Services.Vocabulary;
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
    private IVocabularyService _vocabularyService = null!;
    private TranslationWorkflow _workflow = null!;
    private GlobalHotkeyHelper _hotkeyService = null!;
    private TrayIconManager _trayManager = null!;

    private PetWindow _petWindow = null!;
    private ResultWindow? _currentResultWindow;
    private SettingsWindow? _currentSettingsWindow;
    private VocabularyWindow? _currentVocabularyWindow;

    private DispatcherTimer? _dailyReminderTimer;
    private bool _isCapturingWorkflowRunning;

    protected override void OnStartup(StartupEventArgs e)
    {
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 0: OnStartup entered\n"); } catch { }
        base.OnStartup(e);

        AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
        {
            try { System.IO.File.WriteAllText("D:\\温温\\crash.log", ev.ExceptionObject.ToString()); } catch { }
        };
        DispatcherUnhandledException += (s, ev) =>
        {
            try { System.IO.File.WriteAllText("D:\\温温\\crash.log", ev.Exception.ToString()); } catch { }
        };

        // 1. 初始化设置服务与词库服务
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 1: Loading settings\n"); } catch { }
        _settingsService = new SettingsService();
        _settingsService.LoadSettings();
        _vocabularyService = new VocabularyService();
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 2: Settings loaded\n"); } catch { }

        // 2. 初始化核心与服务
        _captureService = new WpfCaptureService();

        var ocrProviders = new IOcrProvider[]
        {
            new WindowsNativeOcrProvider(),
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
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 3: Creating PetWindow\n"); } catch { }
        _petWindow = new PetWindow(_settingsService);
        _petWindow.RequestCapture += (s, ev) => StartCaptureWorkflow();
        _petWindow.RequestVocabulary += (s, ev) => ShowVocabularyWindow();
        _petWindow.RequestSettings += (s, ev) => ShowSettings();
        _petWindow.RequestExit += (s, ev) => ExitApplication();

        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 4: Ensuring HWND\n"); } catch { }
        var helper = new WindowInteropHelper(_petWindow);
        var hWnd = helper.EnsureHandle();

        // 4. 初始化系统托盘
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 5: Creating TrayIconManager\n"); } catch { }
        _trayManager = new TrayIconManager(hWnd);
        _trayManager.RequestCapture += (s, ev) => StartCaptureWorkflow();
        _trayManager.RequestVocabulary += (s, ev) => ShowVocabularyWindow();
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

        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 6: Registering hotkey\n"); } catch { }
        _hotkeyService = new GlobalHotkeyHelper();
        _hotkeyService.Initialize(hWnd);
        _hotkeyService.HotkeyPressed += (s, ev) => StartCaptureWorkflow();
        RegisterConfiguredHotkey();

        // 6. 启动背单词每日提醒调度器
        InitializeDailyReminderTimer();

        // 7. 显示桌宠
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 7: Showing pet window\n"); } catch { }
        MainWindow = _petWindow;
        if (_settingsService.CurrentSettings.PetVisible)
        {
            _petWindow.Show();
        }
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} Step 8: PetWindow.Show() completed successfully\n"); } catch { }
    }

    private void RegisterConfiguredHotkey()
    {
        var s = _settingsService.CurrentSettings;
        var success = _hotkeyService.Register(s.HotkeyAlt, s.HotkeyCtrl, s.HotkeyShift, s.HotkeyWin, s.HotkeyKey, out var error);
        if (!success)
        {
            // 如果是因为设置了被禁用的 Alt+F4，自动智能修复为单键 F4
            if (s.HotkeyAlt && s.HotkeyKey.Equals("F4", StringComparison.OrdinalIgnoreCase) && !s.HotkeyCtrl && !s.HotkeyShift)
            {
                var f4Success = _hotkeyService.Register(false, false, false, false, "F4", out _);
                if (f4Success)
                {
                    s.HotkeyAlt = false;
                    _ = _settingsService.SaveSettingsAsync();
                    _trayManager.ShowNotification("快捷键已自动优化", "由于 Windows 禁止将 Alt+F4 注册为全局热键，已自动优化为快捷单键 [F4]（截图翻译）。");
                    return;
                }
            }

            _trayManager.ShowNotification("快捷键冲突提示", $"未能注册全局快捷键 [{GlobalHotkeyHelper.FormatKey(s.HotkeyAlt, s.HotkeyCtrl, s.HotkeyShift, s.HotkeyWin, s.HotkeyKey)}]：{error}");
        }
    }

    private void InitializeDailyReminderTimer()
    {
        _dailyReminderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _dailyReminderTimer.Tick += OnDailyReminderTick;
        _dailyReminderTimer.Start();
    }

    private void OnDailyReminderTick(object? sender, EventArgs e)
    {
        var s = _settingsService.CurrentSettings;
        if (!s.EnableDailyWordReminder) return;

        var now = DateTime.Now;
        var todayStr = now.ToString("yyyy-MM-dd");
        var timeStr = now.ToString("HH:mm");

        // 如果今天尚未提醒，且当前时间达到或超过设定提醒时间
        if (s.LastReminderDate != todayStr && string.Compare(timeStr, s.DailyReminderTime, StringComparison.Ordinal) >= 0)
        {
            var stats = _vocabularyService.GetStats();
            if (stats.DueReviewWords > 0 || stats.TotalWords > 0)
            {
                s.LastReminderDate = todayStr;
                _ = _settingsService.SaveSettingsAsync();

                _trayManager.ShowNotification(
                    "📚 哆啦A梦每日背单词提醒",
                    $"叮咚！今天有 {stats.DueReviewWords} 个生词待复习哦！快来花 3 分钟复习一下吧~");
            }
        }
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
                _currentResultWindow = new ResultWindow(
                    _workflow,
                    _settingsService,
                    _vocabularyService,
                    _translationService,
                    onOpenVocabularyWindow: ShowVocabularyWindow,
                    onOpenSettingsWindow: ShowSettings);

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
                    await _currentResultWindow.LoadAndTranslateAsync(string.Empty, ocrResult.ErrorMessage);
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

    private void ShowVocabularyWindow()
    {
        try
        {
            if (_currentVocabularyWindow != null && _currentVocabularyWindow.IsLoaded)
            {
                _currentVocabularyWindow.Activate();
                return;
            }

            _currentVocabularyWindow = new VocabularyWindow(
                _vocabularyService,
                _translationService,
                _settingsService);
            _currentVocabularyWindow.Closed += (s, e) => _currentVocabularyWindow = null;
            _currentVocabularyWindow.Show();
        }
        catch (Exception ex)
        {
            try { System.IO.File.WriteAllText("D:\\温温\\crash.log", ex.ToString()); } catch { }
            MessageBox.Show($"打开单词本模块失败:\n{ex.Message}", "单词本错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            _hotkeyService,
            onResetPetPosition: ResetPetPosition
        );

        _currentSettingsWindow.SettingsSaved += (s, e) =>
        {
            _ocrService.SetCurrentProvider(_settingsService.CurrentSettings.OcrProvider);
            _translationService.SetCurrentProvider(_settingsService.CurrentSettings.TranslationProvider);
            RegisterConfiguredHotkey();
            _petWindow.Topmost = _settingsService.CurrentSettings.PetTopMost;
            _petWindow.OnSettingsUpdated();
        };

        _currentSettingsWindow.Show();
    }

    private void ResetPetPosition()
    {
        _petWindow.ResetToDefaultPosition();
    }

    private void ExitApplication()
    {
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} [App] ExitApplication called:\n{Environment.StackTrace}\n"); } catch { }
        _dailyReminderTimer?.Stop();
        _hotkeyService.Dispose();
        _trayManager.Dispose();
        _petWindow.Close();
        _currentResultWindow?.Close();
        _currentSettingsWindow?.Close();
        _currentVocabularyWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { System.IO.File.AppendAllText("D:\\温温\\app.log", $"{DateTime.Now:HH:mm:ss.fff} [App] OnExit called (ExitCode: {e.ApplicationExitCode}):\n{Environment.StackTrace}\n"); } catch { }
        _dailyReminderTimer?.Stop();
        _hotkeyService?.Dispose();
        _trayManager?.Dispose();
        base.OnExit(e);
    }
}
