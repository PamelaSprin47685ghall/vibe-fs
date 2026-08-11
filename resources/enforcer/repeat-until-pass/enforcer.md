# repeat-until-pass — Enforcer

## Definition
Repeat-until-pass treats a lucky successful run as verification after identical or equivalent runs have already failed nondeterministically. The root-cause is that a favorable sample is selected from mixed verdicts, so nondeterminism is treated as noise rather than as a defect in the experiment.

## Governing Principle
A repeated experiment does not become more true because one sample is favorable. If the relevant inputs are unchanged yet verdicts differ, the unresolved fact is nondeterminism. Selecting the green sample is statistical cherry-picking: it discards evidence precisely because that evidence is inconvenient. Correct verification removes the hidden variable, not the red observations.

## Trigger When
Trigger when a failing test/command is rerun until success and the eventual green run is accepted without explaining or eliminating the earlier failure.

## Do Not Trigger When
- A bounded retry of known transient infrastructure outside the system under test is explicit, and final failure remains visible.
- The first failure was explained, the hidden input was fixed, and a subsequent green run is a new experiment under controlled inputs.
- The command is a poll that waits on a causal readiness signal with a timeout, not a rerun of an already-failed assertion.
- A flaky test is being quarantined or deleted as policy work rather than rerun until green to ship.

## Distinguish From
flaky-test-tolerated accepts unstable tests as policy. timeout-inflated-to-pass changes budgets. This rule is the act of sampling repeatedly until the desired verdict appears. Tie-break: fire here when the operator/CI retries until green; fire flaky-test-tolerated when instability is left in the suite as accepted policy; fire timeout-inflated-to-pass when the hidden variable is masked by a larger wait.

## Decision Procedure
Stop after the first unexplained nondeterministic failure. Capture inputs/environment, reproduce, find the hidden variable, and make one run deterministic before accepting green.

## Examples
- positive: a unit test fails twice, then passes on the third identical invocation, and the green run is committed as proof.
- near-miss: CI retries a known GitHub API 429 with a bounded backoff and still fails the job if all attempts error.
- counterexample: after finding a race and adding a lock, a single green run under the fixed inputs is accepted.

## Nudge
Do not choose evidence by outcome. One unexplained red invalidates a lucky green until the nondeterminism has a cause and a fix.
