using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TranslatePet.UI.Utils;

/// <summary>
/// 现代化 Windows 毛玻璃 / 亚克力 (Acrylic / Blur Behind) 辅助类
/// 支持 Windows 10 (1803+) 与 Windows 11 (21H2/22H2/23H2+) 硬件加速毛玻璃模糊
/// </summary>
public static class WindowBlurHelper
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic backdrop

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", PreserveSig = true)]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum AccentState
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_INVALID_STATE = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public int GradientColor; // AABBGGRR
        public int AnimationId;
    }

    /// <summary>
    /// 为指定 WPF 窗口启用沉浸式毛玻璃效果
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="tintColor">半透明滤镜底色 (默认深邃极夜蓝灰 #151824)</param>
    public static void EnableBlur(Window window, uint tintColor = 0x66241815)
    {
        if (window == null) return;

        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                ApplyBlurToHandle(handle, tintColor);
            }
        }

        if (window.IsLoaded)
        {
            Apply();
        }
        else
        {
            window.SourceInitialized += (s, e) => Apply();
        }
    }

    private static void ApplyBlurToHandle(IntPtr handle, uint tintColor)
    {
        try
        {
            // 1. 设置 Windows 11 深色模式与圆角偏好
            int darkMode = 1;
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            int cornerPref = DWMWCP_ROUND;
            DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPref, sizeof(int));

            // 2. 尝试 Win11 DWM 系统 Acrylic 材质
            int backdrop = DWMSBT_TRANSIENTWINDOW;
            DwmSetWindowAttribute(handle, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));

            // 3. 应用 SetWindowCompositionAttribute 亚克力毛玻璃 (对 AllowsTransparency=True 层叠窗口原生生效)
            var accent = new AccentPolicy
            {
                AccentState = AccentState.ACCENT_ENABLE_ACRYLICBLURBEHIND,
                AccentFlags = 0, // 0: 无额外多余硬线边框，纯净毛玻璃
                GradientColor = unchecked((int)tintColor),
                AnimationId = 0
            };

            int accentStructSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentStructSize);
            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);

                var data = new WindowCompositionAttributeData
                {
                    Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                    Data = accentPtr,
                    SizeOfData = accentStructSize
                };

                int status = SetWindowCompositionAttribute(handle, ref data);
                if (status == 0)
                {
                    // 若 Acrylic 状态受阻，优雅降级为传统高效 BlurBehind
                    accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;
                    Marshal.StructureToPtr(accent, accentPtr, false);
                    SetWindowCompositionAttribute(handle, ref data);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
        catch
        {
            // 异常降级防崩
        }
    }
}
