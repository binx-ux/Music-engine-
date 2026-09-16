╭══• ೋ•✧๑♡๑✧•ೋ •══╮
           Cuebox
╰══• ೋ•✧๑♡๑✧•ೋ •══╯

≽ ^⎚ ˕ ⎚^ ≼

*ੈ✩‧₊˚༺☆༻*ੈ✩‧₊˚

mic + music + soundboard on windows. dumps the mix into a virtual cable so roblox / discord / games just see another mic.

no injection. no ripping spotify.

<p align="center">
  <img src="src/UI/Assets/cuebox.png" width="120" alt="Cuebox">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Windows-10%2F11-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8">
  <img src="https://img.shields.io/github/v/release/binx-ux/Music-engine-?style=for-the-badge&color=C9A36A" alt="release">
  <img src="https://img.shields.io/github/downloads/binx-ux/Music-engine-/total?style=for-the-badge&color=7DBA96" alt="downloads">
  <img src="https://img.shields.io/badge/license-ask%20%3D%20free-C9A36A?style=for-the-badge" alt="license">
  <img src="https://img.shields.io/github/stars/binx-ux/Music-engine-?style=for-the-badge&color=E8D4B0" alt="stars">
</p>

──── ୨୧ ────

## install

cmd. writes the zip as bytes then unpacks it.

```
powershell -NoP -C "$z=$env:TEMP+'\cuebox.zip'; [IO.File]::WriteAllBytes($z,(iwr -useb 'https://github.com/binx-ux/Music-engine-/releases/latest/download/Cuebox.zip').Content); $d=$env:LOCALAPPDATA+'\Cuebox'; if(Test-Path $d){ri $d -Recurse -Force}; Expand-Archive $z $d -Force; start (Join-Path $d 'Cuebox.exe')"
```

lands in `%LOCALAPPDATA%\Cuebox`. settings in `%AppData%\Cuebox`. no extra .net install.

──── ୨୧ ────

## what it actually does

- your real mic (wasapi)
- local files / direct audio urls
- soundboard + global hotkeys
- voice stuff if you turn it on (eq, comp, gate, pitch)
- headphones hear one mix, virtual mic hears another
- spotify now playing / play pause. not the encrypted stream. that stays on spotify.

need a virtual cable if other apps should hear you. vb-audio cable is the usual one. more in [VIRTUAL_DEVICE.md](VIRTUAL_DEVICE.md).

──── ୨୧ ────

## roblox / discord

1. install a virtual cable
2. open cuebox, pick your mic + headphones
3. virtual out = `CABLE Input` (or whatever your cable calls playback)
4. start the engine
5. in the other app, mic = `CABLE Output`
6. talk, play a song, smash a pad

──── ୨୧ ────

## build it yourself

```
dotnet restore Mixline.sln
dotnet build Mixline.sln -c Release
dotnet test Mixline.sln -c Release
dotnet publish src/UI/Mixline.App.csproj -c Release -r win-x64 --self-contained true
```

exe name is `Cuebox.exe`.

more pages if you care: [ARCHITECTURE.md](ARCHITECTURE.md) · [AUDIO.md](AUDIO.md) · [SPOTIFY.md](SPOTIFY.md)

──── ୨୧ ────

## license

[LICENSE](LICENSE)

sell it if you want. if somebody asks for a copy you give it to them free. no ifs, ands, or buts.

──── ୨୧ ────

## kyn

⋆˚࿔ kynvyr 𝜗𝜚˚⋆

[github](https://github.com/binx-ux) · [guns.lol](https://guns.lol/kynvyr_) · [ig](https://instagram.com/kynvyr/) · [coffee](https://buymeacoffee.com/kynvyr)

discord: **kynvyr**

<p>
  <a href="https://github.com/binx-ux"><img src="https://img.shields.io/badge/GitHub-binx--ux-181717?style=for-the-badge&logo=github&logoColor=white" alt="GitHub"></a>
  <a href="https://instagram.com/kynvyr/"><img src="https://img.shields.io/badge/Instagram-kynvyr-E4405F?style=for-the-badge&logo=instagram&logoColor=white" alt="Instagram"></a>
  <a href="https://buymeacoffee.com/kynvyr"><img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-kynvyr-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me a Coffee"></a>
  <a href="https://guns.lol/kynvyr_"><img src="https://img.shields.io/badge/guns.lol-kynvyr_-111111?style=for-the-badge" alt="guns.lol"></a>
  <img src="https://img.shields.io/badge/Discord-kynvyr-5865F2?style=for-the-badge&logo=discord&logoColor=white" alt="Discord">
</p>
