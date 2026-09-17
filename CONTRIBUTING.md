# Contributing

Bugs, features, and questions go through GitHub Issues. Pick the matching form:

https://github.com/binx-ux/Music-engine-/issues/new/choose

Security holes go to [SECURITY.md](SECURITY.md), not a public issue.

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

## Version

Cuebox versions look like `1.2.33`.

- major: first number goes up, the rest reset to 0 (`1.2.33` -> `2.0.0`)
- minor: second number goes up, the rest stay (`1.2.33` -> `1.3.33`)
- fix: last two numbers go up (`1.2.33` -> `1.2.34`)

Current release is `1.3.1`.

## Pull requests

Fork, branch off `master`, open a PR. The form asks what you changed and how you checked it (tone into headphones, virtual cable into Discord, etc.).
