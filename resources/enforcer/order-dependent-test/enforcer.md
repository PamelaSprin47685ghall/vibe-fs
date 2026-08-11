# order-dependent-test — Enforcer

## Definition
A test is order-dependent when its verdict depends on which other tests ran before it rather than solely on its own explicit setup and inputs.

## Governing Principle
A test case should be a proposition with local premises. Shared residue adds invisible premises supplied by suite history, so the proposition changes when ordering changes. The suite then stops being a set of independent proofs and becomes one giant stateful scenario whose correctness cannot be localized.

## Trigger When
Trigger when a test passes only after another test, fails in isolation, depends on global/static state, reused databases/files, environment mutation, or cleanup performed elsewhere.

## Do Not Trigger When
Do not trigger for an intentionally ordered end-to-end scenario modeled and executed as one test with one explicit lifecycle.

## Distinguish From
mock-hidden-state hides mutable state inside fixtures. flaky-test-tolerated accepts nondeterminism broadly. This rule is specifically cross-test causal dependence.

## Decision Procedure
Run the test alone and under reordered neighbors. Any required state must be created in its own setup and disposed within its own scope, or the scenario should be modeled as a single explicit test.

## Nudge
Each test must carry its own premises. Eliminate suite-history dependencies so test order is irrelevant to meaning.
