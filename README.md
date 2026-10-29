# Aetherworks

A Windows game launcher and catalogue built with C# and WPF on .NET 10.

## Features
- Store, installed-game library, downloads activity, command console and settings pages.
- Google OpenID Connect sign-in using an OAuth 2.0 Desktop client and PKCE. Google passwords are entered only on Google's website.
- Depot commands: `sudo fetch depot 1 = [install]` and `sudo fetch depot 1 = [delete]`.
- HTTPS ZIP downloads, staged extraction, archive path validation, executable validation and desktop shortcuts.
- Launcher installs to `C:\Program Files (x86)\Aetherworks`; games install separately to `C:\AetherworksApps\<GameName>`.

## Google sign-in setup
Create an OAuth Client ID of type **Desktop app** in Google Cloud Console, configure the OAuth consent screen, and paste the client ID into Settings. The launcher uses authorization code + PKCE and validates OAuth state and the verified email. Game installs, launches and depot management require a successful Google sign-in. Sign-in is session-only; persistent account sessions, cloud saves and cross-device accounts are not yet implemented.

## Catalogue / depots
Configure a local JSON file path or HTTPS URL in Settings. Example:

```json
[
  {
    "id": "sample-game",
    "name": "Sample Game",
    "description": "Example depot",
    "version": "1.0.0",
    "depot": 1,
    "downloadUrl": "https://github.com/OWNER/REPOSITORY/releases/download/v1.0/Game.zip",
    "executable": "Game.exe",
    "installFolder": "SampleGame"
  }
]
```

Depot numbers must be unique positive integers. The console maps the number to a catalogue entry. The supported syntax is `sudo fetch depot 1 = [install]` and `sudo fetch depot 1 = [delete]`; deletion requires confirmation.

A GitHub Actions artifact page URL (such as an `actions/runs/.../artifacts/...` link) is not a stable direct depot URL and expires according to the workflow's artifact retention policy. The build artifact contains the launcher and installer, not a game depot. For the test, use a direct ZIP URL and configure its executable path to match the ZIP contents. For real game depots, publish a ZIP as a GitHub Release asset or host it at a stable HTTPS URL.

## Build
Requires the .NET 10 SDK and Windows Desktop targeting pack.

```powershell
dotnet restore Aetherworks/Aetherworks.csproj
dotnet build Aetherworks/Aetherworks.csproj -c Release
dotnet publish Aetherworks/Aetherworks.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true --output publish
```

The Inno Setup installer packages the launcher. Uninstalling the launcher intentionally leaves installed games untouched.

## Publisher / signing
The installer displays **Atherworks** as publisher metadata. This is not an Authenticode signature; a trusted Windows signature requires a valid code-signing certificate.

## CI / releases
GitHub Actions builds artifacts on pushes, pull requests and manual dispatch. It does not automatically publish GitHub Releases.
