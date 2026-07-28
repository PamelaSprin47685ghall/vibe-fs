# 0.4.0-rc.3 — release candidate

| Field | Value |
|---|---|
| Version | `0.4.0-rc.3` |
| Date | 2026-07-28 |
| Base commit (pre-cutover) | `b987b4967cf3bd2fa22608d0f321bc6b269569b9` |
| Status | Release candidate for controlled distribution. Not final `0.4.0`. |

## Gate requirements (PR11)

```bash
git clean -xfd
npm ci
npm run format
npm run build
npm run test:compile
npm run test:next
npm run test:manager-tools
node testkit/opencode/tests/gate-testkit.mjs
CANARY_REPEAT=3 node scripts/run-canary-staggered.mjs
npm pack ./build
# empty-dir install of resulting tarball + import('wanxiangshu')
```

## Product claims covered by this RC

- Prompt Authority single runtime service + stable LogicalRunId + AgentOwnerRoot two-phase send
- Typed join/list JSON without F# Result arrays
- Manager full-loop Host canary
- Companion eligibility from ActiveLogicalRun only
- Review physical confirmation path
- Fallback Logical-Run epoch; omit-model inherits BaseModel only
- Process/PTY stress, restart recovery, Orchestrator publish crash-safe canaries

## Still not final 0.4.0

- RC observation period
- Second clean-checkout gate after promotion
- Optional real-provider A/A/B/B request traces
- Commercial license / private package policy for external distribution
