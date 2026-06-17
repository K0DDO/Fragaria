using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Fragaria.Services;

public sealed class MicrophoneCapture : IDisposable
{
    private WasapiCapture? _capture;
    private bool _disposed;

    public event EventHandler<WaveInEventArgs>? DataAvailable;
    public WaveFormat? WaveFormat => _capture?.WaveFormat;
    public bool IsRunning => _capture != null;

    public void Start(string? deviceId = null)
    {
        Stop();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            MMDevice device;
            if (!string.IsNullOrEmpty(deviceId))
                device = enumerator.GetDevice(deviceId);
            else
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);

            _capture = new WasapiCapture(device) { ShareMode = AudioClientShareMode.Shared };
            _capture.DataAvailable += (_, e) => DataAvailable?.Invoke(this, e);
            _capture.StartRecording();
            AppLogger.Info($"Microphone started: {device.FriendlyName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Microphone start failed", ex);
            Stop();
        }
    }

    public void Stop()
    {
        if (_capture == null) return;
        try
        {
            _capture.StopRecording();
            _capture.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Microphone stop failed", ex);
        }
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

    public BusOutput(string name, int sampleRate = 48000)
    {
        Name = name;
        _buffer = new BufferedWaveProvider(new WaveFormat(sampleRate, 32, 2))
        {
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
    }

    public string Name { get; }
    public WaveFormat Format => _buffer!.WaveFormat;
    public bool IsRunning => _output != null;

    public void Start(string? deviceId = null)
    {
        Stop();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            MMDevice device;
            if (!string.IsNullOrEmpty(deviceId))
                device = enumerator.GetDevice(deviceId);
            else
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _output = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);
            _output.Init(_buffer!);
            _output.Play();
            AppLogger.Info($"Bus '{Name}' started: {device.FriendlyName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Bus '{Name}' start failed", ex);
            Stop();
        }
    }

    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (_output == null || pcm.IsEmpty) return;
        _buffer!.AddSamples(pcm.ToArray(), 0, pcm.Length);
    }

    public void Stop()
    {
        if (_output == null) return;
        try
        {
            _output.Stop();
            _output.Dispose();
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Bus '{Name}' stop failed", ex);
        }
        _output = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
