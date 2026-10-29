# Aetherworks

A Windows game launcher and catalogue built with C# and WPF on .NET 10.

## Build
Requires the .NET 10 SDK and Windows Desktop targeting pack.

```powershell
dotnet restore Aetherworks/Aetherworks.csproj
dotnet build Aetherworks/Aetherworks.csproj -c Release
dotnet publish Aetherworks/Aetherworks.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Game catalogue
Edit `Aetherworks/Data/games.json` and add entries matching this schema:

```json
[
  {
    "id": "my-first-game",
    "name": "My First Game",
    "description": "A short game description.",
    "version": "1.0.0",
    "depot": 1,
    "downloadUrl": "https://github.com/OWNER/REPOSITORY/releases/download/v1.0/Game.zip",
    "executable": "MyFirstGame.exe",
    "installFolder": "MyFirstGame"
  }
]
```

The download URL must be an HTTPS URL to a ZIP depot. The installer downloads the complete archive, extracts it into a staging folder, rejects archive path traversal, verifies the executable, then swaps the staged files into place.

## Game install location
The initial implementation uses a per-machine data directory under `%ProgramData%\Aetherworks\Games` to avoid silently assuming that all users can write to the root of C:. The requested `C:\AetherworksApps` destination will be made configurable in the settings milestone.

## CI and releases
GitHub Actions builds on pushes to main/master, pull requests, and manual dispatch. It uploads a self-contained Windows x64 build artifact. It deliberately does **not** create GitHub Releases or publish installers on tag pushes; releases remain manual.

## Current scope
The repository currently contains the WPF launcher shell, JSON catalogue loading and validation, ZIP depot installation with staging and executable verification, desktop shortcuts, game launch, and game uninstall. Windows installer packaging, configurable paths, pause/resume, update comparison, and comprehensive automated tests remain release blockers and are tracked for subsequent implementation.
