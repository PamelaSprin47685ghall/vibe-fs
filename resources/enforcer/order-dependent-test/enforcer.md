# order-dependent-test — Enforcer

## Definition
A test is order-dependent when its verdict depends on which other tests ran before it rather than solely on its own explicit setup and inputs.

## Governing Principle
A test case should be a proposition with local premises. Shared residue adds invisible premises supplied by suite history, so the proposition changes when ordering changes. The suite then stops being a set of independent proofs and becomes one giant stateful scenario whose correctness cannot be localized.

## Trigger When
Trigger when a test passes only after another test, fails in isolation, depends on global/static state, reused databases/files, environment mutation, or cleanup performed elsewhere.

## Do Not Trigger When
- The steps are an intentionally ordered end-to-end scenario modeled and executed as one test with one explicit lifecycle.
- Shared fixtures are immutable and each case still creates its own premises.
- Dependence is only on process-wide constants that tests never mutate.

## Distinguish From
mock-hidden-state hides mutable state inside fixtures. flaky-test-tolerated accepts nondeterminism broadly. Tie-break: if the defect is causal dependence across tests, this rule; if a single mock hides a cursor, mock-hidden-state; if the suite accepts unstable verdicts in general, flaky-test-tolerated.

## Decision Procedure
Run the test alone and under reordered neighbors. Any required state must be created in its own setup and disposed within its own scope, or the scenario should be modeled as a single explicit test.

## Examples
- positive: `test_update` passes only after `test_insert` because both share one database row.
- near-miss: A single `it("creates then updates")` owns the whole lifecycle explicitly.
- counterexample: Each test opens a fresh temp directory and unique ids; order does not change verdicts.

## Nudge
Each test must carry its own premises. Eliminate suite-history dependencies so test order is irrelevant to meaning.
