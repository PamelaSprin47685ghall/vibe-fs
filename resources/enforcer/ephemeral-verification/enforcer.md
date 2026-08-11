# ephemeral-verification — Enforcer

## Definition
Verification is ephemeral when the only evidence for correctness is an unreproducible shell probe, temporary script, manual inspection, or debug output that disappears with the session.

## Governing Principle
A discovery that cannot be replayed is not part of the engineering system. It may convince one person once, but it cannot defend the invariant tomorrow, teach the next maintainer, or turn red when the defect returns. Durable verification converts private confidence into shared executable memory.

## Trigger When
Trigger when a one-off command, ad hoc script, manual click path, transient log, or local experiment is the sole proof of a behavior or bug fix and is not preserved as a maintained test/gate/canary.

## Do Not Trigger When
Do not trigger for exploratory probes whose conclusions are subsequently encoded in durable verification, or for non-repeatable forensic evidence that cannot reasonably become a test.

## Distinguish From
unverified-completion-claim has no adequate proof at all. missing-regression-test concerns bug fixes specifically. This rule concerns proof that existed but was allowed to evaporate.

## Decision Procedure
Ask whether a future maintainer can reproduce the same falsifiable check from the repository alone. If not, preserve the useful part of the probe in the project’s verification ladder.

## Nudge
If the experiment taught the system something important, make the system remember it. Turn one-off proof into a repeatable test, gate, or canary.
