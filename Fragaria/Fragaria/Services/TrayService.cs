using System.Drawing;
using System.Runtime.InteropServices;

namespace Fragaria.Services;

/// <summary>
/// System tray icon using WinForms NotifyIcon (lightweight, works with WinUI 3).
/// </summary>
public sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private bool _disposed;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public TrayService()
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Fragaria",
            Visible = true
        };

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "fragaria.ico");
            if (File.Exists(iconPath))
                _icon.Icon = new Icon(iconPath);
            else
                _icon.Icon = SystemIcons.Application;
        }
        catch
        {
            _icon.Icon = SystemIcons.Application;
        }

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Открыть Fragaria", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Выход", null, (_, _) => ExitRequested?.Invoke());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(3000, title, text, System.Windows.Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
    }
}
