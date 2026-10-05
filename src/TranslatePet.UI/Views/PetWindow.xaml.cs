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

    // 总共 8 帧: 0(待机), 1(眨眼), 2(开心), 3(挥手), 4(掏铜锣烧), 5(抛起大张嘴), 6(大嚼特嚼), 7(满足眯眯笑)
    private readonly BitmapImage[] _frames = new BitmapImage[8];
    private DispatcherTimer? _animTimer;
    private int _hoverFrameIndex = 2;
    private readonly Random _rng = new();

    private DateTime _nextBlinkTime = DateTime.UtcNow.AddSeconds(2);
    private DateTime _nextEatDorayakiTime = DateTime.UtcNow.AddSeconds(8); // 启动后 8 秒初尝铜锣烧
    private bool _isBlinking;
    private bool _isEatingDorayaki;

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
        for (int i = 0; i < 8; i++)
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
                var diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", $"frame_{i}.png");
                if (File.Exists(diskPath))
                {
                    var bmp = new BitmapImage(new Uri(diskPath));
                    bmp.Freeze();
                    _frames[i] = bmp;
                }
            }
        }

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
        if (_isMouseOver || _isDragging || _isEatingDorayaki)
        {
            return;
        }

        // 1. 待机定时：从百宝袋掏出铜锣烧抛到口中吃
        if (DateTime.UtcNow >= _nextEatDorayakiTime && !_isBlinking)
        {
            _ = PlayEatDorayakiSequenceAsync();
            return;
        }

        // 2. 待机常规自然眨眼
        if (DateTime.UtcNow >= _nextBlinkTime && !_isBlinking)
        {
            _isBlinking = true;
            try
            {
                SetFrame(1);
                await Task.Delay(130);

                SetFrame(0);

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
                _nextBlinkTime = DateTime.UtcNow.AddSeconds(2.2 + _rng.NextDouble() * 2.6);
            }
        }
    }

    /// <summary>
    /// 经典动作：从百宝袋掏出铜锣烧抛向空中接进嘴里吃掉！
    /// </summary>
    private async Task PlayEatDorayakiSequenceAsync()
    {
        _isEatingDorayaki = true;
        try
        {
            // 动作 1: 从百宝袋掏出金灿灿的铜锣烧举在身前 (Frame 4)
            SetFrame(4);
            await Task.Delay(600);
            if (!_isEatingDorayaki) return;

            // 动作 2: 铜锣烧高高抛向空中，仰头大张嘴“啊——”准备接 (Frame 5)
            SetFrame(5);
            await Task.Delay(480);
            if (!_isEatingDorayaki) return;

            // 动作 3: 接入口中，肉肉的腮帮子鼓鼓地大口咀嚼嚼 (Frame 6)
            SetFrame(6);
            await Task.Delay(750);
            if (!_isEatingDorayaki) return;

            // 动作 4: 咽下肚子，摸摸圆滚滚的肚子，满足地眯眼笑 (Frame 7)
            SetFrame(7);
            await Task.Delay(800);
        }
        catch
        {
            // Ignore cancellation
        }
        finally
        {
            if (!_isMouseOver)
            {
                SetFrame(0);
            }
            _isEatingDorayaki = false;
            // 下一次吃铜锣烧间隔：14 ~ 28 秒随机触发
            _nextEatDorayakiTime = DateTime.UtcNow.AddSeconds(14 + _rng.NextDouble() * 14);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var floatSb = TryFindResource("FloatStoryboard") as Storyboard;
        floatSb?.Begin();

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
        _isEatingDorayaki = false; // 鼠标互动打断进食，优先响应互动

        var scaleAnim = new DoubleAnimation(1.08, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        _ = RunHoverAnimationAsync();
    }

    private async Task RunHoverAnimationAsync()
    {
        double angle = 2.5;
        while (_isMouseOver)
        {
            SetFrame(_hoverFrameIndex);
            _hoverFrameIndex = (_hoverFrameIndex == 2) ? 3 : 2;

            var rotateAnim = new DoubleAnimation(angle, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
            angle = -angle;

            await Task.Delay(200);
        }

        var resetRotate = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, resetRotate);
    }

    private void OnPetMouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOver = false;

        var scaleAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

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
                _isEatingDorayaki = false;
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
                // 单击触发欢呼动作 (Frame 3)
                SetFrame(3);
                await Task.Delay(160);

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
