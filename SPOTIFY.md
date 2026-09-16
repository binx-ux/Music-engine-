# Spotify

Cuebox uses Spotify's documented Authorization Code with PKCE flow and the Web API.

## What works

- Connect with a Client ID from the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard)
- Redirect URI: `http://127.0.0.1:43821/callback` (add this URI to your Spotify app)
- Scopes: `user-read-currently-playing`, `user-read-playback-state`, `user-modify-playback-state`, `user-read-private`
- Now playing: title, artist, album, artwork, play state
- Play / pause / next / previous on the user's existing Spotify Connect device

Tokens are stored with Windows DPAPI (`ProtectedData`, current user) in `%AppData%\Cuebox\spotify.bin`. Logs redact bearer tokens and query secrets.

## What does not work, on purpose

Spotify playback is encrypted. Cuebox does not:

- Read Spotify process memory
- Steal cookies or desktop session tokens
- Inject into Spotify
- Loopback-capture Spotify in a way that bypasses DRM
- Mix full Spotify tracks into the virtual microphone

The Web Playback SDK can create a browser playback device, but Spotify's developer terms restrict commercial use of that SDK without approval, and the SDK still does not give Cuebox PCM to mix. It is not embedded.

Official 30-second `preview_url` MP3s exist on some tracks. Cuebox currently shows that URL in the now-playing payload for diagnostics but does not auto-play previews into the mixer.

## Using it

1. Create a Spotify app.
2. Paste the Client ID into Cuebox Settings.
3. Click Connect Spotify and sign in in the browser.
4. Open the Spotify desktop or mobile app so there is an active player.
5. Cuebox can display and control that player. Put music you want in the virtual mic into the local Music page instead.
