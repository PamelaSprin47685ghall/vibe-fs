# tool-error-ignored — Enforcer

## Definition
A tool error is ignored when an operation reports that evidence gathering or mutation failed, yet subsequent reasoning proceeds as though the intended observation or change had succeeded.

## Governing Principle
A failed tool call creates an epistemic gap. The desired fact was not established, so any conclusion that depends on it has lost a premise. Continuing can be legitimate only if the error is explicitly classified and an independent source supplies equivalent evidence. Otherwise the workflow silently converts “unknown” into “probably fine.”

## Trigger When
Trigger when commands, tests, patches, searches, builds, or external tools return errors that are skipped without resolution while later steps rely on their intended result.

## Do Not Trigger When
Do not trigger when the failure is consciously classified as irrelevant to the goal, the reason is recorded, and alternate evidence fully discharges the same proof obligation.

## Distinguish From
unverified-completion-claim declares success without enough evidence. false-gate produces misleading green. This rule concerns a concrete red signal that was observed and then discarded.

## Decision Procedure
Name what the failed tool was supposed to establish. Either repair/retry it under understood semantics, or obtain equivalent evidence elsewhere and state why the original failure is non-blocking.

## Nudge
An error removes a premise until accounted for. Resolve it or replace its missing evidence explicitly; never continue as though a failed observation succeeded.
