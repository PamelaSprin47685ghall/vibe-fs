# 0.4.0-rc.3 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.3` |
| Date | 2026-07-28 |
| Commit | `bfb130dbc771de14a146a56347e6ea387cf11b18` |
| Status | Release candidate for controlled distribution. **Not** final `0.4.0`. |
| Package | private commercial (`private: true`, `SEE LICENSE IN LICENSE`) |

Evidence: `docs/evidence/0.4.0-rc.3/`

## Clean gate evidence (this commit lineage)

| Gate | Result |
|---|---|
| `git clean -xfd && npm ci` | pass |
| `npm run build` | pass |
| `tests-next` | 276 passed / 0 failed |
| `test:manager-tools` | pass |
| `gate-testkit` | 29 passed |
| `CANARY_REPEAT=3` staggered P0 (17 canaries) | pass |
| `npm pack ./build` | `wanxiangshu-0.4.0-rc.3.tgz` |
| empty-dir install + `import('wanxiangshu')` | pass |

## Product claims covered

- Prompt Authority single runtime service + stable LogicalRunId + AgentOwnerRoot two-phase send
- Typed join/list JSON without F# Result arrays
- Manager full-loop Host canary
- Companion eligibility from ActiveLogicalRun only (basic/cache/replacement)
- Review physical confirmation path
- Fallback Logical-Run epoch; omit-model inherits BaseModel only
- Process/PTY stress; host/reviewer restart recovery
- Orchestrator publish / restart-publish crash-safe canaries

## Fixes required for clean gate stability

- testkit: classify `devops` before bare `executor→inspector` role heuristic
- companion-replacement: renew scenario watchdog and re-prompt when Y busy-skips self-rebase/delta under parallel load

## Still not final 0.4.0

- RC observation period
- Second clean-checkout gate after promotion to `0.4.0`
- **Blocking for final 0.4.0**: real-provider same-run A/A/B/B request traces (policy aligned 2026-07-28)
- Commercial license / private package policy for external distribution
