---
layout: default
title: Help
---

# Help

Bugs, ideas, and questions go through GitHub issue forms. Do not dump a blank issue.

[Open an issue](https://github.com/binx-ux/Music-engine-/issues/new/choose)

## Security

If you found a real hole (token leak, arbitrary file write, anything that hurts users), do not open a public issue. Use [GitHub Security Advisories](https://github.com/binx-ux/Music-engine-/security/advisories/new). Say the version and how to reproduce it. Do not attach recorded mic audio.

## Versions

Cuebox versions look like `1.2.33`.

- **major**: first number goes up, the rest reset to 0 (`1.2.33` -> `2.0.0`)
- **minor**: second number goes up, the rest stay (`1.2.33` -> `1.3.33`)
- **fix**: last two numbers go up (`1.2.33` -> `1.2.34`)

Current app release is **1.3.1**.

Cuebox checks GitHub for a new installer when it starts. You can skip minor updates and fixes. Major updates still show the next time you open the app.

## Build it

```
dotnet restore Mixline.sln
dotnet build Mixline.sln -c Release
dotnet test Mixline.sln -c Release
dotnet publish src/UI/Mixline.App.csproj -c Release -r win-x64 --self-contained true
```

Exe name is `Cuebox.exe`. Audio code stays out of `src/UI`. No game injection, DLL hooks, or Spotify DRM workarounds.

More in the repo: [ARCHITECTURE.md](https://github.com/binx-ux/Music-engine-/blob/master/ARCHITECTURE.md), [AUDIO.md](https://github.com/binx-ux/Music-engine-/blob/master/AUDIO.md), [CONTRIBUTING.md](https://github.com/binx-ux/Music-engine-/blob/master/CONTRIBUTING.md).

## Discord

kynvyr. Git channel gets pushes. Versions channel gets GitHub releases.
