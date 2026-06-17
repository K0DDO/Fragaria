using System.Collections.Concurrent;
using Fragaria.Models;
using Fragaria.Services.Dsp;
using NAudio.Wave;

namespace Fragaria.Services;

public sealed class StripChannel : IDisposable
{
    private ProcessAudioCapture? _capture;
    private readonly object _lock = new();
    public readonly StripDspChain Dsp = new();
    public readonly FftAnalyzer Analyzer = new();

    public AudioStrip Strip { get; }
    public bool IsActive { get; private set; }

    public StripChannel(AudioStrip strip) => Strip = strip;

    public void Start()
    {
        lock (_lock)
        {
            StopInternal();
            if (Strip.Kind == StripKind.Window && Strip.ProcessId > 0)
            {
                _capture = new ProcessAudioCapture(Strip.ProcessId, Strip.Title);
                _capture.DataAvailable += OnData;
                _capture.StartRecording();
            }
            IsActive = true;
        }
    }

    public void Stop()
    {
        lock (_lock) { StopInternal(); }
    }

    private void StopInternal()
    {
        if (_capture == null) return;
        _capture.DataAvailable -= OnData;
        _capture.StopRecording();
        _capture.Dispose();
        _capture = null;
        IsActive = false;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        LatestBuffer = e.Buffer;
        LatestCount = e.BytesRecorded;
    }

    public byte[]? LatestBuffer { get; private set; }
    public int LatestCount { get; private set; }

    public void Dispose() => Stop();
}

public sealed class MixerEngine : IDisposable
{
    private readonly WindowEnumerator _windows = new();
    private readonly MicrophoneCapture _mic = new();
    private readonly BusOutput _headphones = new("Headphones");
    private readonly BusOutput _stream = new("Stream");
    private readonly SoftLimiter _hpLimiter = new(0.98f);
    private readonly SoftLimiter _streamLimiter = new(0.95f);
    private readonly ConcurrentDictionary<string, StripChannel> _channels = new();
    private readonly System.Timers.Timer _scanTimer;
    private readonly object _mixLock = new();
    private readonly FftAnalyzer _hpFft = new();
    private readonly FftAnalyzer _streamFft = new();

    private NoiseGate _noiseGate = new(new NoiseGateSettings());
    private DuckingProcessor _ducking = new(new DuckingSettings());
    private DynamicsCompressor _hpBusComp = new(new CompressorSettings());
    private DynamicsCompressor _streamBusComp = new(new CompressorSettings());
    private BusRecorder? _recorder;
    private AppSettings _settings = new();
    private System.Timers.Timer? _mixTimer;
    private float[] _workBuffer = Array.Empty<float>();
    private float[] _hpMix = Array.Empty<float>();
    private float[] _streamMix = Array.Empty<float>();
    private bool _disposed;

    public MasterBus Headphones { get; } = new() { Name = "A2 Monitor" };
    public MasterBus Stream { get; } = new() { Name = "A1 Stream" };
    public MasterBus Record { get; } = new() { Name = "B1 Record" };
    public MasterBus Music { get; } = new() { Name = "B2 Music" };
    public AudioStrip Microphone { get; } = new()
    {
        Id = "mic",
        Kind = StripKind.Microphone,
        Title = "Микрофон",
        Duckable = false
    };

    public readonly StripDspChain MicDsp = new();
    public readonly FftAnalyzer MicAnalyzer = new();

    public event Action? StripsChanged;
    public event Action? LevelsUpdated;
    public event Action<string>? ObsSceneApplied;

    public float DuckingGain => _ducking.CurrentGain;
    public bool IsRecording => _recorder?.IsRecording ?? false;
    public bool AudioReady { get; private set; }
    public string? StartError { get; private set; }

    public IReadOnlyList<AudioStrip> WindowStrips =>
        _channels.Values.Select(c => c.Strip).OrderBy(s => s.Title).ToList();

    public MixerEngine()
    {
        _scanTimer = new System.Timers.Timer(1500) { AutoReset = true };
        _scanTimer.Elapsed += (_, _) => ScanWindows();
        _mic.DataAvailable += OnMicData;
    }

