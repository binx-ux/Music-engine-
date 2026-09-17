---
layout: default
title: Games
---

# Games and Discord

Windows will not let a normal app invent a new microphone. Cuebox plays the mix into a virtual cable. The other app records the other end of that cable.

This is the same class of routing voice tools have used for years. Cuebox does not hook Roblox, Discord, or any game.

## Setup

<ol class="steps">
  <li>Install <a href="https://vb-audio.com/Cable/">VB-Audio Cable</a>, then restart Cuebox.</li>
  <li>Home or Devices → <strong>Use for games</strong>.</li>
  <li>Cuebox virtual output = <code>CABLE Input</code>.</li>
  <li>In Roblox, Discord, or the game, microphone = <code>CABLE Output</code>.</li>
  <li>Talk, play a song, hit a pad. Watch the meters.</li>
</ol>

| Cuebox virtual output | App microphone |
| --- | --- |
| CABLE Input (VB-Audio) | CABLE Output |
| VoiceMeeter Input | VoiceMeeter Output |

If no cable is installed, Cuebox still works in your headphones. Games just will not hear the mix.

## If nobody can hear you

- Engine is running (Home says Running)
- Cuebox virtual out is CABLE Input, not your speakers
- The game mic is CABLE Output, not your headset mic
- Mic and music sends to the virtual bus are not muted on the mixer
- Windows default mic can be pointed at CABLE Output from Devices if you check that box

## What we will not ship

No DLL injection, no game hooks, no “avoid TOS” mute bypass. Those get closed. The virtual cable is the supported path.
