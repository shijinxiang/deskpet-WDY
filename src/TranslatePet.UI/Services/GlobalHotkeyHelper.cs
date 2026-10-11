using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using TranslatePet.Core.Interfaces;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Services;

public class GlobalHotkeyHelper : IHotkeyService
{
    private const int HOTKEY_ID = 9001;
    private const int TEST_HOTKEY_ID = 9002;

    private HwndSource? _hwndSource;
    private bool _isRegistered;
    private (bool Alt, bool Ctrl, bool Shift, bool Win, string Key) _currentRegistration;

    public event EventHandler? HotkeyPressed;
    public bool IsRegistered => _isRegistered;

    public GlobalHotkeyHelper()
    {
    }

    public void Initialize(IntPtr windowHandle)
    {
        _hwndSource = HwndSource.FromHwnd(windowHandle);
        _hwndSource?.AddHook(HwndHook);
    }

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    public bool Register(bool alt, bool ctrl, bool shift, bool win, string keyStr)
    {
        return Register(alt, ctrl, shift, win, keyStr, out _);
    }

    /// <summary>
    /// 注册全局快捷键并输出详细错误诊断
    /// </summary>
    public bool Register(bool alt, bool ctrl, bool shift, bool win, string keyStr, out string errorMessage)
    {
        errorMessage = string.Empty;

        // 1. 静态与系统规则校验
        var validation = ValidateHotkey(alt, ctrl, shift, win, keyStr);
        if (!validation.Success)
        {
            errorMessage = validation.Message;
            return false;
        }

        Unregister();

        if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
        {
            errorMessage = "主窗口句柄尚未就绪，无法注册快捷键。";
            return false;
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        if (alt) modifiers |= NativeMethods.MOD_ALT;
        if (ctrl) modifiers |= NativeMethods.MOD_CONTROL;
        if (shift) modifiers |= NativeMethods.MOD_SHIFT;
        if (win) modifiers |= NativeMethods.MOD_WIN;

        var (_, vk) = ParseKey(keyStr);

        var success = NativeMethods.RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, modifiers, vk);
        _isRegistered = success;

        if (success)
        {
            _currentRegistration = (alt, ctrl, shift, win, keyStr);
        }
        else
        {
            int err = Marshal.GetLastWin32Error();
            errorMessage = err == 1409
                ? $"快捷键 [{FormatKey(alt, ctrl, shift, win, keyStr)}] 已被其他应用或系统功能占用，请更换按键。"
                : $"快捷键注册失败 (Win32 错误码: {err})。";
        }

        return success;
    }

