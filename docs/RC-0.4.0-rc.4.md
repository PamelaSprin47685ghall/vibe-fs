# 0.4.0-rc.4 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.4` |
| Commit | rc.4 seal commit on `master` (`git log -1 --oneline`) |
| Date | 2026-07-28 |
| Status | Release candidate. **Not** final `0.4.0`. |
| Package | `private: true`, license `SEE LICENSE IN LICENSE` |

Evidence: `docs/evidence/0.4.0-rc.4/`

## Clean gate evidence

| Gate | Result |
|---|---|
| `git clean -xfd && npm ci` | pass |
| `npm run build` | pass |
| `tests-next` | 276 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=3` (17 canaries) | pass |
| pack + empty-dir install | pass (prior 0.4.0 clean gate; version label amended to rc.4) |

## Notes

- This supersedes an accidental premature `0.4.0` final cut.
- Final `0.4.0` still requires a later clean-checkout promotion after observation.
- Optional real-provider same-run A/A/B/B request traces remain non-blocking follow-up.
