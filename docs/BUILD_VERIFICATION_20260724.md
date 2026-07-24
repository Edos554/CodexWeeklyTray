# Build Verification

Date: 2026/07/24

Scope: public candidate source after post-review fixes.

Result:

- Restore: succeeded.
- Build: succeeded.
- Warnings: 0.
- Errors: 0.
- Latest checked build: footer order updated to `更新` -> `ログ` -> `状態`.
- Latest checked build: remaining bars use a continuous blue (100-60) -> green (60-40) -> yellow (40-20) -> red (20-0) palette.
- Latest checked build: `Open Codex` uses the same verified-local-installation then `PATH` fallback as rate-limit retrieval.
- Latest checked build: the five-hour remaining bar follows its parent width.

Notes:

- Build outputs and intermediate files may be generated under `CodexWeeklyTray/bin` and `CodexWeeklyTray/obj` by a normal local build.
- `bin`, `obj`, runtime logs, backup folders, and Drive-package folders are not part of the public target manifest.
- Runtime visual verification from the latest public candidate build is still required before GitHub publication.
