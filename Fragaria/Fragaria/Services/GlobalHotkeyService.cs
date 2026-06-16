using System.Runtime.InteropServices;
using Fragaria.Models;

namespace Fragaria.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly nint _hwnd;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 1;
    private bool _disposed;

    public GlobalHotkeyService(nint hwnd) => _hwnd = hwnd;

    public int Register(uint modifiers, uint vk, Action handler)
    {
        var id = _nextId++;
        if (!RegisterHotKey(_hwnd, id, modifiers, vk))
            return -1;
        _handlers[id] = handler;
        return id;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_hwnd, id);
        _handlers.Clear();
    }

    public void ProcessMessage(uint msg, nint wParam)
    {
        if (msg != WM_HOTKEY) return;
        var id = (int)wParam;
        if (_handlers.TryGetValue(id, out var h)) h();
    }

    public void RegisterScenes(IEnumerable<ScenePreset> scenes, Action<ScenePreset> onScene)
    {
        UnregisterAll();
        foreach (var scene in scenes)
        {
            if (scene.HotkeyKey == 0) continue;
            Register((uint)scene.HotkeyModifiers, (uint)scene.HotkeyKey, () => onScene(scene));
        }
    }

    private const uint WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        UnregisterAll();
    }
}
