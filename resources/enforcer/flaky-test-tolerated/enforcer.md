# flaky-test-tolerated — Enforcer

## Definition
A flaky test is not “a test that occasionally fails.” It is a measuring instrument whose verdict changes while the relevant inputs are treated as equivalent — and the organization has decided to live with that ambiguity.

The rule fires on **tolerance**, not first discovery. One unexplained intermittent red is evidence to investigate. A flake becomes institutional debt when reruns, quarantine, folklore, or selective disbelief turn nondeterminism into normal operating procedure.

## Governing Principle
A test has two jobs: detect a meaningful difference and make the verdict interpretable.

If the same relevant state can produce red or green, the suite can no longer tell product change from measurement noise. The first casualty is not CI speed; it is epistemology. Every future red now has an escape hatch: “probably flaky.” Every future green carries a discount: “maybe we got lucky.”

One tolerated flake teaches engineers a habit more expensive than the test itself — **distrust red until rerun**. Once that habit spreads, real regressions receive the same treatment as noise.

## Trigger When
Trigger when known nondeterminism remains accepted in a test that is still presented as evidence, including:

- CI automatically reruns a failing test and reports only the eventual green verdict;
- the team routinely says “rerun it” before investigating the first red;
- a test is quarantined, skipped, or labeled “known flaky” with no bounded owner/exit plan while its supposed coverage is still counted;
- timing windows are widened repeatedly without identifying the hidden input;
- failures depend on order, clock, random seed, shared residue, race, resource pressure, or external service state, and that source remains uncontrolled;
- reviewers accept “passes most of the time” as sufficient for a deterministic contract.

## Do Not Trigger When
- A stochastic/property test records its random seed and a given seed produces a deterministic verdict.
- A single intermittent failure is actively being treated as a defect and investigated rather than normalized.
- A test intentionally verifies probabilistic behavior with a statistically defined contract appropriate to the product; nondeterminism is then part of the specification, not accidental measurement noise.
- A truly external transient is retried by an explicitly modeled infrastructure policy, while correctness assertions themselves remain deterministic and final failure remains visible.

## Distinguish From
`repeat-until-pass` is the act of selecting a favorable sample from mixed outcomes. `flaky-test-tolerated` is the broader policy failure: the unstable instrument is allowed to remain authoritative.

`time-dependent-test`, `order-dependent-test`, `random-source-in-logic`, and shared-state rules often identify the mechanism. Use those when the cause is known and specific; keep this rule when the central wound is that nondeterministic verdicts have been normalized.

## Decision Procedure
Ask whether two runs that the test suite considers “the same experiment” can produce different verdicts.

If yes, identify the hidden input: clock, seed, scheduling, order, process lifetime, network state, shared database, filesystem residue, port allocation, environment, resource pressure.

Then ask the policy question: is the suite still treating this test as trustworthy evidence without controlling that input? If yes, this rule applies.

## Examples
- positive: CI retries a test up to three times; the job is green if any attempt passes, and the flake remains indefinitely.
- positive: a test occasionally fails under parallel execution, so the suite is forced to serial mode rather than repairing leaked shared state; the test is still called reliable.
- positive: a “temporary” quarantine has no owner, date, or exit criterion six months later.
- near-miss: an intermittent failure appears once, is preserved, and investigation finds an unseeded random choice; the seed is made explicit before the test returns to trusted status.
- counterexample: a property test reports seed `0xabc`; rerunning that exact seed deterministically reproduces the failure.

## Nudge
A flaky test is a broken instrument, not an eccentric coworker.

Fix the hidden input or retire the instrument. Do not train the organization to negotiate with red.
