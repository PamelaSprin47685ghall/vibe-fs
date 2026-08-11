# canary-skipped — Main

## What To Do Now
An undocumented Host assumption lacks a canary. Prove it against the real boundary before release.

## Repair Strategy
1. Confirm the tip applies at the real boundary, not a symptom downstream.
2. Restore the missing name, ownership, test, or control so the ScoreWhen condition no longer holds.
3. Prefer one canonical fix over a local workaround that leaves the invariant broken.

## Decision Branches
- If the root cause is a missing domain type or named case: introduce the type at the boundary and migrate callers.
- If the root cause is a collapsed or bypassed boundary: restore the interface and stop sharing internals.
- If the root cause is missing proof: add a durable assertion, contract test, or canary that would fail under a realistic defect.
- If the change is destructive or speculative: stop, establish authority and the true owner first.

## Wrong Fixes
- Papering over the symptom with another flag, catch-all, facade, or compatibility shim.
- Leaving dual paths, commented-out code, or ephemeral probes as the only record of the fix.
- Testing private helpers instead of the supported entry point when the contract is public.

## Verification
- Re-read the changed boundary and confirm the ScoreWhen condition is gone.
- Exercise the success path and the relevant failure, cancellation, or near-miss path.
- Ensure no duplicate source of truth, silent catch-all, or unowned helper remains.

## Done When
- The nudge is applied at the owning boundary.
- Callers and proofs use the canonical representation.
- A reviewer can see why the tip no longer fires without relying on tribal knowledge.

## Scope and Authority
- Touch only the owning module, contract, and directly affected callers.
- Do not expand into unrelated cleanup, renames, or framework churn.
- Destructive actions require explicit authority and a verified target.
