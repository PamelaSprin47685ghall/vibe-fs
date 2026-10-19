# tool-error-ignored — Lesson

## Opinion

### Definition

A tool error is ignored when an operation reports that evidence gathering or mutation failed, yet subsequent reasoning proceeds as though the intended observation or change had succeeded. The root-cause is that a failed observation is treated as an established premise, converting “unknown” into unearned certainty.

### Governing Principle

A failed tool call creates an epistemic gap. The desired fact was not established, so any conclusion that depends on it has lost a premise. Continuing can be legitimate only if the error is explicitly classified and an independent source supplies equivalent evidence. Otherwise the workflow silently converts “unknown” into “probably fine.”

### Trigger When

Trigger when commands, tests, patches, searches, builds, or external tools return errors that are skipped without resolution while later steps rely on their intended result.

### Do Not Trigger When

- The failure is consciously classified as irrelevant to the goal, the reason is recorded, and alternate evidence fully discharges the same proof obligation.
- The failed call was a non-goal probe after the needed evidence was already established independently.
- Negative tests whose purpose is to observe the tool error itself.
- Expected non-zero exits under a documented fallback that then proves the same property another way.

### Distinguish From

`unverified-completion-claim` declares success without enough evidence. `false-gate` produces misleading green. Tie-break: if a concrete red signal was observed and then discarded, use this rule; if completion is claimed with no adequate evidence at all, use `unverified-completion-claim`.

### Decision Procedure

Name what the failed tool was supposed to establish. Either repair/retry it under understood semantics, or obtain equivalent evidence elsewhere and state why the original failure is non-blocking.

### Examples

- positive: a test run fails, the agent ignores stderr, and reports the feature complete.
- near-miss: search fails because a path is absent; the agent records that and proves the same fact by reading the parent listing.
- counterexample: tests were never run and completion is asserted anyway — that is `unverified-completion-claim`.

## Advice

### What To Do Now

Resolve the failed tool operation or replace the missing evidence with an explicit alternative before continuing any conclusion that depended on it. The failed tool’s intended proof obligation is who owns whether later conclusions may proceed; a red signal cannot be inherited as success.

### Why This Matters

A tool failure is not merely an inconvenience; it changes what is known. Ignoring it causes later steps to inherit a premise that was never established, so a polished final result can rest on a silent hole in the evidence chain.

### Repair Strategy

Classify the failure, inspect its cause, rerun only when retry semantics are sound, or use an independent source that proves the same property. Record non-blocking rationale when the failed operation was genuinely irrelevant.

### Decision Branches

If later conclusions depend on the failed operation’s intended result, repair, retry under sound semantics, or replace it with equivalent evidence.
If the failure is irrelevant and another source already proves the same property, record that classification and continue.

### Common Wrong Fixes

- Hide stderr, append `|| true`, or treat a later unrelated success as covering the failed check.
- Retry blindly without understanding whether retry is safe or idempotent.
- Quote a previous green run as if it replaced the failed current observation.

### Verification

Invariant: every observed error ends resolved or explicitly superseded by equivalent evidence with a stated reason. No later conclusion may assume success of a tool that reported failure.

### Done When

No conclusion depends on the imagined success of a tool that actually reported failure.

### Nudge

An error removes a premise until accounted for. Resolve it or replace its missing evidence explicitly; never continue as though a failed observation succeeded.
