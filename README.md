# Aetherworks

A Windows game launcher and catalogue built with C# and WPF on .NET 10.

## Build

Requires the .NET 10 SDK and Windows Desktop targeting pack.

```powershell
dotnet restore Aetherworks/Aetherworks.csproj
dotnet build Aetherworks/Aetherworks.csproj -c Release
dotnet publish Aetherworks/Aetherworks.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --output publish
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

The download URL must be HTTPS and point to a ZIP depot. The launcher downloads the archive, extracts it into a temporary staging directory, rejects unsafe paths and symbolic links, verifies the configured executable, and then swaps the staged game into place. Existing game files are restored if the final swap or shortcut creation fails.

## Install locations

- Launcher: `C:\Program Files (x86)\Aetherworks` (32-bit Program Files location on 64-bit Windows).
- Games: `C:\AetherworksApps\<GameName>`.

The launcher installer is separate from game installations. Uninstalling the launcher is designed to leave the game library under `C:\AetherworksApps` untouched.

## Installer identity and signing

The installer displays **Atherworks** as its publisher metadata, as requested. This display value is not a cryptographic Authenticode signature. A trusted Windows signature requires a valid code-signing certificate and secure certificate handling in GitHub Actions; no certificate is embedded in this repository.

## CI and releases

GitHub Actions builds the Windows x64 launcher and packages an Inno Setup installer on pushes to `main`/`master`, pull requests, and manual dispatch. Build artifacts are uploaded to the workflow run. It deliberately does **not** create GitHub Releases or publish installers on tag pushes; releases remain manual.

## Current implementation status

Implemented: WPF launcher shell, JSON catalogue loading/validation, ZIP depot download and staged extraction, executable verification, desktop shortcuts, game launch, game uninstall, and CI packaging.

Still required before calling this a complete release: pause/resume downloads, a real downloads/progress management view, game version/update comparison and update flow, settings and catalogue-source configuration, artwork-rich store/library presentation, automated tests, and a verified Windows CI run. These are not claimed as complete yet.
