using System.Runtime.InteropServices;

namespace Fragaria.Services;

/// <summary>Subclass Win32 HWND to receive WM_HOTKEY in WinUI 3.</summary>
public sealed class HwndMessageHook : IDisposable
{
    private readonly nint _hwnd;
    private readonly GlobalHotkeyService _hotkeys;
    private nint _oldWndProc;
    private readonly WndProcDelegate _wndProc;
    private bool _disposed;

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_HOTKEY = 0x0312;

    public HwndMessageHook(nint hwnd, GlobalHotkeyService hotkeys)
    {
        _hwnd = hwnd;
        _hotkeys = hotkeys;
        _wndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WM_HOTKEY)
            _hotkeys.ProcessMessage(msg, wParam);
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_oldWndProc != 0)
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
    }
}
