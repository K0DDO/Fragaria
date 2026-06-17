using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fragaria.Models;
using Fragaria.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace Fragaria.ViewModels;

public sealed partial class StripViewModel : ObservableObject
{
    private readonly AudioStrip _strip;
    private readonly Action _onChanged;

    public StripViewModel(AudioStrip strip, Action onChanged)
    {
        _strip = strip;
        _onChanged = onChanged;
        Title = strip.Title;
        ProcessName = strip.ProcessName;
        IsMicrophone = strip.Kind == StripKind.Microphone;
        HeadphonesPercent = strip.HeadphonesVolume * 100;
        StreamPercent = strip.StreamVolume * 100;
        HeadphonesLimitPercent = strip.HeadphonesLimit * 100;
        StreamLimitPercent = strip.StreamLimit * 100;
        Muted = strip.Muted;
        Duckable = strip.Duckable;
        RouteA1 = strip.RouteStream;
        RouteA2 = strip.RouteHeadphones;
        Solo = strip.Solo;
        EqLow = strip.Eq.LowDb;
        EqMid = strip.Eq.MidDb;
        EqHigh = strip.Eq.HighDb;
        CompThreshold = strip.Compressor.ThresholdDb;
        CompRatio = strip.Compressor.Ratio;
        CompLevel = Math.Clamp((-strip.Compressor.ThresholdDb + 40) / 40.0 * 100, 0, 100);
        GateLevel = 50;
        _ = LoadIconAsync(strip.IconPng);
    }

    public string Title { get; }
    public string ProcessName { get; }
    public bool IsMicrophone { get; }

    [ObservableProperty] private double _headphonesPercent;
    [ObservableProperty] private double _streamPercent;
    [ObservableProperty] private double _headphonesLimitPercent;
    [ObservableProperty] private double _streamLimitPercent;
    [ObservableProperty] private bool _muted;
    [ObservableProperty] private bool _duckable = true;
    [ObservableProperty] private bool _routeA1 = true;
    [ObservableProperty] private bool _routeA2 = true;
    [ObservableProperty] private bool _solo;
    [ObservableProperty] private double _gateLevel = 50;
    [ObservableProperty] private double _compLevel = 60;
    [ObservableProperty] private double _peakHp;
    [ObservableProperty] private double _peakStream;
    [ObservableProperty] private double _eqLow;
    [ObservableProperty] private double _eqMid;
    [ObservableProperty] private double _eqHigh;
    [ObservableProperty] private double _compThreshold = -12;
    [ObservableProperty] private double _compRatio = 4;
    [ObservableProperty] private BitmapImage? _icon;
    public float[] Spectrum { get; } = new float[32];

    partial void OnHeadphonesPercentChanged(double value) { _strip.HeadphonesVolume = (float)(value / 100); _onChanged(); }
    partial void OnStreamPercentChanged(double value) { _strip.StreamVolume = (float)(value / 100); _onChanged(); }
    partial void OnHeadphonesLimitPercentChanged(double value)
    {
        _strip.HeadphonesLimit = (float)(value / 100);
        if (_strip.HeadphonesVolume > _strip.HeadphonesLimit) HeadphonesPercent = value;
        _onChanged();
    }
    partial void OnStreamLimitPercentChanged(double value)
    {
        _strip.StreamLimit = (float)(value / 100);
        if (_strip.StreamVolume > _strip.StreamLimit) StreamPercent = value;
        _onChanged();
    }
    partial void OnMutedChanged(bool value) { _strip.Muted = value; _onChanged(); }
    partial void OnDuckableChanged(bool value) { _strip.Duckable = value; _onChanged(); }
    partial void OnRouteA1Changed(bool value) { _strip.RouteStream = value; _onChanged(); }
    partial void OnRouteA2Changed(bool value) { _strip.RouteHeadphones = value; _onChanged(); }
    partial void OnSoloChanged(bool value) { _strip.Solo = value; _onChanged(); }
    partial void OnGateLevelChanged(double value) => _onChanged();
    partial void OnEqLowChanged(double value) { _strip.Eq.LowDb = (float)value; _onChanged(); }
    partial void OnEqMidChanged(double value) { _strip.Eq.MidDb = (float)value; _onChanged(); }
    partial void OnEqHighChanged(double value) { _strip.Eq.HighDb = (float)value; _onChanged(); }
    partial void OnCompLevelChanged(double value)
    {
        _strip.Compressor.ThresholdDb = (float)(-40 + (100 - value) * 0.4);
        _onChanged();
    }
    partial void OnCompThresholdChanged(double value) { _strip.Compressor.ThresholdDb = (float)value; _onChanged(); }
    partial void OnCompRatioChanged(double value) { _strip.Compressor.Ratio = (float)value; _onChanged(); }

    public void UpdatePeaks(float hp, float st) { PeakHp = hp * 100; PeakStream = st * 100; }
    public void UpdateSpectrum(float[] bands) => Array.Copy(bands, Spectrum, Math.Min(bands.Length, 32));

