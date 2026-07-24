# Publication Scan

Date: 2026/07/24 23:17

Scope: public candidate source after post-review fixes. Backup folders, build output, and runtime logs are excluded from the public target scan.

Result:

- Generated output directories: `CodexWeeklyTray/obj` exists after the local build and is excluded from the public target; no source `bin` or runtime `logs` directory is included.
- Secret-like scan: no API key, `sk-*`, `sk-proj-*`, password, cookie, or OAuth secret values were found in public target files.
- Path scan: no user-specific absolute path, Drive backup package path, `Current`, or `Versions` runtime path was found in public target source and documentation files.

Reviewed scan hits:

- `README.md` states that the app does not store OAuth tokens, cookies, or API keys.
- `CodexAppServerClient.cs` uses `CancellationToken token` as ordinary .NET control-flow code.
- `CodexAppServerClient.cs` and `AppLog.cs` use `LocalAppData` through .NET APIs to locate local Codex CLI and log folders; no user-specific absolute path is embedded.

Remaining before GitHub publication:

- Manual runtime visual verification from the latest public candidate build, including `更新 -> ログ -> 状態`, all four bar-color ranges, the five-hour bar width, and `Open Codex` PATH fallback.
- Remove generated `CodexWeeklyTray/obj` before creating the GitHub source package.
- Repository description and visibility decision.
- Source-only versus downloadable-binary publication decision.
