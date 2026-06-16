using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

public sealed class MicrophoneCapture : IDisposable
{
    private WasapiCapture? _capture;
    private bool _disposed;

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public WaveFormat? WaveFormat => _capture?.WaveFormat;

    public void Start(string? deviceId = null)
    {
        Stop();
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;
        if (!string.IsNullOrEmpty(deviceId))
            device = enumerator.GetDevice(deviceId);
        else
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

        _capture = new WasapiCapture(device) { ShareMode = AudioClientShareMode.Shared };
        _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
        _capture.StartRecording();
    }

    public void Stop()
    {
        if (_capture == null) return;
        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

public sealed class BusOutput : IDisposable
{
    private WasapiOut? _output;
    private readonly BufferedWaveProvider? _buffer;
    private bool _disposed;

    public BusOutput(string name)
    {
        Name = name;
        _buffer = new BufferedWaveProvider(new WaveFormat(48000, 32, 2))
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
    }

    public string Name { get; }
    public WaveFormat Format => _buffer!.WaveFormat;

    public void Start(string? deviceId = null)
    {
        Stop();
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;
        if (!string.IsNullOrEmpty(deviceId))
            device = enumerator.GetDevice(deviceId);
        else
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _output = new WasapiOut(device, AudioClientShareMode.Shared, 50);
        _output.Init(_buffer!);
        _output.Play();
    }

    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (_buffer == null) return;
        _buffer.AddSamples(pcm.ToArray(), 0, pcm.Length);
    }

    public void Stop()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
