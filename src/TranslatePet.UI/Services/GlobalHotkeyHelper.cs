using System.Windows.Input;
using System.Windows.Interop;
using TranslatePet.Core.Interfaces;
using TranslatePet.UI.Utils;

namespace TranslatePet.UI.Services;

public class GlobalHotkeyHelper : IHotkeyService
{
    private const int HOTKEY_ID = 9001;
    private HwndSource? _hwndSource;
    private bool _isRegistered;

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

    public bool Register(bool alt, bool ctrl, bool shift, bool win, string keyStr)
    {
        Unregister();

        if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
        {
            return false;
        }

        uint modifiers = NativeMethods.MOD_NOREPEAT;
        if (alt) modifiers |= NativeMethods.MOD_ALT;
        if (ctrl) modifiers |= NativeMethods.MOD_CONTROL;
        if (shift) modifiers |= NativeMethods.MOD_SHIFT;
        if (win) modifiers |= NativeMethods.MOD_WIN;

        if (!Enum.TryParse<Key>(keyStr, true, out var key))
        {
            key = Key.Q;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);

        var success = NativeMethods.RegisterHotKey(_hwndSource.Handle, HOTKEY_ID, modifiers, virtualKey);
        _isRegistered = success;
        return success;
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
}
