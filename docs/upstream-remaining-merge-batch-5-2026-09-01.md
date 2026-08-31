# Upstream Remaining Merge — Batch 5

## Result

Batch 5 closes M2, Change publish correctness, against `upstream/master@fcd5ab11b`. This branch is cumulative: its parent is batch 4 at `8afbaea5b`, not a fresh copy of `upstream/master`.

The existing publish algorithm already placed post-rebase review before `publishUnderGate` and restarted the complete loop when the target moved. The missing fact was executable causality: tests called projection classifiers, but could not observe the real program's lease lifetime or prove the fresh witness reached `FfMerge`.

## Upstream behavior changed

1. `OrchestratorProgramDeps` now carries `AcquirePublishGate: unit -> Task<PublishGateLease>` instead of the physical lock path.
2. `OrchestratorRuntime` remains the sole physical lock adapter. It closes over the canonical path, acquires `IntegrationGate`, and returns only a typed release capability.
3. `OrchestratorProgram.publishUnderGate` consumes that capability. No review, repair, Git path, or lock mechanism crosses the boundary.
4. `ChangeSurface` supplies resource-observing ports and drives the real `OrchestratorProgram`; it does not reproduce the publish decision, rebase loop, projection classifier, or witness formula.
5. The external-effect contract now names the actual `AcquirePublishGate` admission symbol after ownership moved out of `Program.fs`.

The publish algorithm's business behavior was not changed. The dependency boundary was narrowed so its behavior can be proven and the runtime remains the only physical lock owner.

## Proof design

RED checkpoint `9cf1ed463` adds three production-bound counterworlds. All failed on unmodified upstream because the registered surface exposed no program-level observation.

GREEN checkpoint `a23d98c58` proves:

- fresh path: both pre- and post-rebase review see no held lease; only `FfMerge` sees it; acquire/release are exactly one and balanced;
- conflict recovery: same-manager repair and subsequent review see no held lease; only `FfMerge` sees it;
- CAS race: target advances immediately before the first lease grant; the first witness is abandoned, a second rebase and a distinct barrier are produced from the new head, and the only `FfMerge` receives the fresh expected head.

The last counterexample distinguishes the correct loop from two shortcuts that earlier tests allowed: retrying CAS with the stale witness, or merely replacing the expected head without rerunning rebase and review.

## Verification

- `node scripts/build.mjs` — PASS; 734 F# sources, 161 registered surfaces.
- `node scripts/check.mjs` — PASS; 36 gates, zero control-pyramid debt, external-effect registry closed.
- focused Change + external-effect suite — PASS; 114/114.
- production program gate-scope suite — PASS; 3/3.
- cumulative Repository Programming + Change + external-effect suite — PASS; 225/225.
- `npm run format-build-test` — PASS on disposable validation branch `codex/validation-batch-5-cumulative@6eb23996e`: format 696/696 unchanged; 36 static gates; owner dependency scan; build; unit 3935/3935; all requirement integration suites; 273/273 verification harness cases; Host e2e; package integration; `npm pack --dry-run`.

The disposable validation branch contained `90c451bcc`, the already-reviewed Wireit proof correction from the earlier cumulative PR. It is not duplicated in this batch. Without that prerequisite, upstream's stale release test still expects the pre-Wireit command layout even though the production scripts have already moved to Wireit.

The full ladder exposed two inherited batch-4 integration gaps and closed each as a separate commit:

1. `8d1b6e55e` resolves an exclusive-create target once, then reuses the same physical path for write and failure classification. This removes an accidental repeated higher-order invocation and makes the semantic-decorator gate green without inventing a retry policy.
2. `991c0fe63` replaces a source-layout assertion in requirement grounding with a production-bound counterexample. The real grounding observer records both package effects, then throws; the real repository transaction must still commit. Fresh conflict closure remains owned and independently proven by `REPOSITORY-PROGRAMMING-014`.

No baseline, suppression, allowlist, threshold, timeout, or assertion was weakened.

## Remaining work

This PR adds batch 5 / M2 on top of batch 4. Batches 6–9 will continue cumulatively after the preceding PRs are merged. Final upstream refresh and PR identity are recorded in a follow-up documentation commit immediately before publication.
