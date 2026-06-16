using Fragaria.Models;
using NAudio.Wave;

namespace Fragaria.Services;

public sealed class BusRecorder : IDisposable
{
    private WaveFileWriter? _hpWriter;
    private WaveFileWriter? _streamWriter;
    private readonly RecordingSettings _settings;
    private bool _recording;

    public BusRecorder(RecordingSettings settings) => _settings = settings;

    public bool IsRecording => _recording;

    public void Start()
    {
        Stop();
        var folder = string.IsNullOrEmpty(_settings.OutputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Fragaria")
            : _settings.OutputFolder;
        Directory.CreateDirectory(folder);

        var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var format = new WaveFormat(48000, 32, 2);

        if (_settings.RecordHeadphones)
            _hpWriter = new WaveFileWriter(Path.Combine(folder, $"Fragaria_A_{ts}.wav"), format);
        if (_settings.RecordStream)
            _streamWriter = new WaveFileWriter(Path.Combine(folder, $"Fragaria_B_{ts}.wav"), format);

        _recording = _hpWriter != null || _streamWriter != null;
    }

    public void WriteHeadphones(byte[] pcm) => _hpWriter?.Write(pcm, 0, pcm.Length);
    public void WriteStream(byte[] pcm) => _streamWriter?.Write(pcm, 0, pcm.Length);

    public void Stop()
    {
        _hpWriter?.Dispose(); _hpWriter = null;
        _streamWriter?.Dispose(); _streamWriter = null;
        _recording = false;
    }

    public void Dispose() => Stop();
}
