using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Fragaria.Models;
using Fragaria.Native;

namespace Fragaria.Services;

public sealed record WindowInfo(
    nint Hwnd,
    uint ProcessId,
    string Title,
    string ProcessName,
    byte[]? IconPng);

/// <summary>
/// Enumerates visible top-level windows for per-window routing.
/// Audio is captured per-window via HWND → PID mapping and session title matching.
/// </summary>
public sealed class WindowEnumerator
{
    public IReadOnlyList<WindowInfo> GetAudibleWindows()
    {
        var results = new List<WindowInfo>();
        var seen = new HashSet<nint>();

        User32.EnumWindows((hwnd, _) =>
        {
            if (!IsCandidateWindow(hwnd))
                return true;

            if (!seen.Add(hwnd))
                return true;

            var title = GetTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            User32.GetWindowThreadProcessId(hwnd, out var pid);
            var processName = GetProcessName(pid);

            if (IsFragariaProcess(processName))
                return true;

            results.Add(new WindowInfo(
                hwnd,
                pid,
                title,
                processName,
                CaptureIconPng(hwnd)));

            return true;
        }, 0);

        return results
            .OrderByDescending(w => w.Hwnd == User32.GetForegroundWindow())
            .ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsCandidateWindow(nint hwnd)
    {
        if (!User32.IsWindowVisible(hwnd))
            return false;

        if (User32.GetWindow(hwnd, User32.GW_OWNER) != 0)
            return false;

        var exStyle = User32.GetWindowLong(hwnd, User32.GWL_EXSTYLE);
        if ((exStyle & User32.WS_EX_TOOLWINDOW) != 0 && (exStyle & User32.WS_EX_APPWINDOW) == 0)
            return false;

        return true;
    }

    private static string GetTitle(nint hwnd)
    {
        var len = User32.GetWindowTextLength(hwnd);
        if (len <= 0) return "";
        var sb = new StringBuilder(len + 1);
        User32.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString().Trim();
    }

    private static string GetProcessName(uint pid)
    {
        try
        {
            return Path.GetFileNameWithoutExtension(Process.GetProcessById((int)pid).ProcessName);
        }
        catch
        {
            var handle = Kernel32.OpenProcess(Kernel32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (handle == 0) return $"pid-{pid}";
            try
            {
                var sb = new StringBuilder(1024);
                var size = sb.Capacity;
                if (Kernel32.QueryFullProcessImageName(handle, 0, sb, ref size))
                    return Path.GetFileNameWithoutExtension(sb.ToString());
            }
            finally
            {
                CloseHandle(handle);
            }
            return $"pid-{pid}";
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint hObject);

    private static bool IsFragariaProcess(string name) =>
        name.Contains("Fragaria", StringComparison.OrdinalIgnoreCase);

    private static byte[]? CaptureIconPng(nint hwnd)
    {
        try
        {
            var hIcon = User32.SendMessage(hwnd, User32.WM_GETICON, User32.ICON_SMALL, 0);
            if (hIcon == 0)
                hIcon = User32.SendMessage(hwnd, User32.WM_GETICON, User32.ICON_BIG, 0);
            if (hIcon == 0) return null;

            using var icon = Icon.FromHandle(hIcon);
            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
