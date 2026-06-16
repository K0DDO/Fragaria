namespace Fragaria.Models;

public enum StripKind
{
    Window,
    Microphone,
    System
}

public sealed class EqSettings
{
    public float LowDb { get; set; }
    public float MidDb { get; set; }
    public float HighDb { get; set; }
}

public sealed class CompressorSettings
{
    public bool Enabled { get; set; } = true;
    public float ThresholdDb { get; set; } = -12f;
    public float Ratio { get; set; } = 4f;
    public float AttackMs { get; set; } = 10f;
    public float ReleaseMs { get; set; } = 100f;
    public float MakeupDb { get; set; }
}

public sealed class AudioStrip
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public StripKind Kind { get; init; }
    public nint Hwnd { get; init; }
    public uint ProcessId { get; init; }
    public string Title { get; set; } = "Unknown";
    public string ProcessName { get; set; } = "";
    public byte[]? IconPng { get; set; }
    public bool Pinned { get; set; }

    public float HeadphonesVolume { get; set; } = 1f;
    public float StreamVolume { get; set; } = 1f;
    public float HeadphonesLimit { get; set; } = 1f;
    public float StreamLimit { get; set; } = 1f;
    public bool Muted { get; set; }
    public bool Solo { get; set; }
    public bool Duckable { get; set; } = true;
    public bool RouteStream { get; set; } = true;
    public bool RouteHeadphones { get; set; } = true;

    public EqSettings Eq { get; set; } = new();
    public CompressorSettings Compressor { get; set; } = new();

    public float PeakHeadphones { get; set; }
    public float PeakStream { get; set; }
    public float[] Spectrum { get; set; } = new float[32];

    public float EffectiveHeadphones =>
        Muted || !RouteHeadphones ? 0f : Math.Min(HeadphonesVolume, HeadphonesLimit);

    public float EffectiveStream =>
        Muted || !RouteStream ? 0f : Math.Min(StreamVolume, StreamLimit);
}

public sealed class MasterBus
{
    public string Name { get; init; } = "";
    public float Volume { get; set; } = 1f;
    public float Limit { get; set; } = 1f;
    public bool Muted { get; set; }
    public float Peak { get; set; }
    public float[] Spectrum { get; set; } = new float[32];
    public CompressorSettings Compressor { get; set; } = new();

    public float Effective => Muted ? 0f : Math.Min(Volume, Limit);
}

public sealed class DuckingSettings
{
    public bool Enabled { get; set; } = true;
    public float Threshold { get; set; } = 0.03f;
    public float Amount { get; set; } = 0.35f;
    public float AttackMs { get; set; } = 30f;
    public float ReleaseMs { get; set; } = 300f;
}

public sealed class NoiseGateSettings
{
    public bool Enabled { get; set; } = true;
    public float Threshold { get; set; } = 0.02f;
    public float AttackMs { get; set; } = 5f;
    public float ReleaseMs { get; set; } = 80f;
    public float Floor { get; set; } = 0f;
}

public sealed class ScenePreset
{
    public string Name { get; set; } = "Игра";
    public string Hotkey { get; set; } = "";
    public int HotkeyModifiers { get; set; }
    public int HotkeyKey { get; set; }
    public MixerPreset Mixer { get; set; } = new();
}

public sealed class MixerPreset
{
    public string Name { get; set; } = "default";
    public float MasterHeadphones { get; set; } = 1f;
    public float MasterStream { get; set; } = 1f;
    public float MasterHeadphonesLimit { get; set; } = 1f;
    public float MasterStreamLimit { get; set; } = 1f;
    public List<StripPreset> Strips { get; set; } = [];
}

public sealed class StripPreset
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public StripKind Kind { get; set; }
    public float HeadphonesVolume { get; set; } = 1f;
    public float StreamVolume { get; set; } = 1f;
    public float HeadphonesLimit { get; set; } = 1f;
    public float StreamLimit { get; set; } = 1f;
    public bool Muted { get; set; }
    public bool Duckable { get; set; } = true;
    public EqSettings Eq { get; set; } = new();
    public CompressorSettings Compressor { get; set; } = new();
}

public sealed class ObsSettings
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "ws://127.0.0.1:4455";
    public string Password { get; set; } = "";
    public bool MuteOnStreamScene { get; set; } = true;
    public string StreamSceneName { get; set; } = "Live";
}

public sealed class RecordingSettings
{
    public bool RecordHeadphones { get; set; }
    public bool RecordStream { get; set; }
    public string OutputFolder { get; set; } = "";
}

public sealed class VirtualDriverSettings
{
    public bool UseFragariaDevices { get; set; } = true;
    public string HeadphonesDeviceName { get; set; } = "Fragaria Output A";
    public string StreamDeviceName { get; set; } = "Fragaria Output B";
    public string InputDeviceName { get; set; } = "Fragaria Input";
    public bool DriverInstalled { get; set; }
}

public sealed class AppSettings
{
    public string HeadphonesDeviceId { get; set; } = "";
    public string StreamDeviceId { get; set; } = "";
    public string MicrophoneDeviceId { get; set; } = "";
    public bool AutoStart { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string ActivePreset { get; set; } = "default";
    public NoiseGateSettings NoiseGate { get; set; } = new();
    public DuckingSettings Ducking { get; set; } = new();
    public ObsSettings Obs { get; set; } = new();
    public RecordingSettings Recording { get; set; } = new();
    public VirtualDriverSettings VirtualDriver { get; set; } = new();
    public List<ScenePreset> Scenes { get; set; } =
    [
        new() { Name = "Игра", Hotkey = "Ctrl+1", HotkeyModifiers = 0x0002, HotkeyKey = 0x31 },
        new() { Name = "Разговор", Hotkey = "Ctrl+2", HotkeyModifiers = 0x0002, HotkeyKey = 0x32 },
        new() { Name = "Стрим", Hotkey = "Ctrl+3", HotkeyModifiers = 0x0002, HotkeyKey = 0x33 }
    ];
}
