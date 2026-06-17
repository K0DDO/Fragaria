using NAudio.Wave;

namespace Fragaria.Services;

public sealed class ProcessAudioCapture : IDisposable
{
    private readonly ProcessLoopbackCapture _capture;
    private bool _disposed;

    public ProcessAudioCapture(uint processId, string windowTitle)
    {
        ProcessId = processId;
        WindowTitle = windowTitle;
        _capture = new ProcessLoopbackCapture(processId, windowTitle);
        WaveFormat = _capture.WaveFormat;
        _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
    }

    public uint ProcessId { get; }
    public string WindowTitle { get; }
    public bool IsProcessIsolated => _capture.IsProcessIsolated;
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
