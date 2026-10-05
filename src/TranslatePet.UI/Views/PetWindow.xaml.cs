using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TranslatePet.Core.Interfaces;

namespace TranslatePet.UI.Views;

public partial class PetWindow : Window
{
    private readonly ISettingsService _settingsService;
    private Point _mouseDownPos;
    private bool _isDragging;
    private bool _isMouseOver;

    private readonly BitmapImage[] _frames = new BitmapImage[4];
    private DispatcherTimer? _animTimer;
    private int _hoverFrameIndex = 2;
    private Random _rng = new();
    private DateTime _nextBlinkTime = DateTime.UtcNow.AddSeconds(2);
    private bool _isBlinking;

    public event EventHandler? RequestCapture;
    public event EventHandler? RequestSettings;
    public event EventHandler? RequestExit;

    public PetWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;

        LoadAnimationFrames();
        InitializeAnimationTimer();
    }

    private void LoadAnimationFrames()
    {
        for (int i = 0; i < 4; i++)
        {
            try
            {
                var uri = new Uri($"pack://application:,,,/Assets/frame_{i}.png", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _frames[i] = bmp;
            }
            catch
            {
                // Fallback to disk if pack URI fails
                var diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"frame_{i}.png");
                if (File.Exists(diskPath))
                {
                    var bmp = new BitmapImage(new Uri(diskPath));
                    bmp.Freeze();
                    _frames[i] = bmp;
                }
            }
        }

        // 默认显示待机常态帧 (Frame 0)
        SetFrame(0);
    }

    private void SetFrame(int index)
    {
        if (index >= 0 && index < _frames.Length && _frames[index] != null)
        {
            CharacterFrameImage.Source = _frames[index];
        }
    }

    private void InitializeAnimationTimer()
    {
        // 50ms (20fps) 精细帧调度器
        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animTimer.Tick += OnAnimationTick;
        _animTimer.Start();
    }

    private async void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_isMouseOver)
        {
            // 鼠标悬停交互状态：在 Frame 2 与 Frame 3 之间交替招手
            return; // 悬停切帧由专属悬停循环驱动
        }

        if (_isBlinking) return;

        // 待机自然眨眼调度
        if (DateTime.UtcNow >= _nextBlinkTime)
        {
            _isBlinking = true;
            try
            {
                // 闭眼 (Frame 1)
                SetFrame(1);
                await Task.Delay(130);

                // 睁眼 (Frame 0)
                SetFrame(0);

                // 35% 几率触发连眨 (Double Blink)
                if (_rng.NextDouble() < 0.35)
                {
                    await Task.Delay(80);
                    SetFrame(1);
                    await Task.Delay(110);
                    SetFrame(0);
                }
            }
            catch
            {
                SetFrame(0);
            }
            finally
            {
                _isBlinking = false;
                // 下一次眨眼时间 (2.2 ~ 4.8 秒随机间隔)
                _nextBlinkTime = DateTime.UtcNow.AddSeconds(2.2 + _rng.NextDouble() * 2.6);
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 启动物理浮动呼吸动画
        var floatSb = TryFindResource("FloatStoryboard") as Storyboard;
        floatSb?.Begin();

        // 恢复位置
        var settings = _settingsService.CurrentSettings;
        if (settings.PetPositionX >= 0 && settings.PetPositionY >= 0)
        {
            Left = settings.PetPositionX;
            Top = settings.PetPositionY;
        }
        else
        {
            Left = SystemParameters.WorkArea.Right - Width - 30;
            Top = SystemParameters.WorkArea.Bottom - Height - 50;
        }
    }

    private void OnPetMouseEnter(object sender, MouseEventArgs e)
    {
        _isMouseOver = true;

        // 轻微放大与摇晃
        var scaleAnim = new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        // 启动悬停招手切帧循环
        _ = RunHoverAnimationAsync();
    }

    private async Task RunHoverAnimationAsync()
    {
        double angle = 3.0;
        while (_isMouseOver)
        {
            // Frame 2 -> Frame 3 循环切换
            SetFrame(_hoverFrameIndex);
            _hoverFrameIndex = (_hoverFrameIndex == 2) ? 3 : 2;

            // 头部轻微摆动
            var rotateAnim = new DoubleAnimation(angle, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            angle = -angle;

            await Task.Delay(200);
        }

        // 恢复正常角度
        var resetRotate = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, resetRotate);
    }

    private void OnPetMouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOver = false;

        var scaleAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        // 恢复常态帧
        SetFrame(0);
        _nextBlinkTime = DateTime.UtcNow.AddSeconds(1.5);
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

                _settingsService.CurrentSettings.PetPositionX = Left;
                _settingsService.CurrentSettings.PetPositionY = Top;
                _ = _settingsService.SaveSettingsAsync();
            }
        }
    }

    protected override async void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            if (!_isDragging)
            {
                // 单击欢快庆祝动作反馈 (Frame 3)
                SetFrame(3);
                await Task.Delay(180);

                // 触发截图翻译流程
                RequestCapture?.Invoke(this, EventArgs.Empty);

                SetFrame(0);
            }
        }
        _isDragging = false;
    }

    private void OnPetMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
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
        _animTimer?.Stop();
        _settingsService.CurrentSettings.PetPositionX = Left;
        _settingsService.CurrentSettings.PetPositionY = Top;
    }
}
