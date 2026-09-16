# Contributing

## Layout

Keep audio code out of `src/UI`. If a change needs WASAPI, put it in `src/Audio`.

## Rules

- No allocations in the monitor mix callback if you can avoid them. Preallocate.
- Do not log tokens, passwords, or recorded audio.
- Do not add game injection, DLL hooks, or Spotify DRM workarounds.
- Prefer Microsoft Learn, Spotify developer docs, and current package docs over old forum snippets.
- Do not use em dashes in comments, docs, or UI copy.

## Tests

```
dotnet test Mixline.sln
```

Hardware-dependent WASAPI tests are not run in CI. Use the in-app test tone to check routing on a real machine.

## Pull requests

Say what you changed and how you verified it (tone into headphones, virtual cable into Discord, etc.).
