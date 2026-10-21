# MF-01 next boundary: explicit STOP/CONT signal requests

Decision after review with root: retain this as a prerequisite design and counterexample matrix; do not publish an unconsumed public STOP/CONT extension now. If the concrete child-process guest owner is selected and implemented, its exact physical need can justify the small request-only capability in the same reception sequence. Do not wire the present main vm park implementation to POSIX signals, introduce a Paused phase, or count the proposed primitive as MF-01 sandbox completion.

This assessment is read-only. No new native experiment, build, source change, metadata change, or commit was performed.

## Existing consumers and contract

Published library HEAD is `45849f51778f6a12dcafbc12ce1c8c7f7f65502b` (0.11.0). `src/process/AGENTS.md` LEMMA 2 says signals target only the session's POSIX group while the direct child has not been observed exiting. The result is a request acknowledgement, never an exit observation. `ProcessSession.fs:213` permits TERM/KILL/INT/HUP; `Native.fs:99` uses negative pid for the owned group. ProcessState is Starting/Running/Draining/Closed. SupervisedProcess forwards the same signal method and imports SignalName from process.

There is no current production STOP/CONT consumer. Main `JsSandbox.suspendProgram` installs a deferred Promise; `ToolWorkflow.unparkSite` resolves that original continuation and resets its deadline; `abandonSite` rejects the deferred and waits for finally and ledger settlement. This must continue to satisfy repository-programming WHAT006 and WHAT032..039/041. It must not be replaced by merely stopping an arbitrary Host/worker thread.

The concrete prospective consumer is the single-run child-process guest owner described by `evidence/module-construction-20261021/sandbox-owner-contract.md:84..97`. Existing Node/Bun raw POSIX probes demonstrated original-closure preservation, but used parent process.kill, outside the public ProcessSession capability. The interrupted long park run observed about 19m46 of stopping; it is not a one-hour pass or public STOP/CONT interface proof.

## Minimal signal semantics

Add only SIGSTOP and SIGCONT to the existing whitelist and exported SignalName. Keep the same Requested/NotRunning/Failed result, unchanged raw signal strings, owned negative-PGID dispatch, and the existing refusal after observed direct exit. Repeating a request sends that request; do not synthesize an observed transition or automatically add a second signal. No retry and no new lifecycle ledger.

While the OS has stopped a target, the library's existing phase remains Running and exit remains null. A caller must independently observe its exact owned process identity and stopped state before claiming physical suspension. Neither Requested, lack of output, a heartbeat gap, nor wait TimedOut establishes Paused. After CONT, a bound continuation/business receipt establishes progress; Requested does not establish that either.

The small extension must not expand the model-facing this.call signal algebra (WHAT050 still lists TERM/KILL/INT/HUP) or grant guest code access to its host ProcessSession. It is an owner-side primitive.

## Ownership and shutdown risks

- STOP targets the entire owned group, including same-group descendants. A SupervisedProcess call also stops its keeper, target, and same-group forwarding; external parent cleanup remains essential. Do not migrate a guest to Supervision merely to obtain this feature, or erase existing UNKNOWN_TARGET_EXIT behavior after hard KILL.
- A stopped child cannot consume stdin. A small successful write confirms only first-hop pipe acceptance. Large unpark values can block write/flush; a protocol that awaits consumption before CONT can deadlock. The later guest owner needs explicit bounded control staging, input framing, and release ordering rather than a new arbitrary product size limit.
- Unpark must transfer the new deadline, park generation, and injected value in an order that prevents an old runnable continuation executing under stale authority. STOP/CONT alone provides no such atomic control protocol.
- TERM sent to a stopped target need not yield exit before it resumes. KILL must remain available during stopping and the parent must await actual exit plus each output EOF. Requested termination and discardOutput do not close ownership.
- Preserve direct-exit/Draining refusal for both new signals. No post-exit old-PGID control, no raw caller-supplied pid, no second current-by-pid owner map. Descendants that escaped the group remain outside this capability.
- Host death does not auto-resume or reap a stopped group; there is no destructor or parent-death guardian in ProcessSession. The later sandbox owner must state its crash responsibility explicitly.

