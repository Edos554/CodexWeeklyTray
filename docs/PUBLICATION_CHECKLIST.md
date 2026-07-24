# Publication Checklist

## Completed In This Candidate

- Separated public candidate folder created.
- Private README and internal operation ledger excluded.
- Drive backup packages excluded.
- Historical backup folders excluded.
- `bin`, `obj`, and runtime logs excluded.
- Initial user-facing docs created.
- MIT license decided and `LICENSE` added.
- Clean Release builds from the public candidate folder, including post-review fixes, succeeded with 0 warnings and 0 errors on 2026/07/24.
- Log ON/OFF behavior was verified for ON -> OFF -> ON logging.
- Public candidate footer was updated after reboot to the requested compact order: `更新` -> `ログ` -> `状態`.
- Remaining bars use a continuous blue (100-60) -> green (60-40) -> yellow (40-20) -> red (20-0) palette.
- `Open Codex` suppresses duplicate tray-initiated launches during the same tray-app session and uses the same verified-local-installation then `PATH` fallback as rate-limit retrieval.
- The five-hour remaining bar follows its parent width.
- Sensitive-string, local-path, and excluded-directory scan completed after deleting generated `bin` and `obj`.
- Public target manifest must be regenerated after every public-candidate file change.

## Still Required Before GitHub Publication

- Decide repository description and visibility.
- Run final manual runtime visual check from the current public candidate build, including footer order and bar colors.
- Re-run scan and manifest if any file changes before publication.
- Review whether Japanese UI text should remain as-is or be generalized/localized.
- Decide whether releases will publish source only or downloadable binaries.
- Add screenshots only after checking they contain no private account data.
