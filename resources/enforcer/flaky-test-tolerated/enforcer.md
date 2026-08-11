# flaky-test-tolerated — Enforcer

## Definition
A flaky test is tolerated when nondeterministic red/green results are accepted as normal rather than treated as a defect in the test or system boundary.

## Governing Principle
A test is a measuring instrument. If identical relevant inputs can produce different verdicts, the instrument cannot distinguish product change from measurement noise. Once flakes are normalized, every red result acquires plausible deniability and every green result loses evidentiary weight. The damage is systemic: one unreliable test teaches the team not to trust the suite.

## Trigger When
Trigger when a nondeterministic test is routinely rerun, quarantined without a removal plan, ignored, or described as harmless despite changing verdict under equivalent conditions.

## Do Not Trigger When
Do not trigger for a genuinely stochastic property test whose randomness is controlled/reproducible and whose pass criterion is deterministic for the recorded seed.

## Distinguish From
repeat-until-pass uses reruns as verification. time-dependent-test and order-dependent-test name common causes. This rule is the policy failure of accepting nondeterminism itself.

## Decision Procedure
Reproduce with recorded inputs and isolate all hidden sources: time, randomness, shared state, ordering, races, external services. A test is not repaired until one run has one meaning.

## Nudge
A flaky test is a broken instrument. Remove the nondeterminism or remove the test; never teach the suite that red may mean nothing.