## Finite Node22 and Bun acceptance matrix

1. Base-red: both public signal calls reject as Unsupported on 0.11.0. Candidate accepts the same typed names while unrelated names remain rejected without native dispatch.
2. Actual owned child and real same-group grandchild announce readiness and identities. STOP returns Requested; independently inspect both for stopped state. Assert ProcessState still Running, exit null, wait TimedOut, and a separate foreign control process still progresses.
3. Queue one nonce-bound small input while stopped. Written means first hop only; require no application receipt before CONT. After CONT observe the same child identity and original closure state (e.g. 41 to 42), exactly one matching receipt, ordinary exit code, complete dual-channel bytes and EOF. No relaunch or reconstructed continuation.
4. Repeat STOP and CONT requests without emitting exit/Closed. Do not assume an immediate native scheduling transition. Verify an active wait remains pending until actual closure and its registration is reclaimed.
5. STOP then TERM does not imply closed. STOP then KILL must close the real child and same-group descendant; preserve both output channels, terminal signal, and final ownership cleanup. Exercise Capture with a pending output read or bounded stdin write to exclude stranded waits.
6. After direct child exit with inherited output still open (existing draining fixture), both new names return NotRunning; independently demonstrate no new STOP/CONT was sent to its leftover group. After Closed and failed startup, likewise no dispatch.
7. Deterministic native signal failure (ESRCH/EPERM seam) returns Failed with the original operation/code and does not manufacture a lifecycle transition or retry. Treat this as error injection, distinct from actual OS stop/continue proofs.
8. SupervisedProcess's automatically widened type/forwarding is exercised on real keeper and target, followed by actual final extent/EOF/cleanup. Do not count target PID alone as its whole group.
9. A real installed tarball outside the workspace runs the new cases on Node22 and Bun with no compiler or unrelated package modules. Strict TypeScript accepts STOP/CONT on ProcessSession and SupervisedProcess, rejects a non-signal, and preserves the unchanged phase and request-result unions. Current stock process-package.test.mjs is Linux-only; on this macOS machine a skip cannot supply this proof.

All native scenarios need independent finite cleanup in finally; KILL works without first trusting a pending CONT acknowledgement. Record actual runtime versions, exact PIDs and group membership, exits/EOF, pass/fail/skip counts, and no live owned processes. Reuse no raw historical PID.

## Proposed future write sets, not currently authorized

Core behavior/documentation:

- `src/process/ProcessSession.fs`: add only the two signal strings.
- `src/process/index.d.ts`: widen SignalName; explicitly retain request-only meaning.
- `src/process/AGENTS.md` and `PROCESS.md`: preserve LEMMA 2 and document STOP/CONT without Paused claims, along with the stopped-input and external owner obligations.
- `src/process/tests/004.test.mjs` and its existing `tests/worker.mjs`: the finite physical stop/resume/group/drain cases and original-closure readiness barrier.
- `src/supervision/tests/003.test.mjs`: inherited signal behavior through the actual keeper/target composition. No supervision production/type rewrite is needed because it already forwards SignalName.

Package proof can remain in a temporary installed consumer for the first isolated candidate. If persisted in the repository, the separate exact follow-up is `scripts/tests/process-consumer.{mjs,ts}` and `scripts/tests/supervision-consumer.{mjs,ts}` (plus the existing process-only package harness only if its Linux-only observation is deliberately made portable, not simply unskipped).

Public package metadata/version and full artifact continuity remain root's serial work. No main file is part of this signal-only package.

## Why this precedes, rather than replaces, sandbox work

The complete independent sandbox still needs host-authoritative synchronous and asynchronous RPC, all Callable constructors/getters/thunks/callbacks confined to the guest, cancel/deadline leases and reset, park-generation control, unpark ordering, finally/abandon cleanup, ledger settlement, and capability rechecks. A STOP/CONT extension closes none of these individually. It reduces the need for a future owner to bypass the physical library using raw pid calls and makes one real prerequisite independently falsifiable. If the child-process design is later rejected, the product has gained an honest POSIX owner capability rather than a partially wired sandbox or a false Paused abstraction.
