---
layout: default
title: Install
---

# Install

CueboxSetup.exe asks two questions: where the app sits, and where settings, music, logs, and pads sit. Those can be different drives.

[Download CueboxSetup.exe](https://github.com/binx-ux/Music-engine-/releases/latest/download/CueboxSetup.exe) from the latest GitHub release.

## PowerShell

```
irm https://raw.githubusercontent.com/binx-ux/Music-engine-/master/installer/install.ps1 | iex
```

Downloads the latest `CueboxSetup.exe` and runs it. If that fails, it falls back to the zip and lets you pick folders.

Self-contained. You do not install .NET yourself.

## Zip

Unpack [Cuebox.zip](https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip) anywhere and run `Cuebox.exe`. Optional: a `data.path` file next to the exe with one line, the data folder. Or make a `Cuebox.data` folder next to the exe for a portable layout.

You can move the data folder later in **Settings → Files**.

## After install

1. Open Cuebox
2. Devices: pick your real mic and headphones
3. Install [VB-Audio Cable](https://vb-audio.com/Cable/) if you want games to hear you
4. Devices → **Use for games**
5. In the game or Discord, set the microphone to **CABLE Output**

More on that path: [Games]({{ '/games/' | relative_url }}).