    public void Start(AppSettings settings)
    {
        _settings = settings;
        AudioReady = false;
        StartError = null;
        _mixTimer?.Stop();
        _mixTimer?.Dispose();
        _mixTimer = null;
        try
        {
            _noiseGate = new NoiseGate(settings.NoiseGate, settings.SampleRate);
            _ducking = new DuckingProcessor(settings.Ducking);
            _recorder = new BusRecorder(settings.Recording, settings.SampleRate);

            var vdm = new VirtualDriverManager(settings.VirtualDriver);
            var hpId = string.IsNullOrEmpty(settings.HeadphonesDeviceId) ? null : settings.HeadphonesDeviceId;
            var stId = string.IsNullOrEmpty(settings.StreamDeviceId) ? null : settings.StreamDeviceId;
            if (settings.VirtualDriver.UseFragariaDevices)
            {
                hpId ??= vdm.ResolveDeviceId(settings.VirtualDriver.HeadphonesDeviceName, NAudio.CoreAudioApi.DataFlow.Render);
                stId ??= vdm.ResolveDeviceId(settings.VirtualDriver.StreamDeviceName, NAudio.CoreAudioApi.DataFlow.Render);
            }

            _headphones.Start(hpId);
            _stream.Start(stId);
            _mic.Start(string.IsNullOrEmpty(settings.MicrophoneDeviceId) ? null : settings.MicrophoneDeviceId);
            _scanTimer.Start();
            ScanWindows();

            AudioReady = _headphones.IsRunning || _stream.IsRunning || _mic.IsRunning;

            var mixTimer = new System.Timers.Timer(Math.Max(10, settings.BufferMs / 4)) { AutoReset = true };
            mixTimer.Elapsed += (_, _) => MixFrame();
            mixTimer.Start();
            _mixTimer = mixTimer;
            AppLogger.Info($"MixerEngine started (audio ready: {AudioReady})");
        }
        catch (Exception ex)
        {
            StartError = ex.Message;
            AppLogger.Error("MixerEngine.Start failed", ex);
        }
    }

    public void Restart(AppSettings settings)
    {
        _mic.Stop();
        _headphones.Stop();
        _stream.Stop();
        Start(settings);
    }

    public void ScanWindows()
    {
        var windows = _windows.GetAudibleWindows();
        var activeIds = new HashSet<string>(_channels.Where(c => c.Value.Strip.Pinned).Select(c => c.Key));

        foreach (var w in windows)
        {
            var id = $"hwnd-{w.Hwnd}";
            activeIds.Add(id);
            if (_channels.ContainsKey(id))
            {
                _channels[id].Strip.Title = w.Title;
                continue;
            }
            AddWindowStrip(w);
        }

        foreach (var key in _channels.Keys.ToList())
        {
            if (!activeIds.Contains(key) && !_channels[key].Strip.Pinned)
            {
                _channels[key].Dispose();
                _channels.TryRemove(key, out _);
            }
        }
        StripsChanged?.Invoke();
    }

    public void AddWindowByHwnd(nint hwnd)
    {
        var windows = _windows.GetAudibleWindows();
        var w = windows.FirstOrDefault(x => x.Hwnd == hwnd);
        if (w == null) return;
        var id = $"hwnd-{w.Hwnd}";
        if (_channels.ContainsKey(id)) return;
        AddWindowStrip(w, pinned: true);
        StripsChanged?.Invoke();
    }

    private void AddWindowStrip(WindowInfo w, bool pinned = false)
    {
        var id = $"hwnd-{w.Hwnd}";
        var strip = new AudioStrip
        {
            Id = id,
            Kind = StripKind.Window,
            Hwnd = w.Hwnd,
            ProcessId = w.ProcessId,
            Title = w.Title,
            ProcessName = w.ProcessName,
            IconPng = w.IconPng,
            Pinned = pinned
        };
        var channel = new StripChannel(strip);
        _channels[id] = channel;
        channel.Start();
    }

    public IReadOnlyList<WindowInfo> GetAvailableWindows() => _windows.GetAudibleWindows();

    public void StartRecording()
    {
        var strips = new List<AudioStrip> { Microphone };
        strips.AddRange(WindowStrips);
        _recorder?.Start(strips);
    }
    public void StopRecording() => _recorder?.Stop();

