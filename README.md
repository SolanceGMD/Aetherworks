# Aetherworks

A Windows game launcher and catalogue built with C# and WPF on .NET 10.

## Features
- Store, installed-game library, downloads activity, command console and settings pages.
- No account or sign-in required.
- Depot commands: `sudo fetch depot 2 = [install]` and `sudo fetch depot 2 = [delete]`.
- HTTPS ZIP downloads, staged extraction and archive path validation.
- Source-only projects can be downloaded into the Library without requiring a game executable.
- Launcher installs to `C:\Program Files (x86)\Aetherworks`; games and source projects install separately to `C:\AetherworksApps\<GameName>`.

## Featured depot
**Wobble Works** is listed as in development. A playable build is not available yet; depot 2 downloads its source archive:

`https://github.com/SolanceGMD/Aetherworks-Depots/releases/download/depot2/Wobble.Works.zip`

Use the Store's **Download Source** button or enter:

```text
sudo fetch depot 2 = [install]
```

To remove its downloaded source files:

```text
sudo fetch depot 2 = [delete]
```

The downloaded source is placed under `C:\AetherworksApps\WobbleWorks-Source`. It is source code, not a playable game build.

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

Depot deletion requires confirmation. The launcher uninstaller intentionally leaves downloaded games and source projects untouched.

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
