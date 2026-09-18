# Audio

## APIs used

- WASAPI through NAudio (`WasapiCapture`, `WasapiOut`, `MMDeviceEnumerator`)
- `IMMNotificationClient` for device add/remove/default changes
- Media Foundation via `AudioFileReader` / `MediaFoundationReader` for MP3, WAV, FLAC, M4A/AAC, WMA
- NVorbis for OGG
- Shared mode by default. Exclusive mode is optional and will fail over with a visible error if the device rejects it

Shared-mode IAudioClient3 period selection is not wired yet. Buffer presets map to WASAPI event-driven latency:

| Preset | Requested buffer |
| --- | --- |
| Low Latency | 10 ms |
| Balanced | 20 ms |
| Stable | 40 ms |
| Custom | 5 to 80 ms |

Internal mix is 48 kHz stereo float. That matches most game voice paths. Capture and file sources are resampled with cubic interpolation or NAudio's WDL resampler on the decode thread.

## Mixer buses

Each of Mic, Music, and Soundboard has volume, mute, solo, pan, monitor send, and virtual send. Master has volume, mute, monitor send, and virtual send. A limiter sits on both output buses unless bypass is enabled in Settings.

## Voice chain

Optional, in order:

1. High-pass
2. Noise gate
3. Spectral noise reduction (off by default)
4. Five-band EQ
5. Compressor
6. De-esser
7. Pitch correction
8. Saturation
9. Limiter

Voice Enhance turns on a mild version of high-pass, gate, voice EQ, compressor, and limiter. Autotune stays off unless you choose Light, Medium, or Strong.

## Real-time rules

The monitor `ISampleProvider.Read` callback mixes preallocated buffers only. File decode, URL IO, and soundboard file loads happen off that thread. The callback still takes a short lock to snapshot voice playback and mixer parameters.

## Test tone

Settings can enable a 1 kHz tone at a low level so you can verify routing without another app.
