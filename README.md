# Aetherworks

A Windows game launcher and catalogue built with C# and WPF on .NET 10.

## Features
- Store, per-game detail pages, installed-game Library, downloads activity, command console and settings.
- No account or sign-in required.
- Steam-style library layout under `C:\AetherworksApps\steamapps`.
- ZIP downloads are staged in `steamapps\downloading`, then extracted into `steamapps\common\<GameName>`.
- Per-depot JSON manifests are stored separately in `steamapps\appmanifest`; user data has its own `userdata` directory.
- Safe ZIP extraction, install validation, progress reporting and game removal.
- Source-only projects can be downloaded without requiring a game executable. If the installed source contains a Godot `project.godot`, Play launches it using a detected Godot executable or lets the user locate Godot.

## Featured depot
**Wobble Works** is listed as in development. Depot 2 currently downloads its source/data archive:

`https://github.com/SolanceGMD/Aetherworks-Depots/releases/download/depot2/Wobble.Works.zip`

Use the Store's **Download Source** button or enter:

```text
sudo fetch depot 2 = [install]
```

To remove its downloaded source files:

```text
sudo fetch depot 2 = [delete]
```

The source is installed to `C:\AetherworksApps\steamapps\common\Wobble Works`. The current depot does not include a Godot `project.godot` file, so it is not launchable yet; Play will report this until the depot contains a complete Godot project.

## Catalogue / depots
Configure a local JSON file path or HTTPS URL in Settings. Each catalogue item has a unique positive depot number. Normal playable games provide an executable path; source-only items set `"sourceOnly": true` and do not require an executable.

Example source-only entry:

```json
{
  "id": "example-source",
  "name": "Example Project",
  "description": "Source download only",
  "version": "In Development",
  "depot": 3,
  "downloadUrl": "https://github.com/OWNER/REPOSITORY/releases/download/v1/Source.zip",
  "executable": "",
  "installFolder": "Example-Source",
  "sourceOnly": true
}
```

Depot deletion requires confirmation. Uninstalling Aetherworks intentionally leaves downloaded games and source projects untouched.

## Build
Requires the .NET 10 SDK and Windows Desktop targeting pack.

```powershell
dotnet restore Aetherworks/Aetherworks.csproj
dotnet build Aetherworks/Aetherworks.csproj -c Release
dotnet publish Aetherworks/Aetherworks.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --output publish
```

## Publisher / signing
The installer displays **Atherworks** as publisher metadata. This is not an Authenticode signature; a trusted Windows signature requires a valid code-signing certificate.

## CI / releases
GitHub Actions builds artifacts on pushes, pull requests and manual dispatch. It does not automatically publish GitHub Releases.
