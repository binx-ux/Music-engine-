---
layout: default
title: Spotify
---

# Spotify

Cuebox talks to Spotify through the documented Web API (PKCE). It can show now playing and send play, pause, skip. It cannot mix full Spotify tracks into your virtual mic. Encrypted audio stays in Spotify.

## Setup

1. Create an app on the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
2. Redirect URI: `http://127.0.0.1:43821/callback`
3. Paste the Client ID into Cuebox Settings (or use Find Client ID, it asks first)
4. Connect Spotify and sign in in the browser
5. Keep the Spotify app playing on a real device so Cuebox has something to control

Tokens sit in your data folder as `spotify.bin`, encrypted with Windows DPAPI for the current user. Logs strip bearer tokens.

## What we will not do

- Read Spotify process memory
- Steal cookies
- Inject into Spotify
- Bypass DRM with loopback capture
- Mix the encrypted stream into CABLE Output

Want a song in the game mic? Use the Music page with a file or a link you are allowed to play.
