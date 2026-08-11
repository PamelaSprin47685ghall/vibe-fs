# tool-error-ignored — Enforcer

## Definition
A tool error is ignored when an operation reports that evidence gathering or mutation failed, yet subsequent reasoning proceeds as though the intended observation or change had succeeded. The root-cause is that a failed observation is treated as an established premise, converting “unknown” into unearned certainty.

## Governing Principle
A failed tool call creates an epistemic gap. The desired fact was not established, so any conclusion that depends on it has lost a premise. Continuing can be legitimate only if the error is explicitly classified and an independent source supplies equivalent evidence. Otherwise the workflow silently converts “unknown” into “probably fine.”

## Trigger When
Trigger when commands, tests, patches, searches, builds, or external tools return errors that are skipped without resolution while later steps rely on their intended result.

## Do Not Trigger When
- The failure is consciously classified as irrelevant to the goal, the reason is recorded, and alternate evidence fully discharges the same proof obligation.
- The failed call was a non-goal probe after the needed evidence was already established independently.
- Negative tests whose purpose is to observe the tool error itself.
- Expected non-zero exits under a documented fallback that then proves the same property another way.

## Distinguish From
`unverified-completion-claim` declares success without enough evidence. `false-gate` produces misleading green. Tie-break: if a concrete red signal was observed and then discarded, use this rule; if completion is claimed with no adequate evidence at all, use `unverified-completion-claim`.

## Decision Procedure
Name what the failed tool was supposed to establish. Either repair/retry it under understood semantics, or obtain equivalent evidence elsewhere and state why the original failure is non-blocking.

## Examples
- positive: a test run fails, the agent ignores stderr, and reports the feature complete.
- near-miss: search fails because a path is absent; the agent records that and proves the same fact by reading the parent listing.
- counterexample: tests were never run and completion is asserted anyway — that is `unverified-completion-claim`.

## Nudge
An error removes a premise until accounted for. Resolve it or replace its missing evidence explicitly; never continue as though a failed observation succeeded.
