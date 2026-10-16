# release-ladder-skipped — Lesson

## Opinion

### Definition

The release ladder is skipped when verification jumps to a broad or expensive stage without first clearing the lower-level proofs that isolate simpler classes of failure. The root-cause is that a broader stage is counted as proof while a cheaper applicable rung that isolates a narrower class of failure was never cleared.

### Governing Principle

Verification levels form an information hierarchy. Pure tests prove local logic cheaply; contract tests prove boundaries; replay tests prove history; canaries prove real hosts. A high-level success cannot substitute cleanly for lower proofs because failure there has many possible causes, while success may not exercise the specific property a lower rung targets. The ladder orders evidence from narrow causality to broad realism.

### Trigger When

Trigger when applicable lower gates are bypassed and work proceeds directly to integration, canary, release, or completion claims.

### Do Not Trigger When

- A lower rung is genuinely irrelevant to the change—for example pure content with no runtime surface—and the applicable ladder is still followed.
- The change is already proven at the narrower rung and the current command is the next applicable stage.
- The skipped command does not exist in this project’s ladder and no substitute local proof was omitted.
- Re-running a higher rung after a lower-rung regression was already added and is green.

### Distinguish From

canary-skipped omits one real-boundary rung. unverified-completion-claim lacks sufficient verification generally. This rule is specifically violation of the ordered proof strategy. Tie-break: fire here when a cheaper/narrower applicable proof was skipped; fire canary-skipped when the ordered ladder was followed except the real-host rung; fire unverified-completion-claim when there is no adequate proof at any level.

### Decision Procedure

List which layers the change touches, map each to the narrowest proof that can fail there, and run from cheapest/local to broadest/real before promotion.

### Examples

- positive: a fold algebra change is declared done after a staging deploy, with no unit or property tests of the new law.
- near-miss: a docs-only edit skips runtime tests because the project’s ladder marks them inapplicable.
- counterexample: unit, contract, then canary all run in order for a host-policy change.

## Advice

### What To Do Now

Run the applicable verification rungs in order: pure behavior, contract boundaries, replay/recovery, then real-environment canary or release checks where relevant. Each applicable narrower rung is who owns its class of uncertainty; a later integration or canary is not who owns the skipped local proof.

### Why This Matters

A broad test mixes many causes. When it fails, diagnosis is expensive; when it passes, the specific local invariant may still have escaped exercise. Narrow proofs establish small truths first, so later stages test only what cannot be proven below.

### Repair Strategy

Map the change surface to proof levels and clear each applicable gate before promotion. Keep every discovered regression at the lowest level that can express it faithfully.

### Decision Branches

- If the change touches pure logic, clear the local/unit (and property) rung before integration.
- If the change touches a boundary, add or run contract tests before a full environment.
- If lower rungs are inapplicable, record why and run only the remaining applicable ladder.

### Common Wrong Fixes

- Compensate for missing unit/contract proof by running a larger end-to-end suite repeatedly.
- Skip local tests because “CI will catch it” at a later stage.
- Treat a green canary as proof of an untested local algebra.
- Disable a failing lower rung to reach the next stage faster.

### Verification

Each rung should be green through the project’s standard command before the next rung is treated as meaningful evidence. The invariant is: each applicable narrower proof is established before a broader stage is counted as completion evidence.

### Done When

Completion is supported by an ordered chain of proofs, each responsible for the class of uncertainty only that level can settle.

### Nudge

Do not use a broad test to compensate for missing narrow proofs. Climb from local causality to real-environment evidence so each failure has a small search space.
