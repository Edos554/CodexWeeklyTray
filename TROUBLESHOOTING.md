# Troubleshooting

## Codex CLI Not Found

Confirm that Codex CLI is installed and available locally:

```powershell
codex --version
```

The app first searches known local Codex CLI install locations and falls back to `codex` on `PATH`.

## Usage Fetch Fails

Possible causes:

- Codex CLI is not logged in.
- The local Codex app-server interface changed.
- Network or account access is temporarily unavailable.
- Rate-limit data is not returned for the current account or plan.

Use the tray menu's refresh action after confirming Codex CLI works.

## SmartScreen Or Security Warnings

Unsigned builds may show Windows SmartScreen or security reputation warnings. This public candidate does not include signing infrastructure.