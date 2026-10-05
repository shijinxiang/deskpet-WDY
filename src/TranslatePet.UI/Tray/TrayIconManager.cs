using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace TranslatePet.UI.Tray;

public class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 100;
    private const int NIM_ADD = 0x00;
    private const int NIM_MODIFY = 0x01;
    private const int NIM_DELETE = 0x02;

    private const int NIF_MESSAGE = 0x01;
    private const int NIF_ICON = 0x02;
    private const int NIF_TIP = 0x04;
    private const int NIF_INFO = 0x10;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly IntPtr _hWnd;
    private readonly ContextMenu _contextMenu;
    private IntPtr _hIcon;
    private bool _isCreated;

    public event EventHandler? RequestCapture;
    public event EventHandler? RequestTogglePet;
    public event EventHandler? RequestSettings;
    public event EventHandler? RequestExit;

    public TrayIconManager(IntPtr hWnd)
    {
        _hWnd = hWnd;

        _contextMenu = new ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x20, 0x22, 0x2A)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x35, 0x38, 0x44)),
            BorderThickness = new Thickness(1)
        };

        var style = new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, System.Windows.Media.Brushes.White));
        style.Setters.Add(new Setter(MenuItem.FontSizeProperty, 13.0));
        style.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(10, 6, 10, 6)));
        _contextMenu.Resources.Add(typeof(MenuItem), style);

        var captureItem = new MenuItem { Header = "🔍 截图翻译 (快捷键)" };
        captureItem.Click += (s, e) => RequestCapture?.Invoke(this, EventArgs.Empty);

        var togglePetItem = new MenuItem { Header = "🐾 显示 / 隐藏桌宠" };
        togglePetItem.Click += (s, e) => RequestTogglePet?.Invoke(this, EventArgs.Empty);

        var settingsItem = new MenuItem { Header = "⚙️ 参数设置" };
        settingsItem.Click += (s, e) => RequestSettings?.Invoke(this, EventArgs.Empty);

        var exitItem = new MenuItem { Header = "🚪 退出程序" };
        exitItem.Click += (s, e) => RequestExit?.Invoke(this, EventArgs.Empty);

        _contextMenu.Items.Add(captureItem);
        _contextMenu.Items.Add(togglePetItem);
        _contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x35, 0x38, 0x44)) });
        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x35, 0x38, 0x44)) });
        _contextMenu.Items.Add(exitItem);

        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 哆啦A梦经典蓝脑袋
            using var blueBrush = new SolidBrush(Color.FromArgb(0, 150, 214));
            g.FillEllipse(blueBrush, 1, 1, 30, 30);

            // 白色下巴与脸部
            using var whiteBrush = new SolidBrush(Color.White);
            g.FillEllipse(whiteBrush, 4, 10, 24, 20);

            // 红项圈
            using var redBrush = new SolidBrush(Color.FromArgb(229, 57, 53));
            g.FillRectangle(redBrush, 6, 26, 20, 3);

            // 金黄色小铃铛
            using var bellBrush = new SolidBrush(Color.FromArgb(255, 215, 0));
            g.FillEllipse(bellBrush, 13, 24, 6, 6);

            // 标志性红色小圆鼻
            g.FillEllipse(redBrush, 14, 12, 4, 4);

            // 眼睛
            using var eyePen = new Pen(Color.FromArgb(30, 30, 30), 1.2f);
            g.DrawEllipse(eyePen, 10, 6, 5, 7);
            g.DrawEllipse(eyePen, 17, 6, 5, 7);
            using var pupilBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
            g.FillEllipse(pupilBrush, 12, 9, 2, 3);
            g.FillEllipse(pupilBrush, 18, 9, 2, 3);
        }

        _hIcon = bmp.GetHicon();

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "哆啦A梦 翻译桌宠 (运行中)"
        };

        _isCreated = Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    public void HandleWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int action = lParam.ToInt32();
            if (action == WM_RBUTTONUP)
            {
                SetForegroundWindow(_hWnd);
                _contextMenu.IsOpen = true;
            }
            else if (action == WM_LBUTTONDBLCLK)
            {
                RequestTogglePet?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void ShowNotification(string title, string text)
    {
        if (!_isCreated) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hWnd,
            uID = 1001,
            uFlags = NIF_INFO,
            szInfoTitle = title,
            szInfo = text,
            dwInfoFlags = 0x00000001 // NIIF_INFO
        };

        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public void Dispose()
    {
        if (_isCreated)
        {
            var nid = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hWnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isCreated = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
