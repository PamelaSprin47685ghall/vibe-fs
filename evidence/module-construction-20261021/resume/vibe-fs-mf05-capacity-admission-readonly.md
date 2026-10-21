# MF05 capacity / queues: retain current production owners this round

Recommendation: no new package reception for these three areas in this round. No source, metadata, API, build, or commit was changed. This is a source-contract review, not a runtime benchmark or a newly completed full-chain proof.

Fixed reference library: `202a41ab39460980ba053e55a9b3780bbaf411ca`. Fixed main adapter reference: `5880b824f52d6f7bab5ac36dfcb7eff9966fdc41`. Current published library: `45849f51778f6a12dcafbc12ce1c8c7f7f65502b` (0.11.0).

## Contract and actual consumers

| Area | Actual current authority | Fixed pure package | Decision |
| --- | --- | --- | --- |
| Capacity reconciliation | `ModelCapacity/Model.fs` seven ordered failure predicates; only `ModelRoutingSurface.reconcileCapacityEvidence` calls them | `src/capacity/CapacityReconciliation.fs` repeats those same predicates over JS DTO input | Retain. There is no production scheduler/release caller, and no demonstrated functional gain. |
| Provider-step waiting | `CapacityStepDemandQueue` in `ModelCapacity/Queue.fs`, owned by `BorrowingCapacity` and its grant/recall/retirement logic | No separate `src/queue` package exists in fixed source | Retain with borrowing. Do not label execution-admission queue as a replacement for provider-step waits. |
| Execution admission waiting | `ExecutionAdmissionQueue` in `ModelCapacity/Queue.fs`; `ModelRoutingRuntime` owns one instance and performs route/acquire/drain/cancel under its gate | `src/admission-queue/AdmissionQueue.fs` owns FIFO, current-by-session, reuse/supersession, bounded count, take/withdraw/drain | An independent migration is technically possible, but currently offers equivalent behavior without a demonstrated functionality or cost benefit. Defer. |

This is not a claim that all three require a joint migration. Execution admission waiting has a separable boundary; provider-step waiting does not have a corresponding fixed standalone owner. Keep that distinction in the plan.

## Why capacity does not justify an entry

The seven checks (active bounds, token counts, credit set divergence, token owner, waiter owner, execution custody, negative counters) and their fixed ordering are the same in current Model.fs and fixed CapacityReconciliation.fs. `rg` over main source finds a single call to `CapacityReconciliation.decide`, in `ModelRoutingSurface.fs:849`. Production `ModelRoutingRuntime` constructs snapshots but does not apply this pure rule to schedule, repair, or close resources.

The fixed package strictly admits safe JS number values and converts them to int64. Main's typed evidence uses int64 for credits and diagnostic counters. A bridge through float would discard exact adjacent values above 2^53; the already-received permit package deliberately avoids this. The package's malformed DTO rejection is useful at its public input boundary but supplies no new real production observation in this configuration. Do not add a new production reconciliation call as part of extracting a pure rule, because that would introduce an unreviewed fail-closed policy change.

## Why the two queues must stay distinct

Execution admission queue entries contain role/participant/purpose/lender/previous target and complete an `ExecutionAdmissionAcquisition`. They are one pending demand per session and use exact physical identity for reuse, cancellation and supersession. A full queue refuses a new session but permits an existing queued session to change physical generation at the tail. Scheduling and lease issuance remain in ModelRouting, including scheduler exceptions poisoning all pending waiters.

Provider-step demand entries instead carry target, visibility fence, request key, TryOrdinary callback and `TaskCompletionSource<unit>`. BorrowingCapacity owns their sequence, exact request reuse, token availability, recall, completion/cancellation and draining. Treating the admission package as a generic container for these entries would retain the real owner outside and add an adapter without transferring the grant rules.

CapacityLedger still owns the one credit occurrence set. BorrowingCapacity still owns token Idle/InFlight/Retiring state and exact owned/borrowed custody. ExecutionCapacityOwner (the current name; no `PermitRuntime` symbol was found in current or fixed main source) now projects the public exact `ExecutionPermits` owner through a representation-only WeakMap. ChatAdmissionTransaction owns acceptance, target projection, commit and pre-provider terminal/release settlement. A queue extraction must not merge or replace these independent responsibilities.

