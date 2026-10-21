# D static stopping point

Stopped before implementation after the user narrowed scope to a working flow and recording minor issues. No D directory/source copy, source edit, build or test was performed. C remains frozen and its already-started independent verification can finish. This document does not claim production migration or a closed MF01 contract.

## Prepared protocol, not implemented

- Host explicitly invokes next on an exact execution instance. Guest dynamically looks up the method, preserves receiver and awaits it. Raw returned values stay guest-local.
- invokeNext returns only a single-use outcome reference bound to run, instance, owning offer, invocation and fixed next purpose. Each instance may have only one pending/unconsumed result. Host brands the received reference by identity; clones, cross-run, retired-offer and duplicate projection are rejected.
- Host decides cancellation/deadline admission before projectNext. An admitted projection consumes the exact result before reading its required fields, so a thrown getter does not authorize replay. A closed owner cannot submit new projection work. Already admitted work still belongs to the original process owner.
- projectNext interprets only the production StepCommand fields and returns End, Run, Spawn, Signal, Send, WaitFor or Unsupported. It does not generically serialize the raw result, inspect unrelated fields or invoke toJSON. Physical actions, authorization and actual receipts remain Host responsibilities.
- A dedicated onExit operation dynamically looks up the method on the exact execution instance, preserves receiver and awaits it, then reports fixed Done without inspecting the fulfilled value. Done is callback completion only, not physical scope completion.
- Proof remains the separate existing mechanism. Generic callMethod remains an explicitly limited comparison path, never a D production fallback. Watch reason conversion, complete sanity diagnostics, actual main integration and fuller compatibility remain follow-up work.

## Intended 15-file candidate boundary, never written

The planned new directory was `/private/tmp/vibe-fs-mf01-vertical-d-command-20261021`, copied from frozen C. Planned local edits were owner.mjs, guest.mjs, callable-realm.mjs and appended finite tests in callable.test.mjs. Eleven files would remain unchanged: wire.mjs, host-apis.mjs, candidate.test.mjs, host-deadline.mjs, deadline.test.mjs, src/guest-membrane/Sandbox.fs, its minimal fsproj, scripts/build.mjs, dependencies/ProcessSession.mjs, dependencies/Sandbox.mjs and dependencies/GuestMembrane.mjs. No public API, main source, index or metadata was in scope.

## Saved evidence and follow-up

Detailed prior read-only design: `/private/tmp/vibe-fs-mf01-command-projection-design-20261021.md`. Actual main parser source hash observed during this read was 70d24fe708c88e7ea058df83d20ebe30fcdd84f3be9e8582ed4171ca13b2775d; compiled JsCallLoop hash 7c933e7520da50513b84f2bc7046f08250b6bdd98369dc8b4345118ef2c93f0e. This was only a read of existing artifacts, not a new build or execution.

B's valid class-command rejection and toJSON side-effect incompatibility are confirmed blockers to direct production reception of that isolated mechanism, not a demonstrated regression in the unchanged main flow. D would address those boundaries later; it is not required to prolong this delivery. Queue/ref terminal edges, exact accessor compatibility, Proxy behavior and formatting details remain recorded rather than expanded here.
