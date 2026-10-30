using System.IO;
using System.Net.Http;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aetherworks.Models;

namespace Aetherworks.Services;

public sealed class GameInstallService
{
    public string RootPath { get; } = @"C:\AetherworksApps";
    public string SteamAppsPath => Path.Combine(RootPath, "steamapps");
    public string CommonPath => Path.Combine(SteamAppsPath, "common");
    public string DownloadingPath => Path.Combine(SteamAppsPath, "downloading");
    public string ManifestsPath => Path.Combine(SteamAppsPath, "appmanifest");
    public string UserDataPath => Path.Combine(RootPath, "userdata");
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string GetGamePath(GameManifest game)
    {
        EnsureLegacyMigration(game);
        return SafeDestination(game);
    }

    public async Task InstallAsync(GameManifest game, IProgress<double>? progress, CancellationToken token)
    {
        var destination = SafeDestination(game);
        Directory.CreateDirectory(CommonPath);
        Directory.CreateDirectory(DownloadingPath);
        Directory.CreateDirectory(ManifestsPath);
        Directory.CreateDirectory(UserDataPath);
        var staging = Path.Combine(DownloadingPath, ".staging-" + Guid.NewGuid().ToString("N"));
        var archive = Path.Combine(DownloadingPath, game.Depot + "-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            using (var response = await _http.GetAsync(game.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1;
                await using var input = await response.Content.ReadAsStreamAsync(token);
                var buffer = new byte[81920];
                long readTotal = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, token)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), token);
                    readTotal += read;
                    if (total > 0) progress?.Report(Math.Min(85, readTotal * 85d / total));
                }
            }

            Directory.CreateDirectory(staging);
            using (var zip = ZipFile.OpenRead(archive))
            {
                foreach (var entry in zip.Entries)
                {
                    token.ThrowIfCancellationRequested();
                    var target = Path.GetFullPath(Path.Combine(staging, entry.FullName));
                    var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && target != Path.GetFullPath(staging))
                        throw new InvalidDataException("The game archive contains an unsafe path.");
                    if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    await using var source = entry.Open();
                    await using var dest = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
                    await source.CopyToAsync(dest, token);
                }
            }

            if (!game.SourceOnly && !File.Exists(Path.Combine(staging, game.Executable)))
                throw new InvalidDataException($"The archive did not contain the expected executable: {game.Executable}");

            var backup = destination + ".old";
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try { Directory.Move(staging, destination); }
            catch { if (Directory.Exists(backup) && !Directory.Exists(destination)) Directory.Move(backup, destination); throw; }
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (!game.SourceOnly) CreateShortcut(game, Path.Combine(destination, game.Executable));
            WriteManifest(game);
            progress?.Report(100);
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (File.Exists(archive)) File.Delete(archive);
        }
    }

    public void Uninstall(GameManifest game)
    {
        var path = GetGamePath(game);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        var manifest = ManifestPath(game);
        if (File.Exists(manifest)) File.Delete(manifest);
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), game.Name + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
    }

    public bool IsInstalled(GameManifest game)
    {
        var path = GetGamePath(game);
        var present = game.SourceOnly
            ? Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any()
            : File.Exists(Path.Combine(path, game.Executable));
        if (present) WriteManifest(game);
        return present;
    }

    private string ManifestPath(GameManifest game) => Path.Combine(ManifestsPath, game.Depot + ".json");

    private void WriteManifest(GameManifest game)
    {
        Directory.CreateDirectory(ManifestsPath);
        var metadata = new
        {
            appId = game.Depot,
            gameId = game.Id,
            name = game.Name,
            version = game.Version,
            installDirectory = game.InstallFolder,
            sourceOnly = game.SourceOnly,
            installedAt = DateTimeOffset.UtcNow
        };
        File.WriteAllText(ManifestPath(game), JsonSerializer.Serialize(metadata, JsonOptions));
    }

    private void EnsureLegacyMigration(GameManifest game)
    {
        var current = Path.Combine(CommonPath, game.InstallFolder);
        var legacyCandidates = new List<string> { Path.Combine(RootPath, game.InstallFolder) };
        if (game.Id.Equals("wobble-works", StringComparison.OrdinalIgnoreCase))
            legacyCandidates.Add(Path.Combine(RootPath, "WobbleWorks-Source"));

        if (!Directory.Exists(current))
        {
            var legacy = legacyCandidates.FirstOrDefault(Directory.Exists);
            if (legacy is not null)
            {
                Directory.CreateDirectory(CommonPath);
                Directory.Move(legacy, current);
                WriteManifest(game);
            }
        }
    }

    private string SafeDestination(GameManifest game)
    {
        if (string.IsNullOrWhiteSpace(game.InstallFolder) || game.InstallFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || game.InstallFolder.Contains(".."))
            throw new InvalidDataException("Invalid game install folder.");
        var root = Path.GetFullPath(CommonPath) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(CommonPath, game.InstallFolder));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid install path.");
        return path;
    }

    private static void CreateShortcut(GameManifest game, string executable)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        Directory.CreateDirectory(desktop);
        var shortcutPath = Path.Combine(desktop, game.Name + ".lnk");
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic link = shell.CreateShortcut(shortcutPath);
            link.TargetPath = executable;
            link.WorkingDirectory = Path.GetDirectoryName(executable);
            link.Description = game.Name;
            link.Save();
            Marshal.FinalReleaseComObject(link);
        }
        finally { Marshal.FinalReleaseComObject(shell); }
    }
}