## Closest future candidate: admission-queue only

The fixed library's FIFO/reuse/supersession/take/withdraw/drain laws match the current local implementation. No current user-visible counterexample was found by reading ModelRouting's complete call sites and requirement 012/013/014. Its value would be a separately testable single mutable owner, rather than fixing one of the current known P1s.

The fixed 5880 adapter is not ready to copy as-is: it keeps host `nextNodeId` and `nextFence`, increments them separately, converts them through float, and checks them against the library's number counters. That is mirrored identity minting, not a completed single-owner migration. Library sequence/node/fence counters are floats without a pre-mutation safe-limit guard. This is a long-horizon exactness limitation, not a claim that the running product reached 9e15 enqueues.

If later accepted, remove current `ExecutionAdmissionQueue.demands`, `bySession`, `nextAdmissionDemandSequence`, `nextNodeId`, and `nextFence`. The library alone must issue node identity and decide current/FIFO/withdraw/take. Main may keep a raw frozen-node-to-typed projection and its task completion, and must still check the exact typed reference before acting. Do not add a second current-by-session table or lifecycle ledger. Define exact int64 wire behavior first, as done for permits, instead of using an unchecked float bridge or a hidden counter test seed.

### Proposed future exact source set

Library candidate, if explicitly selected:

- `src/admission-queue/AdmissionQueue.fs`
- `src/admission-queue/index.d.ts`
- `src/admission-queue/AGENTS.md`
- `src/admission-queue/Wanxiangli.AdmissionQueue.fsproj`
- `src/admission-queue/tests/001.test.mjs` plus one numbered rule-test file only if new exactness cases need a separate existing SPEC anchor.

Main bridge:

- `src/Wanxiangshu/OpenCode/Host/ModelCapacity/Queue.fs`: only execution-admission owner replacement; CapacityStepDemandQueue remains unchanged.
- `src/Wanxiangshu/OpenCode/Host/ModelCapacity/Model.fs` and `.fsi`: only exact raw-node representation/projection helpers and removal of unused node/fence mint helpers, after a whole-tree consumer audit.
- Existing requirement files `execution-model-routing/tests/012.test.mjs`, `013.test.mjs`, `014.test.mjs`: preserve current formal coverage and add narrow owner-transfer regressions.

Queue.fsi and ModelRouting.fs should need no public behavior/signature change. If design reveals otherwise, expand the exact set before editing. Library metadata/version/export inventory, main gitlink and lock remain root's serial responsibility, never bundled with borrowing or capacity-rule extraction.

### Required actual reception evidence

- Same physical retry returns the same raw node and typed task/handle, preserving original payload. Same physical with wrong role/participant/purpose is still rejected by existing ModelRouting identity checks.
- Full queue plus physical replacement completes the old demand Superseded exactly once and places the new one after existing peers; no lost slot, phantom capacity or pending waiter.
- Old node/physical cancellation, fake typed wrapper, foreign queue node, and late take cannot remove or complete a newer demand. Genuine current take/admit has a positive control.
- Scheduler exception drains every actual task once and keeps future demands failed; all original error identity/counter behavior survives.
- Read snapshots, successful/duplicate/stale/conflict counters and exact lease projection remain coherent. No settlement side effect is added to peek/snapshot.
- Actual producer to Accepted evidence to queued admission to Host target projection/receipt to permit commit/release remains covered by the existing ChatAdmission/Host tests. A raw queue unit test alone does not prove this chain.
- Node22 and Bun independent module and actual installed tarball/strict TS consume must be verified. Exact-value boundary cases must use genuine public values; do not run or pretend to run 9e15 allocations, and do not introduce a public counter seed.

None of this future candidate is implemented by this read-only decision. Current recommendation remains retain, with MF01 physical owner and exact Host interruption gaps ahead of a behavior-equivalent queue migration.