    private async Task LoadIconAsync(byte[]? png)
    {
        if (png == null || png.Length == 0) return;
        try
        {
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(png.AsBuffer());
            stream.Seek(0);
            var img = new BitmapImage();
            await img.SetSourceAsync(stream);
            Icon = img;
        }
        catch { }
    }
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly MixerEngine _engine;
    private readonly SettingsService _settingsService = new();
    private readonly DispatcherQueue _dispatcher;

    public MainViewModel(MixerEngine engine, DispatcherQueue dispatcher)
    {
        _engine = engine;
        _dispatcher = dispatcher;
        Settings = _settingsService.Load();
        Presets = _settingsService.ListPresets().ToList();
        ActivePreset = Settings.ActivePreset;
        AutoStart = Settings.AutoStart;
        MinimizeToTray = Settings.MinimizeToTray;
        NoiseGateEnabled = Settings.NoiseGate.Enabled;
        NoiseGateThreshold = Settings.NoiseGate.Threshold * 100;
        DuckingEnabled = Settings.Ducking.Enabled;
        DuckingAmount = Settings.Ducking.Amount * 100;
        ObsEnabled = Settings.Obs.Enabled;
        ObsHost = Settings.Obs.Host;
        RecordHeadphones = Settings.Recording.RecordHeadphones;
        RecordStream = Settings.Recording.RecordStream;
        MasterMusic = _engine.Music.Volume * 100;

        _engine.StripsChanged += RefreshStrips;
        _engine.LevelsUpdated += OnLevelsUpdated;
        _engine.ObsSceneApplied += s => StatusMessage = $"Сцена: {s}";
    }

    public AppSettings Settings { get; }
    public ObservableCollection<StripViewModel> Strips { get; } = [];
    public List<string> Presets { get; private set; }
    public List<ScenePreset> Scenes => Settings.Scenes;

    [ObservableProperty] private string _activePreset = "default";
    [ObservableProperty] private double _masterHp = 100;
    [ObservableProperty] private double _masterStream = 100;
    [ObservableProperty] private double _masterHpLimit = 100;
    [ObservableProperty] private double _masterStreamLimit = 100;
    [ObservableProperty] private double _masterMusic = 70;
    [ObservableProperty] private double _masterHpPeak;
    [ObservableProperty] private double _masterStreamPeak;
    [ObservableProperty] private double _masterRecordPeak;
    [ObservableProperty] private double _masterMusicPeak;
    [ObservableProperty] private bool _autoStart;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _noiseGateEnabled;
    [ObservableProperty] private double _noiseGateThreshold = 2;
    [ObservableProperty] private bool _duckingEnabled = true;
    [ObservableProperty] private double _duckingAmount = 35;
    [ObservableProperty] private bool _obsEnabled;
    [ObservableProperty] private string _obsHost = "ws://127.0.0.1:4455";
    [ObservableProperty] private bool _obsConnected;
    [ObservableProperty] private bool _recordHeadphones;
    [ObservableProperty] private bool _recordStream;
    [ObservableProperty] private bool _isRecording;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private double _duckingGain = 100;
    [ObservableProperty] private bool _audioReady;
    public float[] HpSpectrum { get; } = new float[32];
    public float[] StreamSpectrum { get; } = new float[32];

    public void SeedFromInstallDir(string? installDir)
    {
        if (string.IsNullOrEmpty(installDir)) return;
        _settingsService.EnsureBundledPresets(Path.Combine(installDir, "presets"));
        Presets = _settingsService.ListPresets().ToList();
    }

    public void StartEngine()
    {
        _engine.Start(Settings);
        AudioReady = _engine.AudioReady;
        StatusMessage = _engine.StartError ?? (_engine.AudioReady ? "Аудио готово" : "Аудио: проверьте устройства в Settings");
    }

    public void RestartEngine()
    {
        _engine.Restart(Settings);
        AudioReady = _engine.AudioReady;
        StatusMessage = _engine.StartError ?? "Устройства обновлены";
    }

    public void RefreshStrips()
    {
        _dispatcher.TryEnqueue(() =>
        {
            Strips.Clear();
            Strips.Add(new StripViewModel(_engine.Microphone, OnMicStripChanged));
            foreach (var strip in _engine.WindowStrips)
                Strips.Add(new StripViewModel(strip, () => { }));
        });
    }

    private void OnMicStripChanged()
    {
        var vm = Strips.FirstOrDefault(s => s.IsMicrophone);
        if (vm == null) return;
        Settings.NoiseGate.Threshold = (float)Math.Clamp(vm.GateLevel / 500.0, 0.001, 0.15);
        SaveSettings();
    }

