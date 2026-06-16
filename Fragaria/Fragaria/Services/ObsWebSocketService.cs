using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Fragaria.Models;

namespace Fragaria.Services;

/// <summary>OBS WebSocket v5 — scene changes trigger mixer actions.</summary>
public sealed class ObsWebSocketService : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private readonly ObsSettings _settings;
    private bool _disposed;

    public event Action<string>? SceneChanged;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public ObsWebSocketService(ObsSettings settings) => _settings = settings;

    public async Task ConnectAsync()
    {
        if (!_settings.Enabled) return;
        await DisconnectAsync();

        _cts = new CancellationTokenSource();
        _ws = new ClientWebSocket();
        try
        {
            var uri = new Uri(_settings.Host);
            await _ws.ConnectAsync(uri, _cts.Token);
            ConnectionChanged?.Invoke(true);
            _ = ReceiveLoop(_cts.Token);
        }
        catch
        {
            ConnectionChanged?.Invoke(false);
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_ws?.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        _ws?.Dispose();
        _ws = null;
        ConnectionChanged?.Invoke(false);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var buf = new byte[8192];
        while (_ws is { State: WebSocketState.Open } && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buf, ct);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var json = Encoding.UTF8.GetString(buf, 0, result.Count);
                ParseMessage(json);
            }
            catch { break; }
        }
        ConnectionChanged?.Invoke(false);
    }

    private void ParseMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("op", out var op)) return;
            if (op.GetInt32() != 5) return; // Event
            if (!doc.RootElement.TryGetProperty("d", out var data)) return;
            if (data.TryGetProperty("eventType", out var et) &&
                et.GetString() == "CurrentProgramSceneChanged" &&
                data.TryGetProperty("eventData", out var ev) &&
                ev.TryGetProperty("sceneName", out var sn))
            {
                SceneChanged?.Invoke(sn.GetString() ?? "");
            }
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ = DisconnectAsync();
        _cts?.Dispose();
    }
}
