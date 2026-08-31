# Upstream Remaining Merge — Batch 5

## Result

Batch 5 closes M2, Change publish correctness, against `upstream/master@fcd5ab11b`.

The existing publish algorithm already placed post-rebase review before `publishUnderGate` and restarted the complete loop when the target moved. The missing fact was executable causality: tests called projection classifiers, but could not observe the real program's lease lifetime or prove the fresh witness reached `FfMerge`.

## Upstream behavior changed

1. `OrchestratorProgramDeps` now carries `AcquirePublishGate: unit -> Task<PublishGateLease>` instead of the physical lock path.
2. `OrchestratorRuntime` remains the sole physical lock adapter. It closes over the canonical path, acquires `IntegrationGate`, and returns only a typed release capability.
3. `OrchestratorProgram.publishUnderGate` consumes that capability. No review, repair, Git path, or lock mechanism crosses the boundary.
4. `ChangeSurface` supplies resource-observing ports and drives the real `OrchestratorProgram`; it does not reproduce the publish decision, rebase loop, projection classifier, or witness formula.
5. The external-effect contract now names the actual `AcquirePublishGate` admission symbol after ownership moved out of `Program.fs`.

The publish algorithm's business behavior was not changed. The dependency boundary was narrowed so its behavior can be proven and the runtime remains the only physical lock owner.

## Proof design

RED checkpoint `b54deb210` adds three production-bound counterworlds. All failed on unmodified upstream because the registered surface exposed no program-level observation.

GREEN checkpoint `2896f29f3` proves:

- fresh path: both pre- and post-rebase review see no held lease; only `FfMerge` sees it; acquire/release are exactly one and balanced;
- conflict recovery: same-manager repair and subsequent review see no held lease; only `FfMerge` sees it;
- CAS race: target advances immediately before the first lease grant; the first witness is abandoned, a second rebase and a distinct barrier are produced from the new head, and the only `FfMerge` receives the fresh expected head.

The last counterexample distinguishes the correct loop from two shortcuts that earlier tests allowed: retrying CAS with the stale witness, or merely replacing the expected head without rerunning rebase and review.

## Verification

- `node scripts/build.mjs` — PASS; 734 F# sources, 161 registered surfaces.
- `node scripts/check.mjs` — PASS; 36 gates, zero control-pyramid debt, external-effect registry closed.
- focused Change + external-effect suite — PASS; 114/114.
- production program gate-scope suite — PASS; 3/3.

No baseline, suppression, allowlist, threshold, timeout, or assertion was weakened.

## Remaining work

This PR closes batch 5 / M2 only. Batches 6–9 remain separate. Batch 5 is a planned full-ladder boundary; its final ladder and final upstream refresh are recorded before PR creation.