    public void ApplyScene(ScenePreset scene)
    {
        ApplyPreset(scene.Mixer);
        ObsSceneApplied?.Invoke(scene.Name);
    }

    public void OnObsScene(string sceneName, ObsSettings obs)
    {
        if (!obs.Enabled) return;
        ObsSceneApplied?.Invoke(sceneName);

        if (obs.ScenePresetMap.TryGetValue(sceneName, out var presetName))
        {
            var preset = new SettingsService().LoadPreset(presetName);
            ApplyPreset(preset);
            ObsSceneApplied?.Invoke($"preset:{presetName}");
        }

        if (obs.MuteOnStreamScene && sceneName.Equals(obs.StreamSceneName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var ch in _channels.Values)
                if (ch.Strip.Duckable) ch.Strip.Muted = false;
        }
    }

    private byte[]? _micBuffer;
    private int _micCount;

    private void OnMicData(object? sender, WaveInEventArgs e)
    {
        _micBuffer = e.Buffer;
        _micCount = e.BytesRecorded;
    }

    private void MixFrame()
    {
        lock (_mixLock)
        {
            const int samples = 512 * 2;
            EnsureBuffers(samples);
            Array.Clear(_hpMix);
            Array.Clear(_streamMix);

            float micPeakRaw = 0;
            var anySolo = Microphone.Solo || _channels.Values.Any(c => c.Strip.Solo);

            if (_micBuffer != null && _micCount > 0 && (!anySolo || Microphone.Solo))
            {
                CopyToWork(_micBuffer, _micCount);
                _noiseGate.Process(_workBuffer);
                MicDsp.Process(_workBuffer, Microphone);
                micPeakRaw = AudioMath.ComputePeak(_workBuffer);
                MicAnalyzer.Analyze(_workBuffer);
                Array.Copy(MicAnalyzer.Bands, Microphone.Spectrum, 32);

                var hpGain = Microphone.EffectiveHeadphones * Headphones.Effective;
                var stGain = Microphone.EffectiveStream * Stream.Effective;
                AudioMath.MixAdd(_hpMix, _workBuffer, hpGain);
                AudioMath.MixAdd(_streamMix, _workBuffer, stGain);
                Microphone.PeakHeadphones = micPeakRaw * hpGain;
                Microphone.PeakStream = micPeakRaw * stGain;
            }

            _ducking.UpdateFromMicPeak(micPeakRaw);

            foreach (var ch in _channels.Values)
            {
                if (ch.LatestBuffer == null || ch.LatestCount == 0) continue;
                if (anySolo && !ch.Strip.Solo) continue;

                CopyToWork(ch.LatestBuffer, ch.LatestCount);
                ch.Dsp.Process(_workBuffer, ch.Strip);

                var duck = ch.Strip.Duckable ? _ducking.CurrentGain : 1f;
                var hpGain = ch.Strip.EffectiveHeadphones * Headphones.Effective * duck;
                var stGain = ch.Strip.EffectiveStream * Stream.Effective * duck;
                var musicGain = stGain * Music.Effective;

                AudioMath.MixAdd(_hpMix, _workBuffer, hpGain);
                AudioMath.MixAdd(_streamMix, _workBuffer, stGain);
                if (Music.Effective > 0)
                    AudioMath.MixAdd(_streamMix, _workBuffer, musicGain * 0.5f);

                if (_recorder?.IsRecording == true)
                    _recorder.WriteStrip(ch.Strip.Id, FloatBytes(_workBuffer));

                ch.Analyzer.Analyze(_workBuffer);
                Array.Copy(ch.Analyzer.Bands, ch.Strip.Spectrum, 32);
                ch.Strip.PeakHeadphones = AudioMath.ComputePeak(_workBuffer) * hpGain;
                ch.Strip.PeakStream = AudioMath.ComputePeak(_workBuffer) * stGain;
            }

            _hpBusComp.Process(_hpMix);
            _streamBusComp.Process(_streamMix);
            _hpLimiter.Process(_hpMix, _hpMix);
            _streamLimiter.Process(_streamMix, _streamMix);

            Headphones.Peak = AudioMath.ComputePeak(_hpMix);
            Stream.Peak = AudioMath.ComputePeak(_streamMix);
            Record.Peak = Stream.Peak;
            Music.Peak = Stream.Peak * Music.Effective;
            _hpFft.Analyze(_hpMix);
            _streamFft.Analyze(_streamMix);
            Array.Copy(_hpFft.Bands, Headphones.Spectrum, 32);
            Array.Copy(_streamFft.Bands, Stream.Spectrum, 32);

            var hpBytes = FloatBytes(_hpMix);
            var stBytes = FloatBytes(_streamMix);
            _headphones.Write(hpBytes);
            _stream.Write(stBytes);
            _recorder?.WriteHeadphones(hpBytes);
            _recorder?.WriteStream(stBytes);

            LevelsUpdated?.Invoke();
        }
    }

