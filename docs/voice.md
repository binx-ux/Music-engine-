---
layout: default
title: Voice
---

# Voice

Effects stay off until you turn them on. **Voice Enhance** is a mild high-pass, gate, EQ, compressor, and limiter. Tune is separate.

## Tune

On the Voice page, **Tune** is pitch correction.

- Off
- Natural
- Studio
- Hard

Pick a key and scale (chromatic, major, minor). Amount and retune speed are labeled. Keep formants on if you do not want a chipmunk when the shift is large.

The live note and Hz show when it locks. Speak or sing with the mic live.

## Order in the chain

1. High-pass
2. Noise gate
3. Noise reduction (off by default)
4. EQ
5. Compressor
6. De-esser
7. Tune
8. Saturation
9. Limiter

Internal mix is 48 kHz stereo float, which matches most game voice paths.

## Buffer

Settings → Audio. **Stable** is the default. Drop to Balanced or Low Latency if you want less delay and your machine can take it. Exclusive mode will fail with a visible error if the device rejects it.
