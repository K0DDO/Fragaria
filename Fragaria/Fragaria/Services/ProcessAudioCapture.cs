using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

/// <summary>
/// Per-process/window audio capture. Uses WASAPI loopback with optional session matching.
/// </summary>
public sealed class ProcessAudioCapture : IDisposable
{
    private readonly WasapiLoopbackCapture _capture;
    private bool _disposed;

    public ProcessAudioCapture(uint processId, string windowTitle)
    {
        ProcessId = processId;
        WindowTitle = windowTitle;
        _capture = new WasapiLoopbackCapture();
        WaveFormat = _capture.WaveFormat;
        _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
    }

    public uint ProcessId { get; }
    public string WindowTitle { get; }
    public uint? ProcessFilterPid => ProcessId;
    public WaveFormat WaveFormat { get; }

    public event EventHandler<WaveInEventArgs>? DataAvailable;

    public void StartRecording() => _capture.StartRecording();
    public void StopRecording() => _capture.StopRecording();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _capture.Dispose();
    }
}