    private void EnsureBuffers(int samples)
    {
        if (_hpMix.Length < samples) _hpMix = new float[samples];
        if (_streamMix.Length < samples) _streamMix = new float[samples];
        if (_workBuffer.Length < samples) _workBuffer = new float[samples];
    }

    private void CopyToWork(byte[] buffer, int count)
    {
        var floats = BytesToFloats(buffer, count);
        floats.CopyTo(_workBuffer);
    }

    private static ReadOnlySpan<float> BytesToFloats(byte[] buffer, int count)
    {
        var samples = count / 4;
        var floats = new float[samples];
        Buffer.BlockCopy(buffer, 0, floats, 0, count);
        return floats;
    }

    private static byte[] FloatBytes(ReadOnlySpan<float> samples)
    {
        var arr = samples.ToArray();
        var bytes = new byte[arr.Length * 4];
        Buffer.BlockCopy(arr, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public MixerPreset CreatePreset(string name)
    {
        var preset = new MixerPreset
        {
            Name = name,
            MasterHeadphones = Headphones.Volume,
            MasterStream = Stream.Volume,
            MasterHeadphonesLimit = Headphones.Limit,
            MasterStreamLimit = Stream.Limit
        };
        preset.Strips.Add(StripToPreset(Microphone));
        foreach (var strip in WindowStrips)
            preset.Strips.Add(StripToPreset(strip));
        return preset;
    }

    private static StripPreset StripToPreset(AudioStrip s) => new()
    {
        Id = s.Id, Title = s.Title, Kind = s.Kind,
        HeadphonesVolume = s.HeadphonesVolume, StreamVolume = s.StreamVolume,
        HeadphonesLimit = s.HeadphonesLimit, StreamLimit = s.StreamLimit,
        Muted = s.Muted, Duckable = s.Duckable,
        Eq = new EqSettings { LowDb = s.Eq.LowDb, MidDb = s.Eq.MidDb, HighDb = s.Eq.HighDb },
        Compressor = s.Compressor
    };

    public void ApplyPreset(MixerPreset preset)
    {
        Headphones.Volume = preset.MasterHeadphones;
        Stream.Volume = preset.MasterStream;
        Headphones.Limit = preset.MasterHeadphonesLimit;
        Stream.Limit = preset.MasterStreamLimit;
        foreach (var sp in preset.Strips)
        {
            if (sp.Id == Microphone.Id) { ApplyToStrip(Microphone, sp); continue; }
            var ch = _channels.Values.FirstOrDefault(c => c.Strip.Id == sp.Id);
            if (ch != null) ApplyToStrip(ch.Strip, sp);
        }
    }

    private static void ApplyToStrip(AudioStrip strip, StripPreset sp)
    {
        strip.HeadphonesVolume = sp.HeadphonesVolume;
        strip.StreamVolume = sp.StreamVolume;
        strip.HeadphonesLimit = sp.HeadphonesLimit;
        strip.StreamLimit = sp.StreamLimit;
        strip.Muted = sp.Muted;
        strip.Duckable = sp.Duckable;
        strip.Eq.LowDb = sp.Eq.LowDb;
        strip.Eq.MidDb = sp.Eq.MidDb;
        strip.Eq.HighDb = sp.Eq.HighDb;
        strip.Compressor = sp.Compressor;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanTimer.Stop();
        _scanTimer.Dispose();
        _mixTimer?.Stop();
        _mixTimer?.Dispose();
        foreach (var ch in _channels.Values) ch.Dispose();
        _channels.Clear();
        _mic.Dispose();
        _headphones.Dispose();
        _stream.Dispose();
        _recorder?.Dispose();
    }
}
