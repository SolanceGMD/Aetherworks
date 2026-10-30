using System.IO;
using System.Text.Json;

namespace Aetherworks.Services;

public sealed class LauncherSettings
{
    public string CatalogueSource { get; set; } = "";
}

public sealed class LauncherSettingsService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Aetherworks", "settings.json");
    public LauncherSettings Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(_path)) ?? new() : new(); }
        catch { return new(); }
    }
    public void Save(LauncherSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}