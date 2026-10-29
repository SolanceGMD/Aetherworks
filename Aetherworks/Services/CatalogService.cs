using System.Text.Json;
using Aetherworks.Models;

namespace Aetherworks.Services;

public sealed class CatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public async Task<IReadOnlyList<GameManifest>> LoadAsync(string source, CancellationToken cancellationToken = default)
    {
        string json;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            json = await _http.GetStringAsync(uri, cancellationToken);
        else
            json = await File.ReadAllTextAsync(source, cancellationToken);

        var games = JsonSerializer.Deserialize<List<GameManifest>>(json, JsonOptions) ?? [];
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in games)
        {
            if (string.IsNullOrWhiteSpace(game.Id) || !ids.Add(game.Id))
                throw new InvalidDataException("Catalogue contains a missing or duplicate game id.");
            if (string.IsNullOrWhiteSpace(game.Name) || string.IsNullOrWhiteSpace(game.Executable) ||
                string.IsNullOrWhiteSpace(game.InstallFolder) || !Uri.TryCreate(game.DownloadUrl, UriKind.Absolute, out var download) ||
                download.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException($"Game '{game.Id}' has incomplete or unsafe manifest data.");
            if (Path.GetFileName(game.Executable) != game.Executable ||
                game.InstallFolder.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                game.InstallFolder.Contains("..", StringComparison.Ordinal))
                throw new InvalidDataException($"Game '{game.Id}' contains an unsafe installation path.");
        }
        return games;
    }
}