# User Guide

## Start

Run `CodexWeeklyTray.exe` after building or publishing the app. The app appears in the Windows notification area.

## Tray Icon

The tray icon shows the approximate remaining weekly Codex usage percentage. If usage cannot be fetched, the icon switches to an error-style indicator.

## Menu

Right-click the tray icon to open the menu:

- `今すぐ更新`: fetch the latest usage information.
- `ウィンドウを開く`: show the compact status window.
- `Codexを開く`: start the locally installed Codex CLI/app entrypoint when it can be resolved.
- `常に手前に表示`: toggle whether the status window stays above other windows.
- `終了`: close the tray app.

## Notes

The app depends on the local Codex CLI being installed and logged in. It does not manage login itself.
