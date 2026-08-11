# coverage-theater — Enforcer

## Definition
Coverage theater occurs when execution metrics are treated as evidence of correctness even though the tests assert little or nothing that distinguishes correct behavior from plausible defects. The root-cause is that reachability metrics are treated as proof of correctness, so tests can stay green under defects that never violate an assertion.

## Governing Principle
Coverage measures reachability, not truth. A line can execute under a test that would remain green if its result were inverted, its identity corrupted, or its error swallowed. Verification begins only where a test states a proposition capable of being false. The value of an assertion is therefore not that it exists, but that a realistic defect would violate it.

## Trigger When
Trigger when tests increase line/branch/function coverage yet omit meaningful values, identities, invariants, ordering, or failure outcomes.

## Do Not Trigger When
- Coverage is used only as a navigation signal and behavioral assertions independently prove the relevant contract.
- A test asserts a caller-visible proposition that would fail under a realistic defect, even if it also raises coverage.
- Measuring coverage in CI as a heatmap does not by itself constitute theater if it is not treated as the proof.
- Property or contract tests that exercise many branches while asserting invariants are not metric substitutes.

## Distinguish From
`weakened-test-to-pass` removes meaningful expectations. `test-implementation-coupled` asserts the wrong surface. This rule mistakes traversal of code for proof about code. Tie-break: if greenness comes from execution counts rather than a falsifiable proposition, this rule owns the case.

## Decision Procedure
For each test, ask: which plausible defect makes this assertion fail? If no concrete defect can be named, the test is observation without judgment.

## Examples
- positive: a test calls every branch, asserts `expect(result).toBeDefined()`, and a swapped return still passes.
- near-miss: coverage reports are consulted after tests that already assert identity, ordering, and failure outcomes.
- counterexample: replace metric-only tests with assertions that fail if the caller-visible result, identity, or invariant is wrong.

## Nudge
Execution is not verification. Assert a property whose violation would matter to a caller, and make the test capable of turning red under a realistic defect.