    private void OnLevelsUpdated()
    {
        _dispatcher.TryEnqueue(() =>
        {
            MasterHpPeak = _engine.Headphones.Peak * 100;
            MasterStreamPeak = _engine.Stream.Peak * 100;
            MasterRecordPeak = _engine.Record.Peak * 100;
            MasterMusicPeak = _engine.Music.Peak * 100;
            DuckingGain = _engine.DuckingGain * 100;
            Array.Copy(_engine.Headphones.Spectrum, HpSpectrum, 32);
            Array.Copy(_engine.Stream.Spectrum, StreamSpectrum, 32);
            IsRecording = _engine.IsRecording;

            var micVm = Strips.FirstOrDefault(s => s.IsMicrophone);
            micVm?.UpdatePeaks(_engine.Microphone.PeakHeadphones, _engine.Microphone.PeakStream);
            micVm?.UpdateSpectrum(_engine.Microphone.Spectrum);

            var windowStrips = _engine.WindowStrips.ToList();
            for (int i = 0; i < windowStrips.Count; i++)
            {
                var vm = Strips.Skip(1).ElementAtOrDefault(i);
                vm?.UpdatePeaks(windowStrips[i].PeakHeadphones, windowStrips[i].PeakStream);
                vm?.UpdateSpectrum(windowStrips[i].Spectrum);
            }
        });
    }

    partial void OnMasterHpChanged(double value) => _engine.Headphones.Volume = (float)(value / 100);
    partial void OnMasterStreamChanged(double value) => _engine.Stream.Volume = (float)(value / 100);
    partial void OnMasterHpLimitChanged(double value) => _engine.Headphones.Limit = (float)(value / 100);
    partial void OnMasterStreamLimitChanged(double value) => _engine.Stream.Limit = (float)(value / 100);
    partial void OnMasterMusicChanged(double value) => _engine.Music.Volume = (float)(value / 100);

    partial void OnNoiseGateEnabledChanged(bool value) { Settings.NoiseGate.Enabled = value; SaveSettings(); }
    partial void OnNoiseGateThresholdChanged(double value) { Settings.NoiseGate.Threshold = (float)(value / 100); SaveSettings(); }
    partial void OnDuckingEnabledChanged(bool value) { Settings.Ducking.Enabled = value; SaveSettings(); }
    partial void OnDuckingAmountChanged(double value) { Settings.Ducking.Amount = (float)(value / 100); SaveSettings(); }
    partial void OnObsEnabledChanged(bool value) { Settings.Obs.Enabled = value; SaveSettings(); }
    partial void OnRecordHeadphonesChanged(bool value) { Settings.Recording.RecordHeadphones = value; SaveSettings(); }
    partial void OnRecordStreamChanged(bool value) { Settings.Recording.RecordStream = value; SaveSettings(); }

    partial void OnAutoStartChanged(bool value)
    {
        AutoStartService.SetEnabled(value);
        Settings.AutoStart = value;
        SaveSettings();
    }

    public void CompleteSetup(string? hpId, string? streamId, string? micId)
    {
        Settings.HeadphonesDeviceId = hpId ?? "";
        Settings.StreamDeviceId = streamId ?? "";
        Settings.MicrophoneDeviceId = micId ?? "";
        Settings.SetupCompleted = true;
        SaveSettings();
    }

    [RelayCommand]
    private void LoadPreset()
    {
        var preset = _settingsService.LoadPreset(ActivePreset);
        _engine.ApplyPreset(preset);
        MasterHp = preset.MasterHeadphones * 100;
        MasterStream = preset.MasterStream * 100;
        MasterHpLimit = preset.MasterHeadphonesLimit * 100;
        MasterStreamLimit = preset.MasterStreamLimit * 100;
        RefreshStrips();
    }

    [RelayCommand]
    private void SavePreset()
    {
        var preset = _engine.CreatePreset(ActivePreset);
        _settingsService.SavePreset(preset);
        Settings.ActivePreset = ActivePreset;
        SaveSettings();
        Presets = _settingsService.ListPresets().ToList();
    }

    [RelayCommand]
    private void RescanWindows() => _engine.ScanWindows();

    [RelayCommand]
    private void ToggleRecording()
    {
        if (_engine.IsRecording) _engine.StopRecording();
        else _engine.StartRecording();
        IsRecording = _engine.IsRecording;
        StatusMessage = IsRecording ? "Запись..." : "Запись остановлена";
    }

    [RelayCommand]
    private void ApplyScene(ScenePreset scene)
    {
        scene.Mixer = _settingsService.LoadPreset(scene.Name);
        if (scene.Mixer.Strips.Count == 0)
            scene.Mixer = _engine.CreatePreset(scene.Name);
        _engine.ApplyScene(scene);
        ActivePreset = scene.Name;
        LoadPresetCommand.Execute(null);
    }

    public void AddWindow(nint hwnd)
    {
        _engine.AddWindowByHwnd(hwnd);
        RefreshStrips();
    }

    public IReadOnlyList<WindowInfo> GetWindows() => _engine.GetAvailableWindows();

    public void SaveSettings() => _settingsService.Save(Settings);
}
