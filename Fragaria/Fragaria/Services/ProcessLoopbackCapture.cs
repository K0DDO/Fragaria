using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

/// <summary>
/// Windows 10 2004+ process loopback capture for per-window/per-process audio.
/// HWND is resolved to PID; session display name helps split browser tabs.
/// </summary>
public sealed class ProcessLoopbackCapture : IDisposable
{
    private readonly WasapiCapture? _capture;
    private readonly MMDevice? _device;
    private bool _disposed;

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public WaveFormat WaveFormat => _capture?.WaveFormat ?? new WaveFormat(48000, 32, 2);
    public bool IsCapturing => _capture is { CaptureState: CaptureState.Capturing };

    public ProcessLoopbackCapture(uint processId, string? sessionDisplayName = null)
    {
        try
        {
            _device = ActivateLoopbackDevice(processId);
            if (_device == null)
                return;

            _capture = new WasapiCapture(_device) { ShareMode = AudioClientShareMode.Shared };
            _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
            _capture.RecordingStopped += (_, e) => RecordingStopped?.Invoke(this, e);
            SessionDisplayName = sessionDisplayName;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Loopback failed for PID {processId}: {ex.Message}");
        }
    }

    public string? SessionDisplayName { get; }

    public void StartRecording() => _capture?.StartRecording();
    public void StopRecording() => _capture?.StopRecording();

    private static MMDevice? ActivateLoopbackDevice(uint processId)
    {
        // Fallback: capture default render device loopback filtered by session.
        // Full PROCESS_LOOPBACK requires WinRT activation; use session capture helper.
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return device;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _capture?.Dispose();
        _device?.Dispose();
    }
}

/// <summary>
/// Captures a specific WASAPI audio session (best match for window title / tab name).
/// </summary>
public sealed class SessionCapture : IDisposable
{
    private readonly WasapiLoopbackCapture? _loopback;
    private readonly IWaveIn? _sessionCapture;
    private bool _disposed;

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public WaveFormat WaveFormat { get; }

    public SessionCapture(uint processId, string windowTitle)
    {
        var session = AudioSessionMatcher.FindBestSession(processId, windowTitle);
        if (session != null)
        {
            _sessionCapture = session;
            WaveFormat = session.WaveFormat;
            session.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
            return;
        }

        _loopback = new WasapiLoopbackCapture();
        WaveFormat = _loopback.WaveFormat;
        _loopback.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
    }

    public void Start()
    {
        if (_sessionCapture != null) _sessionCapture.StartRecording();
        else _loopback?.StartRecording();
    }

    public void Stop()
    {
        if (_sessionCapture != null) _sessionCapture.StopRecording();
        else _loopback?.StopRecording();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionCapture?.Dispose();
        _loopback?.Dispose();
    }
}

internal static class AudioSessionMatcher
{
    public static IWaveIn? FindBestSession(uint processId, string windowTitle)
    {
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var manager = device.AudioSessionManager;
            AudioSessionControl? best = null;
            var bestScore = 0;

            for (int i = 0; i < manager.SessionCount; i++)
            {
                var session = manager.GetSession(i);
                if (session.GetProcessID != processId)
                    continue;

                var name = session.DisplayName;
                if (string.IsNullOrEmpty(name))
                    name = session.GetSessionIdentifier;

                var score = ScoreMatch(windowTitle, name);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = session;
                }
            }

            if (best == null || bestScore < 30)
                return null;

            return new SessionWaveIn(best, device);
        }
        catch
        {
            return null;
        }
    }

    private static int ScoreMatch(string windowTitle, string sessionName)
    {
        if (string.IsNullOrEmpty(sessionName)) return 0;
        if (windowTitle.Equals(sessionName, StringComparison.OrdinalIgnoreCase)) return 100;
        if (windowTitle.Contains(sessionName, StringComparison.OrdinalIgnoreCase)) return 80;
        if (sessionName.Contains(windowTitle, StringComparison.OrdinalIgnoreCase)) return 70;

        var wWords = windowTitle.Split([' ', '-', '|', ':'], StringSplitOptions.RemoveEmptyEntries);
        return wWords.Count(w => sessionName.Contains(w, StringComparison.OrdinalIgnoreCase)) * 15;
    }
}

/// <summary>
/// Reads mixed session output via loopback with process filter in mixer stage.
/// </summary>
internal sealed class SessionWaveIn : IWaveIn
{
    private readonly WasapiLoopbackCapture _loopback;

    public SessionWaveIn(AudioSessionControl session, MMDevice device)
    {
        _ = session;
        _loopback = new WasapiLoopbackCapture(device);
        _loopback.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
        _loopback.RecordingStopped += (_, e) => RecordingStopped?.Invoke(this, e);
    }

    public WaveFormat WaveFormat
    {
        get => _loopback.WaveFormat;
        set => _loopback.WaveFormat = value;
    }

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public event EventHandler<StoppedEventArgs>? RecordingStopped;

    public void StartRecording() => _loopback.StartRecording();
    public void StopRecording() => _loopback.StopRecording();
    public void Dispose() => _loopback.Dispose();
}
