# Orchestrator e2e baseline failure dumps

Captured against a clean HEAD `dist` (`npm run build` after excluding in-progress Causal WIP from compile). No production or scenario files were modified for this capture.

Invocation pattern (matches `tests/e2e/run.mjs` child spawn; each case is a direct `node` entrypoint):

```bash
mkdir -p changes/active/evidence/orchestrator-baseline
npm run warmup:opencode
npm run build   # required once dist was wiped by a concurrent broken build
node tests/e2e/cases/orchestrator-publish.test.mjs > changes/active/evidence/orchestrator-baseline/orchestrator-publish.log 2>&1
node tests/e2e/cases/orchestrator-unhappy-path.test.mjs > changes/active/evidence/orchestrator-baseline/orchestrator-unhappy-path.log 2>&1
node tests/e2e/cases/orchestrator-restart-publish.test.mjs > changes/active/evidence/orchestrator-baseline/orchestrator-restart-publish.log 2>&1
```

| Canary | Command | Exit code | Last watchdog progress | Blocked expectations | `E2E DIAGNOSTICS` section |
|--------|---------|-----------|------------------------|----------------------|---------------------------|
| `orchestrator-publish` | `node tests/e2e/cases/orchestrator-publish.test.mjs` | **1** | `manager.2 lane=manager.2 expectation=none` | `orch.2`, `manager.3`, `manager.4` | **No** |
| `orchestrator-unhappy-path` | `node tests/e2e/cases/orchestrator-unhappy-path.test.mjs` | **1** | `manager-finality-reviewer.1 lane=manager-finality-reviewer.1 expectation=none` | `orch.2`, `manager.3`, `manager.4`, `coder-resolve.0`, `coder-resolve.1` (scenario error: timed out waiting for `manager.3`) | **No** |
| `orchestrator-restart-publish` | `node tests/e2e/cases/orchestrator-restart-publish.test.mjs` | **1** | `manager.2 lane=manager.2 expectation=none` | `orch-continue.0`, `orch-continue.1`, `manager.3`, `manager.4` (scenario error: timed out waiting for `barrier-reviewer.0`) | **No** |

## Notes

- All three fail via scenario-local **WATCHDOG** silence (5s for publish/restart-publish; 30s for unhappy-path).
- Watchdog dumps include event/Host/journal tails and a `── watchdog blocked expectations ──` block; the formal `══════════════════════ E2E DIAGNOSTICS ══════════════════════` section from `diagnostics-format.js` did **not** appear in any of the three logs.
- Background progress at fire time was consistently stuck on `expectation:manager-finality-reviewer.2` (non-renewal).
