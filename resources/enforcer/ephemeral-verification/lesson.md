# ephemeral-verification — Lesson

## Opinion

### Definition

Verification is ephemeral when the only evidence for correctness is an unreproducible shell probe, temporary script, manual inspection, or debug output that disappears with the session. The root-cause is that an unreproducible session probe is treated as proof, so the discovered invariant evaporates and cannot turn red when the defect returns.

### Governing Principle

A discovery that cannot be replayed is not part of the engineering system. It may convince one person once, but it cannot defend the invariant tomorrow, teach the next maintainer, or turn red when the defect returns. Durable verification converts private confidence into shared executable memory.

### Trigger When

Trigger when a one-off command, ad hoc script, manual click path, transient log, or local experiment is the sole proof of a behavior or bug fix and is not preserved as a maintained test/gate/canary.

### Do Not Trigger When

- Exploratory probes are followed by encoding their conclusions in durable verification.
- The evidence is non-repeatable forensics that cannot reasonably become a test (a one-time production incident dump).
- The check already lives in the project’s maintained test/gate/canary ladder and the probe was only discovery.
- Reproducing a flake or race locally is an investigation step whose next action is a durable regression, not the final proof.

### Distinguish From

`unverified-completion-claim` has no adequate proof at all. `missing-regression-test` concerns bug fixes specifically. This rule concerns proof that existed but was allowed to evaporate. Tie-break: if a probe demonstrated the property and then vanished, this rule owns the case even if someone “saw it work.”

### Decision Procedure

Ask whether a future maintainer can reproduce the same falsifiable check from the repository alone. If not, preserve the useful part of the probe in the project’s verification ladder.

### Examples

- positive: a fix is “verified” by a one-off curl in the terminal; nothing in the repo can replay that check.
- near-miss: the same curl is used to discover the bug, then a contract test is added that encodes the stimulus and result.
- counterexample: turn the probe into the narrowest durable automated check on the standard entry point.

## Advice

### What To Do Now

Translate the successful manual probe into the narrowest durable automated check that preserves the discovered invariant. The project’s standard test, gate, or canary entry is who owns that invariant; a one-off probe is not.

### Why This Matters

Manual verification proves a moment, not a system. Its setup, commands, and observations disappear, so the next regression begins from ignorance. A repository improves only when debugging knowledge is converted into something that future changes must pass.

### Repair Strategy

Extract the essential stimulus and observable result from the probe. Put them into a unit, contract, replay, integration, or canary test according to the boundary involved. Keep the test deterministic and part of the normal check path.

### Decision Branches

- If the probe discovered an invariant the repo must keep, encode that invariant as a maintained check.
- If the session produced no reusable stimulus/result, do not claim verification; obtain durable proof another way.
- If a bug fix lacks any regression, also apply `missing-regression-test`; this rule is specifically about evaporating proof.

### Common Wrong Fixes

- Do not save a cryptic scratch script nobody runs.
- Do not paste terminal output into a comment and call it a test.
- Do not rely on “steps I remember” as the suite.
- Do not add a skipped or manual-only test that CI never executes.

### Verification

Run the standard project entry point and confirm the new check executes there and fails if the old defect is restored. The invariant discovered in the probe must be the property the durable check guards.

### Done When

The knowledge gained during investigation survives the session as a repeatable guard that future work cannot silently bypass.

### Nudge

If the experiment taught the system something important, make the system remember it. Turn one-off proof into a repeatable test, gate, or canary.
