# Cuebox

Windows mixer for mic, music, and a soundboard. Sends the mix to a virtual cable so Roblox, Discord, and other apps can pick it up like a normal mic.

No game injection. No Spotify DRM ripping.

## Install (Windows)

Paste this in Command Prompt. It downloads the zip as raw bytes, writes the file, then unpacks it:

```
powershell -NoP -C "$z=$env:TEMP+'\cuebox.zip'; [IO.File]::WriteAllBytes($z,(iwr -useb 'https://github.com/binx-ux/Cuebox/releases/latest/download/Cuebox.zip').Content); $d=$env:LOCALAPPDATA+'\Cuebox'; if(Test-Path $d){ri $d -Recurse -Force}; Expand-Archive $z $d -Force; start (Join-Path $d 'Cuebox.exe')"
```

That puts Cuebox in `%LOCALAPPDATA%\Cuebox` and launches it. Settings live in `%AppData%\Cuebox`.

## What it does

- Capture a physical microphone through WASAPI
- Play local audio files and supported direct audio URLs
- Trigger a soundboard with global hotkeys
- Process voice (EQ, compressor, gate, optional pitch correction)
- Mix those sources with independent monitor and virtual-mic sends
- Render the mix to headphones and to a virtual cable playback device
- Show Spotify now playing and control an existing Spotify player through official APIs

## Requirements

- Windows 10 1809 or later, 64-bit. Windows 11 is the primary target.
- A physical microphone and headphones/speakers
- A virtual audio cable if you want other apps to hear the mix (see [VIRTUAL_DEVICE.md](VIRTUAL_DEVICE.md))

The download above is self-contained. You do not need to install .NET separately.

## Typical Roblox / Discord setup

1. Install a virtual audio cable (VB-Audio Cable is a common option).
2. Open Cuebox. Select your real microphone and headphones.
3. Select the virtual cable's playback device as Virtual Output (often named CABLE Input).
4. Start the audio engine if it is not already running.
5. In Roblox or Discord, set the microphone to the matching capture device (often CABLE Output).
6. Speak, play music, or fire soundboard pads. Other apps receive the mixed signal.

## Spotify

See [SPOTIFY.md](SPOTIFY.md). Local files still work if Spotify is disconnected.

## Build from source

```
dotnet restore Mixline.sln
dotnet build Mixline.sln -c Release
dotnet test Mixline.sln -c Release
dotnet publish src/UI/Mixline.App.csproj -c Release -r win-x64 --self-contained true
```

The published exe is `Cuebox.exe`.

## Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [AUDIO.md](AUDIO.md)
- [SPOTIFY.md](SPOTIFY.md)
- [VIRTUAL_DEVICE.md](VIRTUAL_DEVICE.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)

## License

Application code in this repository is provided for the Cuebox project. Third-party packages keep their own licenses (NAudio, TagLibSharp, NVorbis).
