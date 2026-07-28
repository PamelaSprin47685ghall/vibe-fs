# 0.4.0-rc.5 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.5` |
| Base | merge of `worktree-0-branch` into rc.4 lineage |
| Date | 2026-07-28 |
| Status | Release candidate. **Not** final `0.4.0`. |
| Package | `private: true`, license `SEE LICENSE IN LICENSE` |
| Scope freeze | `docs/SCOPE-0.4.0-FREEZE.md` |

## Included from worktree-0-branch

- Full role system prompts (`next/prompts/*.md`)
- Session-wide formal A accumulation for terminal completion / join `finalText`
- Session-wide B documented as companion LatestB work record
- Parent background prefers parent B, else session-wide formal A

## Gate

Sealed on clean `git clean -xfd` + `npm ci` + `test:release` (`CANARY_REPEAT=3`) + pack + empty-dir install.
Evidence: `docs/evidence/0.4.0-rc.5/`.

| Gate | Result |
|---|---|
| clean `npm ci` | pass |
| `tests-next` | 277 passed |
| manager-tools + gate-testkit | pass (29) |
| `CANARY_REPEAT=3` 17 canaries | pass |
| pack + empty-dir install/import | pass |

## Still blocking final 0.4.0

- Observation period with no open P0/P1
- **Blocking** provider-visible same-run A/A/B/B request trajectory
- Second clean-checkout gate after promotion to version `0.4.0`
- Private delivery only unless a separate license decision is made
