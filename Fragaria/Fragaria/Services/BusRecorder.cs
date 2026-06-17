using Fragaria.Models;
using NAudio.Wave;

namespace Fragaria.Services;

public sealed class BusRecorder : IDisposable
{
    private WaveFileWriter? _hpWriter;
    private WaveFileWriter? _streamWriter;
    private readonly Dictionary<string, WaveFileWriter> _stripWriters = new();
    private readonly RecordingSettings _settings;
    private readonly WaveFormat _format;
    private bool _recording;
    private string? _sessionFolder;

    public BusRecorder(RecordingSettings settings, int sampleRate = 48000)
    {
        _settings = settings;
        _format = new WaveFormat(sampleRate, 32, 2);
    }

    public bool IsRecording => _recording;

    public void Start(IEnumerable<AudioStrip>? strips = null)
    {
        Stop();
        var folder = string.IsNullOrEmpty(_settings.OutputFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Fragaria")
            : _settings.OutputFolder;
        Directory.CreateDirectory(folder);

        var ts = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _sessionFolder = Path.Combine(folder, ts);
        Directory.CreateDirectory(_sessionFolder);

        if (_settings.RecordHeadphones)
            _hpWriter = new WaveFileWriter(Path.Combine(_sessionFolder, "A2_Monitor.wav"), _format);
        if (_settings.RecordStream)
            _streamWriter = new WaveFileWriter(Path.Combine(_sessionFolder, "A1_Stream.wav"), _format);

        if (_settings.RecordStrips && strips != null)
        {
            foreach (var strip in strips)
            {
                var safe = string.Join("_", strip.Title.Split(Path.GetInvalidFileNameChars())).Trim();
                if (string.IsNullOrWhiteSpace(safe)) safe = strip.Id;
                var path = Path.Combine(_sessionFolder, $"{safe}.wav");
                _stripWriters[strip.Id] = new WaveFileWriter(path, _format);
            }
        }

        _recording = _hpWriter != null || _streamWriter != null || _stripWriters.Count > 0;
        AppLogger.Info($"Recording started: {_sessionFolder}");
    }

    public void WriteHeadphones(byte[] pcm) => _hpWriter?.Write(pcm, 0, pcm.Length);
    public void WriteStream(byte[] pcm) => _streamWriter?.Write(pcm, 0, pcm.Length);

    public void WriteStrip(string stripId, byte[] pcm)
    {
        if (_stripWriters.TryGetValue(stripId, out var w))
            w.Write(pcm, 0, pcm.Length);
    }

    public void Stop()
    {
        _hpWriter?.Dispose(); _hpWriter = null;
        _streamWriter?.Dispose(); _streamWriter = null;
        foreach (var w in _stripWriters.Values) w.Dispose();
        _stripWriters.Clear();
        _recording = false;
    }

    public void Dispose() => Stop();
}
