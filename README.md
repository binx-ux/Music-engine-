# Cuebox

Windows mixer for mic + music + soundboard. Routes the mix through a virtual cable so Roblox, Discord, and games just see another mic.

**1.4.1** · [docs](https://binx-ux.github.io/Music-engine-/) · [releases](https://github.com/binx-ux/Music-engine-/releases/latest)

![Cuebox](src/UI/Assets/cuebox.png)

## Install

**CueboxSetup.exe** from [Releases](https://github.com/binx-ux/Music-engine-/releases/latest), or run this in PowerShell:

```
irm https://raw.githubusercontent.com/binx-ux/Music-engine-/master/installer/install.ps1 | iex
```

That pulls the latest setup (falls back to the zip if needed). Self-contained. No extra .NET install.

Zip works too: unpack `Cuebox.zip` anywhere. Optional `data.path` next to `Cuebox.exe` with one line for the data folder.

## Features

- Real mic through WASAPI
- Search songs by name and download them (yt-dlp)
- Paste YouTube / audio links
- Soundboard with hotkeys
- Voice chain if you turn pieces on
- Headphones and virtual mic can hear different mixes
- Spotify Premium control (now playing / play / pause). Encrypted audio stays in Spotify.

## Games / Discord

1. Install [VB-Audio Cable](https://vb-audio.com/Cable/)
2. In Cuebox: mic + headphones, virtual out = `CABLE Input`
3. Start the engine
4. In the other app: mic = `CABLE Output`

More: [docs/VIRTUAL_DEVICE.md](docs/VIRTUAL_DEVICE.md)

## Build

```
dotnet restore Mixline.sln
dotnet build Mixline.sln -c Release
dotnet publish src/UI/Mixline.App.csproj -c Release -r win-x64 --self-contained true
```

Exe is `Cuebox.exe`.

Notes: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · [docs/AUDIO.md](docs/AUDIO.md) · [docs/spotify.md](docs/spotify.md) · [CONTRIBUTING.md](CONTRIBUTING.md)

## Layout

```
src/          app code (UI, Audio, Spotify, ...)
native/       cuebox_dsp (Rust)
installer/    Inno Setup + scripts
docs/         GitHub Pages + tech notes
scripts/      helpers
tests/        tests
```

## License

[MIT](LICENSE)

## kynvyr

[github](https://github.com/binx-ux) · [guns.lol](https://guns.lol/kynvyr_) · [ig](https://instagram.com/kynvyr/) · [coffee](https://buymeacoffee.com/kynvyr) · discord **kynvyr**
