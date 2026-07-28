# SUPERSEDED by docs/RC-0.4.0-rc.3.md

# 0.4.0-rc.3-dev status (not distribution RC)

| Field | Value |
|---|---|
| Version marker | `0.4.0-rc.3-dev` |
| Date | 2026-07-28 |
| Base commit | `5ab4326101121f681e720a20f33d9b0a34596d7e` |
| Status | Development gate after PR0–PR9 + post-gate fixes. **Not** `0.4.0-rc.3` distribution. |
| Worktree | dirty (uncommitted PR7–PR11 fixes + evidence docs) |

Evidence directory: `docs/evidence/0.4.0-rc.3-dev/`

## Green on this dirty tree

| Gate | Result |
|---|---|
| `npm run build` | pass |
| `tests-next` | 275 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=1` staggered suite | 17/17 pass |
| `CANARY_REPEAT=3` staggered suite | 17/17 × 3 pass |
| `npm pack ./build` | `wanxiangshu-0.4.0-rc.3-dev.tgz` |
| empty-dir install + `import('wanxiangshu')` | pass |
| manager-full-loop | pass |
| companion basic/cache/replacement | pass (parallel wait bound fixed) |
| fallback | pass |
| process-stress / pty-stress | pass |
| host-restart / reviewer-restart | pass |
| orchestrator / publish / restart-publish | pass |

## Remaining before real `0.4.0-rc.3`

1. **Commit** the dirty worktree (PR7–PR11 fixes + docs/evidence).
2. Clean checkout rebuild: `git clean -xfd && npm ci && npm run test:release` on that commit.
3. Version cutover only after clean-checkout green:
   `0.4.0-rc.3-dev` → `0.4.0-rc.3` in package manifests + CHANGELOG.
4. **Blocking for final 0.4.0**: provider request-trace proof of same-run A/A/B/B under real retries.
5. Final `0.4.0` only after RC observation period and a second clean-checkout gate.

## No-Go still checked

- Do not claim final `0.4.0` from this marker alone.
- Do not cut `0.4.0-rc.3` from a dirty tree without committed + clean rebuild evidence.
- Do not publish private package publicly.
- Do not reuse withdrawn `rc.2` green claims.
