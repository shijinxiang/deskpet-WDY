using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TranslatePet.Core.Interfaces;

namespace TranslatePet.UI.Views;

public partial class PetWindow : Window
{
    private readonly ISettingsService _settingsService;
    private Point _mouseDownPos;
    private bool _isDragging;

    public event EventHandler? RequestCapture;
    public event EventHandler? RequestSettings;
    public event EventHandler? RequestExit;

    public PetWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 启动待机呼吸动画
        var floatSb = TryFindResource("FloatStoryboard") as Storyboard;
        floatSb?.Begin();

        var blinkSb = TryFindResource("BlinkStoryboard") as Storyboard;
        blinkSb?.Begin();

        // 恢复位置
        var settings = _settingsService.CurrentSettings;
        if (settings.PetPositionX >= 0 && settings.PetPositionY >= 0)
        {
            Left = settings.PetPositionX;
            Top = settings.PetPositionY;
        }
        else
        {
            // 默认停靠在工作区右下角
            Left = SystemParameters.WorkArea.Right - Width - 30;
            Top = SystemParameters.WorkArea.Bottom - Height - 50;
        }
    }

    private void OnPetMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDownPos = e.GetPosition(this);
        _isDragging = false;
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (IsMouseCaptured)
        {
            var curPos = e.GetPosition(this);
            if (!_isDragging && (Math.Abs(curPos.X - _mouseDownPos.X) > 4 || Math.Abs(curPos.Y - _mouseDownPos.Y) > 4))
            {
                _isDragging = true;
                ReleaseMouseCapture();
                DragMove();

                // 拖拽结束保存坐标
                _settingsService.CurrentSettings.PetPositionX = Left;
                _settingsService.CurrentSettings.PetPositionY = Top;
                _ = _settingsService.SaveSettingsAsync();
            }
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            if (!_isDragging)
            {
                // 单击触发截图翻译流程
                RequestCapture?.Invoke(this, EventArgs.Empty);
            }
        }
        _isDragging = false;
    }

    private void OnPetMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // 右键弹出菜单
    }

    private void OnMenuCaptureClick(object sender, RoutedEventArgs e)
    {
        RequestCapture?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuSettingsClick(object sender, RoutedEventArgs e)
    {
        RequestSettings?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuHideClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnMenuExitClick(object sender, RoutedEventArgs e)
    {
        RequestExit?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // 记忆位置
        _settingsService.CurrentSettings.PetPositionX = Left;
        _settingsService.CurrentSettings.PetPositionY = Top;
    }
}
