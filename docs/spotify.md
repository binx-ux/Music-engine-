---
layout: default
title: Spotify
---

# Spotify

Cuebox talks to Spotify through the documented Web API (PKCE). You need Spotify Premium for play, pause, and skip. It cannot mix full Spotify tracks into your virtual mic. Encrypted audio stays in Spotify.

## Setup

1. Create an app on the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Add both redirect URIs:
   - `http://127.0.0.1:43821/callback`
   - `http://127.0.0.1/callback`
3. Paste the Client ID into Cuebox Settings (or use Find Client ID)
4. Connect Spotify and sign in in the browser
5. Keep the Spotify app open on a real device

Tokens sit in your data folder as `spotify.bin`, encrypted with Windows DPAPI. Logs strip bearer tokens.

Scopes: `user-read-currently-playing`, `user-read-playback-state`, `user-modify-playback-state`, `user-read-private`.

## What we will not do

- Read Spotify process memory
- Steal cookies
- Inject into Spotify
- Bypass DRM with loopback capture
- Mix the encrypted stream into CABLE Output

Want a song in the game mic? Use Music: search a name, paste a link, or drop a file.
