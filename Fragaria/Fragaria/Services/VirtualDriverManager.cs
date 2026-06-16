using Fragaria.Models;
using NAudio.CoreAudioApi;

namespace Fragaria.Services;

/// <summary>
/// Manages Fragaria virtual audio devices.
/// Ships setup for VB-Audio/Cable or open-source virtual driver until native driver lands.
/// </summary>
public sealed class VirtualDriverManager
{
    public VirtualDriverSettings Settings { get; }

    public VirtualDriverManager(VirtualDriverSettings settings) => Settings = settings;

    public DriverStatus CheckStatus()
    {
        var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
        var names = devices.Select(d => d.FriendlyName).ToList();

        var hasInput = names.Any(n => n.Contains("Fragaria", StringComparison.OrdinalIgnoreCase) ||
                                      n.Contains("CABLE Output", StringComparison.OrdinalIgnoreCase));
        var hasOutA = names.Any(n => n.Contains(Settings.HeadphonesDeviceName, StringComparison.OrdinalIgnoreCase) ||
                                     n.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase));
        var hasOutB = names.Any(n => n.Contains(Settings.StreamDeviceName, StringComparison.OrdinalIgnoreCase));

        Settings.DriverInstalled = hasInput && hasOutA;
        return new DriverStatus(hasInput, hasOutA, hasOutB, names);
    }

    public string GetSetupInstructions() =>
        """
        ## Установка виртуальных устройств Fragaria

        1. Скачайте [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) (бесплатно)
        2. Установите и перезагрузите ПК
        3. В Fragaria → Настройки:
           - Шина A → ваши наушники (или CABLE A)
           - Шина B → CABLE Input (для OBS)
           - OBS/Discord → CABLE Output как микрофон

        ### Будущее: нативный драйвер Fragaria
        В разработке собственный драйвер с именами:
        - Fragaria Input (1 вход)
        - Fragaria Output A / B (2 выхода)

        Скрипт установки: `driver/install-fragaria-driver.ps1`
        """;

    public string? ResolveDeviceId(string preferredName, DataFlow flow)
    {
        if (string.IsNullOrEmpty(preferredName)) return null;
        var enumerator = new MMDeviceEnumerator();
        foreach (var d in enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            if (d.FriendlyName.Contains(preferredName, StringComparison.OrdinalIgnoreCase))
                return d.ID;
        }
        return null;
    }
}

public sealed record DriverStatus(bool HasInput, bool HasOutputA, bool HasOutputB, IReadOnlyList<string> AllDevices);
