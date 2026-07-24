<img width="466" height="93" alt="スクリーンショット 2026-07-24 234005" src="https://github.com/user-attachments/assets/caa9c534-1eb7-4e26-89f1-d4253973bef1" />

# Codex Weekly Tray

Codex Weekly Tray is a small Windows tray app that shows Codex weekly rate-limit usage from the local Codex CLI.

This public candidate is separated from the private personal-operation folder. It intentionally excludes private logs, backup packages, Drive records, internal operation notes, build outputs, and machine-specific paths.

## What It Does

- Starts as a Windows notification-area app.
- Reads Codex account rate-limit information through the local Codex CLI app-server interface.
- Shows remaining weekly usage as a tray icon percentage.
- Shows weekly and five-hour window details when available.
- Provides refresh, open Codex, top-most window toggle, and exit actions.

## Opening Codex

- `Open Codex` uses a verified local Codex CLI when available and otherwise falls back to `codex` on `PATH`.
- `Open Codex` prevents duplicate Codex CLI launches while the tray app remains running.
- If the tray app is exited and started again while its Codex CLI window remains open, the existing window is not tracked. Selecting `Open Codex` starts a new CLI window.
- Codex windows opened independently from the tray app are not managed by the tray app.

## Requirements

- Windows.
- .NET 8 SDK for building from source.
- Codex CLI installed and logged in locally.

The app does not store OAuth tokens, cookies, or API keys. It relies on the existing local Codex CLI session.

## Status

This is a public candidate, not a published release. Build and runtime verification from this separated folder should be completed before GitHub publication.

## Documents

- `USER_GUIDE.md`
- `BUILD.md`
- `TROUBLESHOOTING.md`
- `docs/PUBLICATION_CHECKLIST.md`
- `docs/LICENSE_DECISION.md`

## License

MIT License. See `LICENSE`.