    /// <summary>
    /// 预检并测试指定的快捷键是否合法且可用
    /// </summary>
    public (bool Success, string Message) ValidateHotkey(bool alt, bool ctrl, bool shift, bool win, string keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr))
        {
            return (false, "请选择或按下主按键（如 Q、W、F4 等）。");
        }

        string upperKey = keyStr.Trim().ToUpperInvariant();
        bool hasModifier = alt || ctrl || shift || win;
        bool isFKey = IsFunctionKey(upperKey);

        // 1. 系统绝对保留快捷键拦截
        if (alt && !ctrl && !shift && !win && upperKey == "F4")
        {
            return (false, "Alt+F4 为 Windows 窗口关闭全局保留键，系统禁止注册。建议使用单键 F4（无需勾选 Alt）或 Alt+Q、Alt+W。");
        }

        if (alt && !ctrl && !shift && !win && upperKey == "TAB")
        {
            return (false, "Alt+Tab 为 Windows 切换窗口保留快捷键，禁止注册。");
        }

        if (ctrl && alt && !shift && !win && (upperKey == "DELETE" || upperKey == "DEL"))
        {
            return (false, "Ctrl+Alt+Del 为 Windows 安全桌面保留快捷键，禁止注册。");
        }

        if (win && (upperKey == "L" || upperKey == "D" || upperKey == "R" || upperKey == "X"))
        {
            return (false, $"Win+{upperKey} 为 Windows 系统自带功能键，禁止注册。");
        }

        // 2. 普通字符必须搭配修饰键，避免阻断用户日常打字输入
        if (!hasModifier && !isFKey && upperKey != "PAUSE" && upperKey != "PRINTSCREEN")
        {
            return (false, $"普通字符 [{upperKey}] 必须搭配至少一个修饰键（如 Alt、Ctrl 或 Shift），否则将导致日常打字无法输入该字符。");
        }

        // 3. 动态调用 Win32 RegisterHotKey 探测是否冲突
        // 如果当前要测试的键就是当前正在生效的键，直接判定成功
        if (_isRegistered &&
            _currentRegistration.Alt == alt &&
            _currentRegistration.Ctrl == ctrl &&
            _currentRegistration.Shift == shift &&
            _currentRegistration.Win == win &&
            _currentRegistration.Key.Equals(keyStr, StringComparison.OrdinalIgnoreCase))
        {
            return (true, "✅ 当前快捷键已生效，可正常使用。");
        }

        // 临时注销已有热键以防干扰测试
        bool wasRegistered = _isRegistered;
        var prevReg = _currentRegistration;
        if (wasRegistered)
        {
            Unregister();
        }

        try
        {
            uint modifiers = NativeMethods.MOD_NOREPEAT;
            if (alt) modifiers |= NativeMethods.MOD_ALT;
            if (ctrl) modifiers |= NativeMethods.MOD_CONTROL;
            if (shift) modifiers |= NativeMethods.MOD_SHIFT;
            if (win) modifiers |= NativeMethods.MOD_WIN;

            var (_, vk) = ParseKey(keyStr);

            IntPtr testHwnd = _hwndSource?.Handle ?? IntPtr.Zero;
            bool ok = NativeMethods.RegisterHotKey(testHwnd, TEST_HOTKEY_ID, modifiers, vk);
            int err = Marshal.GetLastWin32Error();

            if (ok)
            {
                NativeMethods.UnregisterHotKey(testHwnd, TEST_HOTKEY_ID);
                return (true, "✅ 快捷键有效且未被占用，可正常使用！");
            }
            else
            {
                if (err == 1409)
                {
                    return (false, $"⚠️ [{FormatKey(alt, ctrl, shift, win, keyStr)}] 已被其他运行中的软件（如 QQ 截图、微信等）或系统占用，请更换其他组合。");
                }
                return (false, $"⚠️ 快捷键无法注册 (Win32 错误码: {err})。");
            }
        }
        finally
        {
            // 恢复此前生效的热键
            if (wasRegistered)
            {
                Register(prevReg.Alt, prevReg.Ctrl, prevReg.Shift, prevReg.Win, prevReg.Key);
            }
        }
    }

    public void Unregister()
    {
        if (_isRegistered && _hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwndSource.Handle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }
    }

    public static bool IsFunctionKey(string keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr)) return false;
        keyStr = keyStr.Trim().ToUpperInvariant();
        return keyStr.StartsWith("F") && int.TryParse(keyStr[1..], out int num) && num >= 1 && num <= 24;
    }

    public static string FormatKey(bool alt, bool ctrl, bool shift, bool win, string keyStr)
    {
        var list = new List<string>();
        if (ctrl) list.Add("Ctrl");
        if (alt) list.Add("Alt");
        if (shift) list.Add("Shift");
        if (win) list.Add("Win");
        list.Add(keyStr.ToUpperInvariant());
        return string.Join(" + ", list);
    }

    public static (Key Key, uint VirtualKey) ParseKey(string keyStr)
    {
        if (string.IsNullOrWhiteSpace(keyStr))
        {
            return (Key.Q, 0x51);
        }

        keyStr = keyStr.Trim().ToUpperInvariant();

        // 1. Function keys F1 - F24 (VK_F1 is 0x70)
        if (keyStr.StartsWith("F") && int.TryParse(keyStr[1..], out int fNum) && fNum >= 1 && fNum <= 24)
        {
            var fKey = (Key)((int)Key.F1 + (fNum - 1));
            return (fKey, (uint)(0x70 + (fNum - 1)));
        }

        // 2. Digits 0 - 9 (VK_0 is 0x30)
        if (keyStr.Length == 1 && char.IsDigit(keyStr[0]))
        {
            int digit = keyStr[0] - '0';
            var dKey = (Key)((int)Key.D0 + digit);
            return (dKey, (uint)(0x30 + digit));
        }

        // 3. Letters A - Z (VK_A is 0x41)
        if (keyStr.Length == 1 && keyStr[0] >= 'A' && keyStr[0] <= 'Z')
        {
            int offset = keyStr[0] - 'A';
            var aKey = (Key)((int)Key.A + offset);
            return (aKey, (uint)(0x41 + offset));
        }

        // 4. Special keys
        switch (keyStr)
        {
            case "SPACE":
            case "空格":
                return (Key.Space, 0x20);
            case "~":
            case "`":
            case "OEM3":
                return (Key.Oem3, 0xC0);
            case "TAB":
                return (Key.Tab, 0x09);
            case "-":
            case "MINUS":
                return (Key.OemMinus, 0xBD);
            case "=":
            case "PLUS":
                return (Key.OemPlus, 0xBB);
            case "[":
                return (Key.OemOpenBrackets, 0xDB);
            case "]":
                return (Key.Oem6, 0xDD);
            case ";":
                return (Key.Oem1, 0xBA);
            case "'":
                return (Key.Oem7, 0xDE);
            case ",":
                return (Key.OemComma, 0xBC);
            case ".":
                return (Key.OemPeriod, 0xBE);
            case "/":
                return (Key.OemQuestion, 0xBF);
        }

        // 5. Enum parse fallback
        if (Enum.TryParse<Key>(keyStr, true, out var parsedKey))
        {
            return (parsedKey, (uint)KeyInterop.VirtualKeyFromKey(parsedKey));
        }

        return (Key.Q, 0x51);
    }
}
