# Upstream Remaining Merge — Batch 7

PR: https://github.com/PamelaSprin47685ghall/vibe-fs/pull/25

## Result

Batch 7 closes M7B and M7C on cumulative batch-6 head `59a6f2f92`. Fork/family/terminal and Satellite/Distiller tests no longer obtain verdicts from JavaScript lifecycle mirrors.

## Production boundaries added or extended

- `OpenCode/Host/TerminalPolicySurface` exposes the real no-journal `TerminalPolicy` decisions for session death and outstanding work.
- `Execution/Session/Attachment/SatelliteSurface` drives the real `SatelliteRuntime` with a controlled Host resource. It observes exact durable reuse, replacement order, conflicts, failed discovery and concurrent single-flight creation.
- `OpenCode/Tools/DistillationSurface.contract` now exposes the typed Handle role separately from the lowercase display label. The production role vocabulary, not the test, performs that conversion.
- Existing `SessionsSurface`, `HandleSurface` and `DistillationSurface` manifests gained only the exact managed-session laws consumed by these tests.

No production lifecycle decision was copied into a Surface. The controlled resource records physical effects; `TerminalPolicy`, `SatelliteRuntime`, `HandleProjection`, role/catalog owners and Distiller policy still decide all outcomes.

## Proof correction

M7B deleted three files containing 34 constant-object assertions for fork installation, nudge, terminal dispatch and family cascade. Their behavioral claims already had production-bound coverage through ForkLifecycle, Handle, Attachment and SyncDelegate surfaces. Keeping the local objects added declarations without adding a failure path.

M7C replaced a complete JavaScript Satellite state machine and a constant Distiller result. The old suite could claim:

- concurrent single-flight by resolving the same prebuilt object twice;
- semantic-cut failure with a literal `{ result: 'Error' }` object;
- recovery, replacement and conflict by branching inside test support;
- hidden Distiller ownership with a constant four-field object.

The replacement calls production owners. The literal semantic-cut assertion was deleted because it executed no production path; MANAGED-SESSION-011 is now proved by real `create → close old association → link replacement` effects and fail-closed child discovery.

## History

- `43884d61f` — RED: require the missing production TerminalPolicy surface.
- `a9a431728` — GREEN: bind no-journal terminal decisions to `TerminalPolicy`.
- `7e7fe1d00` — closure: delete fork/family constants and bind family root to production.
- `fba887a3d` — RED: require the missing production Satellite surface.
- `2f0449153` — GREEN: bind Satellite and Distiller identity observations to production.
- `a145572b3` — closure: migrate recovery and hidden-owner consumers; delete dead mirrors.

## Verification

- Fable build: PASS; 737 sources, 164 registered surfaces.
- managed-session-lifecycle: PASS; 125/125.
- focused Satellite/recovery/Distiller: PASS; 12/12.
- `node scripts/check.mjs`: PASS; 699 production files owned, 36 ledger nodes DONE, zero control-pyramid/deadcode/JS-boundary debt, 772 WHAT / 3900 tests traced.

No baseline, suppression, allowlist, threshold or timeout was changed. The declaration count fell because non-executable mirror assertions were deleted.

## Product-semantic conflict discovered

`MANAGED-SESSION-006` says `Retired` is absolutely irreversible. Current `HandleProjection` and its production-bound test deliberately allow an exact retired binding to re-enter `Active` for a new work unit on the same physical child. Batch 7 does not choose between these incompatible rules and does not hide the contradiction. An owner decision must either:

1. make `Retired` irreversible in production and change the same-binding work-unit policy; or
2. revise WHAT/HOW so retirement is terminal for one work unit but the durable identity may begin another.

This is a pre-existing upstream semantic conflict, not introduced by the mirror migration.

## Remaining work

Only `syncDelegateLifecycle` and `ptyLifecycle` remain in `tests/support/managed-surface.mjs`. Batch 8 migrates those consumers and deletes the support file. Batch 9 then upgrades both proof gates to shared AST binding analysis.

PR #25 is stacked on #24. The current account cannot merge upstream PRs; the owner must merge the cumulative chain in dependency order.
