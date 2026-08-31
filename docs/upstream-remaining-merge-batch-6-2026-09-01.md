# Upstream Remaining Merge — Batch 6

PR: https://github.com/PamelaSprin47685ghall/vibe-fs/pull/24

## Result

Batch 6 closes M7A on the cumulative batch-5 head `6bde8890b`. It removes the Handle/fold/join test mirror without changing the Handle lifecycle law.

## Upstream behavior changed

Production business behavior is unchanged. One registered resource boundary was added:

- `Execution/Delegation/Handle/JournalSurface` owns an opaque journal resource;
- open uses the canonical local EventStore and AgentJournal boot path;
- link and abandon call `HandleController`, the production write owner;
- snapshot delegates all record and view projection to `HandleSurface`.

The surface contains representation parsing only. It does not reproduce transition, fold, durability, CAS, or view decisions.

## Proof correction

The deleted support code independently implemented:

- Handle identity and four-state projection transitions;
- listable/joinable/active/reportable views;
- execution-fact replay and idempotence;
- JSON fact codec;
- in-memory journal and HandleController;
- join drain selection.

Those tests could stay green when production behavior diverged. The replacement binds every assertion to registered production code:

- direct transitions and views → `HandleSurface` → `HandleProjection`;
- durable replay → `HandleFoldSurface` → `ExecutionFactFold`;
- fact bytes → `FactCodecSurface` → `FactCodec`;
- first-wins durable abandon → `Handle/JournalSurface` → canonical EventStore → AgentJournal → HandleController.

The previous `WorkActivated → HandleLinked → HandleCompleted` test asserted a locally constructed constant array. It now replays production `HandleLinked` and `HandleCompleted` facts and observes the resulting production joinable projection.

## History

- `17d7b517c` — RED: require a production journal/controller surface.
- `57cdf8feb` — GREEN: add the opaque durable controller proof surface.
- `fe2536b20` — closure: migrate consumers and delete mirror exports.

## Verification

- Fable build: PASS; 735 sources, 162 registered surfaces.
- managed-session-lifecycle package: PASS; 165/165.
- `node scripts/check.mjs`: PASS; 36-node ledger closed, 697 production files owned, zero control-pyramid/deadcode/JS-boundary debt, 772 WHAT / 3940 tests traced.

No baseline, suppression, allowlist, threshold, timeout, or assertion was weakened.

## Remaining work

M7B–M7E still own the transitional Fork/family/terminal, Satellite/Distiller, SyncDelegate, and PTY/process exports. They remain explicit in `managed-surface.mjs` and will be removed cumulatively in batches 7–8. M8/M9 follows in batch 9.

PR #24 is stacked on #23. Upstream merge and rerun permissions are unavailable to the current account; #20–#24 therefore require owner merge in order.
