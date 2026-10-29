using System.Net.Http;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Aetherworks.Models;

namespace Aetherworks.Services;

public sealed class GameInstallService
{
    public string RootPath { get; } = @"C:\AetherworksApps";
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public async Task InstallAsync(GameManifest game, IProgress<double>? progress, CancellationToken token)
    {
        var destination = SafeDestination(game);
        Directory.CreateDirectory(RootPath);
        var staging = Path.Combine(RootPath, ".staging-" + Guid.NewGuid().ToString("N"));
        var archive = Path.Combine(Path.GetTempPath(), "aetherworks-" + Guid.NewGuid().ToString("N") + ".zip");
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

            if (!File.Exists(Path.Combine(staging, game.Executable)))
                throw new InvalidDataException($"The archive did not contain the expected executable: {game.Executable}");

            var backup = destination + ".old";
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            if (Directory.Exists(destination)) Directory.Move(destination, backup);
            try { Directory.Move(staging, destination); }
            catch { if (Directory.Exists(backup) && !Directory.Exists(destination)) Directory.Move(backup, destination); throw; }
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
            CreateShortcut(game, Path.Combine(destination, game.Executable));
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
        var path = SafeDestination(game);
        if (Directory.Exists(path)) Directory.Delete(path, true);
        var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), game.Name + ".lnk");
        if (File.Exists(shortcut)) File.Delete(shortcut);
    }

    public bool IsInstalled(GameManifest game) => File.Exists(Path.Combine(SafeDestination(game), game.Executable));

    private string SafeDestination(GameManifest game)
    {
        if (string.IsNullOrWhiteSpace(game.InstallFolder) || game.InstallFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || game.InstallFolder.Contains(".."))
            throw new InvalidDataException("Invalid game install folder.");
        var root = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(RootPath, game.InstallFolder));
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