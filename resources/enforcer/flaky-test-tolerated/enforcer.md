# flaky-test-tolerated — Enforcer

## Definition
A flaky test is tolerated when nondeterministic red/green results are accepted as normal rather than treated as a defect in the test or system boundary. The root-cause is that changing verdicts under equivalent inputs are treated as normal, so red loses actionability and green loses evidentiary weight.

## Governing Principle
A test is a measuring instrument. If identical relevant inputs can produce different verdicts, the instrument cannot distinguish product change from measurement noise. Once flakes are normalized, every red result acquires plausible deniability and every green result loses evidentiary weight. The damage is systemic: one unreliable test teaches the team not to trust the suite.

## Trigger When
Trigger when a nondeterministic test is routinely rerun, quarantined without a removal plan, ignored, or described as harmless despite changing verdict under equivalent conditions.

## Do Not Trigger When
- A genuinely stochastic property test has controlled, reproducible randomness and a deterministic pass criterion for the recorded seed.
- A single red result is being investigated as a real defect, not normalized as “just flaky.”
- Timing is injected via a fake clock so the test is deterministic despite time appearing in the domain.
- Order dependence was removed; remaining tests are independent even if they once failed under shuffle.

## Distinguish From
`repeat-until-pass` uses reruns as verification. `time-dependent-test` and `order-dependent-test` name common causes. This rule is the policy failure of accepting nondeterminism itself. Tie-break: if the team treats changing verdicts as normal rather than as a broken instrument, this rule owns the case.

## Decision Procedure
Reproduce with recorded inputs and isolate all hidden sources: time, randomness, shared state, ordering, races, external services. A test is not repaired until one run has one meaning.

## Examples
- positive: CI reruns a failing test until green and the suite is marked passing; the flake stays in the tree.
- near-miss: a property test fails for seed `0xabc`, that seed is recorded, and the assertion is deterministic for it.
- counterexample: remove the hidden input or remove the test; do not teach the suite that red may mean nothing.

## Nudge
A flaky test is a broken instrument. Remove the nondeterminism or remove the test; never teach the suite that red may mean nothing.
