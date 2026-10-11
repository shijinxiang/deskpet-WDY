using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using TranslatePet.Core.Interfaces;

namespace TranslatePet.UI.Views;

public partial class PetWindow : Window
{
    private enum PetState
    {
        Idle,
        Blinking,
        EatingDorayaki,
        Hovering,
        Dragging,
        Cheering,
        DockedPeeking
    }

    private enum DockEdge
    {
        None,
        Left,
        Right
    }

    private readonly ISettingsService _settingsService;
    private Point _mouseDownPos;
    private bool _isDragging;
    private PetState _state = PetState.Idle;
    private DockEdge _dockEdge = DockEdge.None;

    // 边缘吸附参数 (距离屏幕边缘 50px 内触发吸附)
    private const double SnapThreshold = 50.0;

    // 常态动画 8 帧:
    // 0: 待机自然微笑
    // 1: 眨眼闭目
    // 2: 开心眯眼笑
    // 3: 挥手打招呼
    // 4: 从百宝袋掏出金灿灿铜锣烧
    // 5: 铜锣烧高抛大张嘴
    // 6: 鼓鼓腮帮大嚼特嚼
    // 7: 咽下满足摸肚肚
    private readonly BitmapImage[] _frames = new BitmapImage[8];

    // 探头新帧动画 (左吸附与右吸附各 4 帧)
    // 0: 初探微露 (大眼睛与头顶好奇探出)
    // 1: 侧头探身抓边缘 (小圆手搭住屏幕边缘，歪头好奇张望)
    // 2: 欢快大幅探出 (大微笑致意 / 悬停展开)
    // 3: 俏皮眨眼与致意
    private readonly BitmapImage[] _peekLeftFrames = new BitmapImage[4];
    private readonly BitmapImage[] _peekRightFrames = new BitmapImage[4];

    private int _peekFrameIdx = 0;
    private DateTime _nextPeekAnimTime = DateTime.UtcNow;

    private DispatcherTimer? _animTimer;
    private readonly Random _rng = new();

    private DateTime _nextBlinkTime = DateTime.UtcNow.AddSeconds(2.0);
    private DateTime _nextEatDorayakiTime = DateTime.UtcNow.AddSeconds(8.0); // 启动后 8 秒初次品尝铜锣烧

    private CancellationTokenSource? _actionCts;
    private CancellationTokenSource? _slideCts;
    private Storyboard? _floatStoryboard;

    public event EventHandler? RequestCapture;
    public event EventHandler? RequestVocabulary;
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
        // 1. 加载常态 8 帧
        for (int i = 0; i < 8; i++)
        {
            _frames[i] = LoadFrameBitmap($"frame_{i}.png");
        }

        // 2. 加载左右边缘探头动画帧
        for (int i = 0; i < 4; i++)
        {
            _peekLeftFrames[i] = LoadFrameBitmap($"frame_peek_left_{i}.png");
            _peekRightFrames[i] = LoadFrameBitmap($"frame_peek_right_{i}.png");
        }

