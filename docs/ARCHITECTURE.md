# Architecture

Cuebox is split so the audio engine does not depend on WPF.

| Project | Role |
| --- | --- |
| `src/Core` | Shared types, paths, versioned config models |
| `src/Logging` | File logs with secret redaction |
| `src/Storage` | JSON config, profiles, DPAPI token store, backup zip |
| `src/Audio` | WASAPI devices, mixer, DSP, file/URL sources, virtual route |
| `src/Soundboard` | Pad layout persistence |
| `src/Spotify` | OAuth PKCE and Web API client |
| `src/IPC` | `RegisterHotKey` and HKCU Run key startup |
| `src/UI` | WPF shell. Talks to the engine through `AppSession` |
| `tests/Mixline.Tests` | Mixer, DSP, hotkey, URL, config tests |

The UI never runs inside the WASAPI callback. Meter values are snapshots read on a 20 Hz timer. A dispatcher exception is logged and marked handled so a window glitch should not dispose the engine. True process isolation (a separate engine host) is not shipped yet; the named pipe name in `AppInfo` is reserved for that.

## Audio data flow

```
Microphone WASAPI capture thread
        -> float ring
Music decode thread
        -> float ring
Soundboard voices (pre-decoded)
        -> mixer
                 |-> monitor WASAPI render (headphones)
                 |-> virtual WASAPI render (virtual cable)
```

Internal processing is 48 kHz stereo float32. Device formats are converted on the capture and render edges.

## UI

WPF with Windows 11 system backdrop (`DWMWA_SYSTEMBACKDROP_TYPE`) when available. Fallback is an opaque dark window. The audio engine does not reference WPF types.
