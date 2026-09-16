# Security

If you found a real hole (token leak, arbitrary file write, anything that hurts users), do not open a public issue.

Use GitHub Security Advisories on this repo:

https://github.com/binx-ux/Music-engine-/security/advisories/new

Say what version you were on and how to reproduce it. Do not attach recorded mic audio.

Cuebox stores Spotify tokens with DPAPI under `%AppData%\Cuebox`. Logs are supposed to strip secrets. If a log still has a token, that is a bug worth a private report.

This project will not add game injection, DLL hooks, or Spotify DRM bypasses. Requests for those get closed.