        SetFrame(0);
    }

    private static BitmapImage LoadFrameBitmap(string fileName)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/Assets/{fileName}", UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            var diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);
            if (File.Exists(diskPath))
            {
                var bmp = new BitmapImage(new Uri(diskPath));
                bmp.Freeze();
                return bmp;
            }
            throw;
        }
    }

    private void SetFrame(int index)
    {
        if (index >= 0 && index < _frames.Length && _frames[index] != null)
        {
            CharacterFrameImage.Source = _frames[index];
        }
    }

    private void SetPeekFrame(int index)
    {
        if (index < 0 || index >= 4) return;
        var bmp = _dockEdge == DockEdge.Left ? _peekLeftFrames[index] : _peekRightFrames[index];
        if (bmp != null)
        {
            CharacterFrameImage.Source = bmp;
        }
    }

    private void InitializeAnimationTimer()
    {
        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animTimer.Tick += OnAnimationTick;
        _animTimer.Start();
    }

    private CancellationToken ResetActionToken()
    {
        CancelCurrentAction();
        _actionCts = new CancellationTokenSource();
        return _actionCts.Token;
    }

    private void CancelCurrentAction()
    {
        if (_actionCts != null)
        {
            try
            {
                _actionCts.Cancel();
                _actionCts.Dispose();
            }
            catch { }
            _actionCts = null;
        }
    }

    private async void OnAnimationTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;

        // 1. 如果处于边缘吸附探头状态，调度探头帧动画
        if (_dockEdge != DockEdge.None)
        {
            if (_state == PetState.DockedPeeking && !IsMouseOver)
            {
                UpdateDockedPeekingAnimation(now);
            }
            return;
        }

        // 2. 仅在常态严格的 Idle 状态下调度待机动作
        if (_state != PetState.Idle)
        {
            return;
        }

        // 3. 待机定时：从百宝袋掏出铜锣烧抛到口中吃
        if (now >= _nextEatDorayakiTime)
        {
            await PlayEatDorayakiSequenceAsync();
            return;
        }

        // 4. 待机常规自然眨眼
        if (now >= _nextBlinkTime)
        {
            await PlayBlinkSequenceAsync();
        }
    }

    /// <summary>
    /// 边缘吸附状态下的探头帧动画循环
    /// </summary>
    private void UpdateDockedPeekingAnimation(DateTime now)
    {
        if (now < _nextPeekAnimTime) return;

        if (_peekFrameIdx == 0)
        {
            // 探出小脑袋并用小圆手抓边缘 (Frame 1)
            _peekFrameIdx = 1;
            SetPeekFrame(1);
            _nextPeekAnimTime = now.AddSeconds(1.8 + _rng.NextDouble() * 1.5);
        }
        else if (_peekFrameIdx == 1)
        {
            if (_rng.NextDouble() < 0.35)
            {
                // 俏皮眨眼 (Frame 3)
                _peekFrameIdx = 3;
                SetPeekFrame(3);
                _nextPeekAnimTime = now.AddSeconds(0.9);
            }
            else
            {
                // 缩回微露状态 (Frame 0)
                _peekFrameIdx = 0;
                SetPeekFrame(0);
                _nextPeekAnimTime = now.AddSeconds(2.0 + _rng.NextDouble() * 1.8);
            }
        }
        else
        {
            _peekFrameIdx = 0;
            SetPeekFrame(0);
            _nextPeekAnimTime = now.AddSeconds(2.0 + _rng.NextDouble() * 1.8);
        }
    }

    /// <summary>
    /// 待机自然眨眼动作
    /// </summary>
    private async Task PlayBlinkSequenceAsync()
    {
        if (_state != PetState.Idle) return;
        _state = PetState.Blinking;
        var token = ResetActionToken();

        try
        {
            SetFrame(1);
            await Task.Delay(130, token);

            SetFrame(0);

            if (_rng.NextDouble() < 0.35)
            {
                await Task.Delay(80, token);
                SetFrame(1);
                await Task.Delay(110, token);
                SetFrame(0);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
            SetFrame(0);
        }
        finally
        {
            if (_state == PetState.Blinking)
            {
                _state = PetState.Idle;
                SetFrame(0);
            }
            _nextBlinkTime = DateTime.UtcNow.AddSeconds(2.4 + _rng.NextDouble() * 2.8);
        }
    }

    /// <summary>
    /// 经典动作：从百宝袋掏出铜锣烧抛向空中接进嘴里吃掉！
    /// </summary>
    private async Task PlayEatDorayakiSequenceAsync()
    {
        if (_state != PetState.Idle) return;
        _state = PetState.EatingDorayaki;
        var token = ResetActionToken();

        try
        {
            // 动作 1: 从百宝袋掏出金灿灿的铜锣烧举在身前 (Frame 4)
            SetFrame(4);
            await Task.Delay(650, token);

            // 动作 2: 铜锣烧高高抛向空中，仰头大张嘴“啊——”准备接 (Frame 5)
            SetFrame(5);
            await Task.Delay(520, token);

            // 动作 3: 接入口中，肉肉的腮帮子鼓鼓地大口咀嚼嚼 (Frame 6)
            SetFrame(6);
            await Task.Delay(850, token);

            // 动作 4: 咽下肚子，摸摸圆滚滚的肚子，满足地眯眼笑 (Frame 7)
            SetFrame(7);
            await Task.Delay(850, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch
        {
        }
        finally
        {
            if (_state == PetState.EatingDorayaki)
            {
                _state = PetState.Idle;
                SetFrame(0);
            }
            // 下一次吃铜锣烧间隔：20 ~ 38 秒随机触发
            _nextEatDorayakiTime = DateTime.UtcNow.AddSeconds(20 + _rng.NextDouble() * 18);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _floatStoryboard = TryFindResource("FloatStoryboard") as Storyboard;
        _floatStoryboard?.Begin();

        var settings = _settingsService.CurrentSettings;
        if (settings.PetPositionX >= 0 && settings.PetPositionY >= 0)
        {
            Left = settings.PetPositionX;
            Top = settings.PetPositionY;
        }
        else
        {
            var workArea = GetCurrentWorkArea();
            Left = workArea.Right - Width - 30;
            Top = workArea.Bottom - Height - 50;
        }

        if (settings.EnableEdgeSnap)
        {
            CheckAndApplyEdgeSnap(isInitial: true);
        }
        else
        {
            EnsureWithinScreen();
        }
    }

    /// <summary>
    /// 鼠标悬停进入：吸附状态下欢快探头大笑招手(纯帧切换，绝对不移动窗体位置杜绝抽搐)；常态下侧头招手致意
    /// </summary>
    private void OnPetMouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDragging) return;

        // 1. 如果处于边缘吸附状态，直接呈现探头招手大笑脸 (Frame 2)，完全不改变 Window.Left，彻底根除高频抽搐
        if (_dockEdge != DockEdge.None)
        {
            SetPeekFrame(2);
            return;
        }

        if (_state == PetState.Hovering) return;

        _state = PetState.Hovering;
        var token = ResetActionToken();

        // 柔和微放大动效 (1.0 -> 1.05)
        var scaleAnim = new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        _ = PlayHoverSequenceAsync(token);
    }

    private async Task PlayHoverSequenceAsync(CancellationToken token)
    {
        try
        {
            // 动作 1: 发现主人靠近，开心地稍稍侧头并挥手致意 (Frame 3, +2.8° 歪头萌态)
            var tiltAnim = new DoubleAnimation(2.8, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
            };
            PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, tiltAnim);
            SetFrame(3);

            // 保持招手 420ms
            await Task.Delay(420, token);

            // 动作 2: 开心眯眼笑 (Frame 2, 稍稍回正到 +1.2°)
            var tiltAnim2 = new DoubleAnimation(1.2, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, tiltAnim2);
            SetFrame(2);

            await Task.Delay(450, token);

            // 动作 3: 再次欢快招手 (Frame 3)
            SetFrame(3);
            await Task.Delay(400, token);

            // 动作 4: 保持乖巧开心的微笑待命 (Frame 2)
            SetFrame(2);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            SetFrame(0);
        }
    }

    /// <summary>
    /// 鼠标移出：吸附状态下切回静候探头帧 (Frame 0)；常态下平滑复位角度与缩放
    /// </summary>
    private void OnPetMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging) return;

        // 1. 如果处于边缘吸附状态，切回自然探头静候帧 (Frame 0)
        if (_dockEdge != DockEdge.None)
        {
            _peekFrameIdx = 0;
            SetPeekFrame(0);
            _state = PetState.DockedPeeking;
            _nextPeekAnimTime = DateTime.UtcNow.AddSeconds(1.6);
            return;
        }

        CancelCurrentAction();

        // 平滑复位微缩放 (1.05 -> 1.0)
        var scaleAnim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        PetHoverScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        // 平滑复位旋转 ( -> 0°)
        var resetRotate = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
        };
        PetRotateTransform.BeginAnimation(RotateTransform.AngleProperty, resetRotate);

        _state = PetState.Idle;
        SetFrame(0);
        _nextBlinkTime = DateTime.UtcNow.AddSeconds(1.8 + _rng.NextDouble() * 1.5);
    }

    private void OnPetMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            _mouseDownPos = e.GetPosition(this);
            _isDragging = false;
            CaptureMouse();
        }
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
                CancelCurrentAction();
                CancelSlide();

                // 拖动时先暂时退出边缘吸附，恢复常态阴影与身姿
                UnDockSilently();

                _state = PetState.Dragging;
                SetFrame(3); // 被提起移动时挥动小手
                ReleaseMouseCapture();

                // Win32 原生模态拖动 (阻塞直到松开鼠标)
                DragMove();

                _isDragging = false;

                // 检查是否触发边缘吸附
                if (_settingsService.CurrentSettings.EnableEdgeSnap)
                {
                    CheckAndApplyEdgeSnap();
                }
                else
                {
                    UnDock();
                    EnsureWithinScreen();
                }

                _settingsService.CurrentSettings.PetPositionX = Left;
                _settingsService.CurrentSettings.PetPositionY = Top;
                _ = _settingsService.SaveSettingsAsync();

                if (_dockEdge == DockEdge.None)
                {
                    if (IsMouseOver)
                    {
                        _state = PetState.Hovering;
                        SetFrame(2);
                    }
                    else
                    {
                        _state = PetState.Idle;
                        SetFrame(0);
                    }
                }
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
                // 单击触发欢快致意 (Frame 3 或 探头 Frame 2)
                CancelCurrentAction();
                if (_dockEdge != DockEdge.None)
                {
                    SetPeekFrame(2);
                }
                else
                {
                    _state = PetState.Cheering;
                    SetFrame(3);
                }

                RequestCapture?.Invoke(this, EventArgs.Empty);

                await Task.Delay(200);
                if (_dockEdge != DockEdge.None)
                {
                    SetPeekFrame(IsMouseOver ? 2 : _peekFrameIdx);
                }
                else if (IsMouseOver)
                {
                    _state = PetState.Hovering;
                    SetFrame(2);
                }
                else
                {
                    _state = PetState.Idle;
                    SetFrame(0);
                }
            }
        }
        _isDragging = false;
    }

    #region 边缘吸附与探头逻辑

    private void CheckAndApplyEdgeSnap(bool isInitial = false)
    {
        var workArea = GetCurrentWorkArea();

        // 垂直方向限制在屏幕可见工作区内
        if (Top < workArea.Top) Top = workArea.Top;
        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;

        // 水平方向判断左右边缘吸附阈值
        bool nearLeft = (Left - workArea.Left < SnapThreshold) || (Left < workArea.Left);
        bool nearRight = (workArea.Right - (Left + Width) < SnapThreshold) || (Left + Width > workArea.Right);

        if (nearLeft)
        {
            ApplyDock(DockEdge.Left, workArea, isInitial);
        }
        else if (nearRight)
        {
            ApplyDock(DockEdge.Right, workArea, isInitial);
        }
        else
        {
            UnDock();
        }
    }

    private void ApplyDock(DockEdge edge, Rect workArea, bool isInitial = false)
    {
        _dockEdge = edge;
        _state = PetState.DockedPeeking;
        CancelCurrentAction();

        // 停止悬浮起伏与微倾斜，使贴边稳如泰山
        _floatStoryboard?.Stop();
        PetTransform.Y = 0;
        PetHoverScale.ScaleX = 1.0;
        PetHoverScale.ScaleY = 1.0;
        PetRotateTransform.Angle = 0;

        // 边缘吸附时隐藏底部悬浮阴影与角色背后的投影，使截面严丝合缝紧贴屏幕边缘
        ShadowEllipse.Opacity = 0.0;
        CharacterShadowEffect.Opacity = 0.0;

        // 贴边方向对齐：左侧吸附紧贴最左，右侧吸附紧贴最右，消除任何内外边距
        CharacterContainer.HorizontalAlignment = edge == DockEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        CharacterFrameImage.HorizontalAlignment = edge == DockEdge.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;

        double targetLeft = edge == DockEdge.Left
            ? workArea.Left
            : workArea.Right - Width;

        if (isInitial || Math.Abs(Left - targetLeft) < 1.0)
        {
            CancelSlide();
            Left = targetLeft;
        }
        else
        {
            StartDockSlide(targetLeft, 160);
        }

        _peekFrameIdx = 0;
        SetPeekFrame(0);
        _nextPeekAnimTime = DateTime.UtcNow.AddSeconds(1.5);
    }

    private void UnDockSilently()
    {
        _dockEdge = DockEdge.None;
        ShadowEllipse.Opacity = 0.28;
        CharacterShadowEffect.Opacity = 0.25;
        CharacterContainer.HorizontalAlignment = HorizontalAlignment.Center;
        CharacterFrameImage.HorizontalAlignment = HorizontalAlignment.Center;
    }

    private void UnDock()
    {
        if (_dockEdge != DockEdge.None)
        {
            CancelSlide();
            _dockEdge = DockEdge.None;
            ShadowEllipse.Opacity = 0.28;
            CharacterShadowEffect.Opacity = 0.25;
            CharacterContainer.HorizontalAlignment = HorizontalAlignment.Center;
            CharacterFrameImage.HorizontalAlignment = HorizontalAlignment.Center;
            _floatStoryboard?.Begin();
            if (_state == PetState.DockedPeeking)
            {
                _state = PetState.Idle;
                SetFrame(0);
            }
        }
    }

    private void EnsureWithinScreen()
    {
        var workArea = GetCurrentWorkArea();
        if (Left < workArea.Left) Left = workArea.Left;
        if (Left + Width > workArea.Right) Left = workArea.Right - Width;
        if (Top < workArea.Top) Top = workArea.Top;
        if (Top + Height > workArea.Bottom) Top = workArea.Bottom - Height;
    }

    private void CancelSlide()
    {
        if (_slideCts != null)
        {
            try
            {
                _slideCts.Cancel();
                _slideCts.Dispose();
            }
            catch { }
            _slideCts = null;
        }
    }

    private void StartDockSlide(double targetLeft, int durationMs)
    {
        CancelSlide();
        _slideCts = new CancellationTokenSource();
        var token = _slideCts.Token;
        _ = AnimateWindowLeftAsync(targetLeft, durationMs, token);
    }

    /// <summary>
    /// 平滑插值动画滑动 Window.Left (单任务保护，可被随时取消)
    /// </summary>
    private async Task AnimateWindowLeftAsync(double targetLeft, int durationMs, CancellationToken token)
    {
        double startLeft = Left;
        double diff = targetLeft - startLeft;
        if (Math.Abs(diff) < 0.5)
        {
            Left = targetLeft;
            return;
        }

        int steps = Math.Max(1, durationMs / 16);
        for (int i = 1; i <= steps; i++)
        {
            if (token.IsCancellationRequested) return;

            double t = (double)i / steps;
            double progress = 1.0 - (1.0 - t) * (1.0 - t); // Quad ease-out
            Left = startLeft + diff * progress;

            try
            {
                await Task.Delay(16, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (!token.IsCancellationRequested)
        {
            Left = targetLeft;
        }
    }

    /// <summary>
    /// 当用户在设置面板修改配置后通知桌宠响应
    /// </summary>
    public void OnSettingsUpdated()
    {
        if (!_settingsService.CurrentSettings.EnableEdgeSnap && _dockEdge != DockEdge.None)
        {
            UnDock();
            EnsureWithinScreen();
            _settingsService.CurrentSettings.PetPositionX = Left;
            _settingsService.CurrentSettings.PetPositionY = Top;
            _ = _settingsService.SaveSettingsAsync();
        }
        else if (_settingsService.CurrentSettings.EnableEdgeSnap && _dockEdge == DockEdge.None)
        {
            CheckAndApplyEdgeSnap();
        }
    }

    /// <summary>
    /// 重置桌宠位置到屏幕右下角预设点
    /// </summary>
    public void ResetToDefaultPosition()
    {
        UnDock();
        var workArea = GetCurrentWorkArea();
        Left = workArea.Right - Width - 30;
        Top = workArea.Bottom - Height - 50;
        _settingsService.CurrentSettings.PetPositionX = Left;
        _settingsService.CurrentSettings.PetPositionY = Top;
        _ = _settingsService.SaveSettingsAsync();
    }

    #endregion

    #region Win32 多屏幕工作区精准获取

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private Rect GetCurrentWorkArea()
    {
        try
        {
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
            {
                var hMonitor = MonitorFromWindow(helper.Handle, MONITOR_DEFAULTTONEAREST);
                if (hMonitor != IntPtr.Zero)
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        var source = PresentationSource.FromVisual(this);
                        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
                        return new Rect(
                            mi.rcWork.Left / dpiX,
                            mi.rcWork.Top / dpiY,
                            (mi.rcWork.Right - mi.rcWork.Left) / dpiX,
                            (mi.rcWork.Bottom - mi.rcWork.Top) / dpiY);
                    }
                }
            }
        }
        catch { }

        return SystemParameters.WorkArea;
    }

    #endregion

    private void OnPetMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
    }

    private void OnMenuCaptureClick(object sender, RoutedEventArgs e)
    {
        RequestCapture?.Invoke(this, EventArgs.Empty);
    }

    private void OnMenuVocabularyClick(object sender, RoutedEventArgs e)
    {
        RequestVocabulary?.Invoke(this, EventArgs.Empty);
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
        CancelCurrentAction();
        CancelSlide();
        _animTimer?.Stop();
        _settingsService.CurrentSettings.PetPositionX = Left;
        _settingsService.CurrentSettings.PetPositionY = Top;
    }
}
