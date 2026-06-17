using System.Text.Json;
using Fragaria.Models;

namespace Fragaria.Services;

public sealed class SettingsService
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Fragaria");

    private static readonly string SettingsPath = Path.Combine(Dir, "settings.json");
    private static readonly string PresetsDir = Path.Combine(Dir, "presets");

    public AppSettings Load()
    {
        EnsureDir();
        if (!File.Exists(SettingsPath))
            return new AppSettings();
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        EnsureDir();
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public IReadOnlyList<string> ListPresets()
    {
        EnsureDir();
        if (!Directory.Exists(PresetsDir)) return ["default"];
        return Directory.GetFiles(PresetsDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n != null)
            .Cast<string>()
            .OrderBy(n => n)
            .DefaultIfEmpty("default")
            .ToList();
    }

    public MixerPreset LoadPreset(string name)
    {
        EnsureDir();
        var path = Path.Combine(PresetsDir, $"{name}.json");
        if (!File.Exists(path))
            return new MixerPreset { Name = name };
        return JsonSerializer.Deserialize<MixerPreset>(File.ReadAllText(path), JsonOptions)
               ?? new MixerPreset { Name = name };
    }

    public void SavePreset(MixerPreset preset)
    {
        EnsureDir();
        Directory.CreateDirectory(PresetsDir);
        var path = Path.Combine(PresetsDir, $"{preset.Name}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
    }

    public void EnsureBundledPresets(string? installPresetsDir)
    {
        EnsureDir();
        Directory.CreateDirectory(PresetsDir);
        if (string.IsNullOrEmpty(installPresetsDir) || !Directory.Exists(installPresetsDir)) return;
        foreach (var file in Directory.GetFiles(installPresetsDir, "*.json"))
        {
            var dest = Path.Combine(PresetsDir, Path.GetFileName(file));
            if (!File.Exists(dest))
                File.Copy(file, dest);
        }
    }

    private static void EnsureDir() => Directory.CreateDirectory(Dir);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
