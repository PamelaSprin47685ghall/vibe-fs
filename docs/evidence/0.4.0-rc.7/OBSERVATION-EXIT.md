# 0.4.0-rc.7 Observation Exit

| Field | Value |
|---|---|
| Sealed RC | `0.4.0-rc.7` |
| Gate commit | `0b0e43bd3d4bd01365dbbc743276d4e3050ef980` |
| Evidence commit | `8e84c6dcf716336bc44d97486d64304c3555d662` |
| Exit date | 2026-07-28 |
| Exit type | Event-driven (criteria in `docs/RC-OBSERVATION-0.4.0.md`) |

## Exit criteria checklist

| Criterion | Evidence |
|---|---|
| Immutable sealed RC with full evidence | `docs/evidence/0.4.0-rc.7/` |
| Full release gate green | `test-release.txt`, `canary-3round-clean.txt` (18 canaries × 3) |
| No open P0/P1 | `NOGO-GAPS.md` — AABB wire + authority closed |
| P2 decisions | None outstanding for final |
| Final cut needs no production code | Version/docs only after this exit |

## Observation scope vs canary coverage

Controlled environment: real OpenCode host (`opencode 1.18.7`) + StrictMock provider forest.

| Scope item | Coverage |
|---|---|
| Ordinary Coder work | agent-dsl, inspector-oneshot, executor, process-stress |
| Manager multi-child fork/join | manager-full-loop, agent-dsl |
| DevOps PTY long-running | pty-stress |
| Reviewer REVISE + dual PERFECT | reviewer-verdict, orchestrator*, reviewer-restart |
| Provider retry | fallback, **fallback-aabb-trace** (wire A/A/B/B) |
| OpenCode restart | host-restart, reviewer-restart, orchestrator-restart-publish |
| Orchestrator multi-worktree publish | orchestrator, orchestrator-publish, restart-publish |
| Companion context replacement | companion, companion-cache, companion-replacement |
| User cancel / parent abort | host-nudge, process/pty dispose paths, tests-next cancel suite |

## Must-not-observe audit

No open defects matching the observation “must not observe” list after rc.7 seal:

- Authority mismatch → fixed rc.6 (confirmation correlation)
- Fifth provider request / wrong EffectiveModel → fixed rc.7 (aabb-trace)
- Duplicate completion / join hang / review misbind / publish / leak → covered by green canary + gate-testkit leak probe

## Decision

**Observation exit: YES.** Proceed to version-only final `0.4.0` cut on top of sealed rc.7, with a second clean-checkout gate on the real version number.

## Residual acceptances (not P0)

| Item | Decision |
|---|---|
| Host double-fires first user prompt under some error paths | Accept: canary normalizes leading duplicate; Logical Run still A→A→B→B for four failure attempts |
| 250ms idle settle for PluginFallbackRetry | Accept: host contract; documented in rc.7 notes |
| Mock reseal on model-id system cold boundary | Accept: product system embeds model name; tools must stay identical |
