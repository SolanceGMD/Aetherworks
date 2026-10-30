namespace Aetherworks.Models;

public sealed class GameManifest
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public int Depot { get; set; }
    public string DownloadUrl { get; set; } = "";
    public string Executable { get; set; } = "";
    public string InstallFolder { get; set; } = "";
    public string? ArtworkUrl { get; set; }
    public bool SourceOnly { get; set; }
}