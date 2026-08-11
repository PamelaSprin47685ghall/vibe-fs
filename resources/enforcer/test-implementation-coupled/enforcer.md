# test-implementation-coupled — Enforcer

## Definition
A test is implementation-coupled when its verdict depends on private structure, helper calls, incidental ordering, internal fields, or algorithm choices that are not part of the supported behavior. The root-cause is that the test freezes private choreography instead of a caller-visible promise, becoming a second implementation rather than an independent specification.

## Governing Principle
A test should constrain what must remain true while leaving implementations free to change what the contract does not promise. Assertions on private structure invert that relation: refactoring correct code becomes expensive while behaviorally wrong code can still pass if it preserves the expected choreography. The test has become a second implementation rather than an independent specification.

## Trigger When
Trigger when tests assert private methods, exact helper call counts, internal object layout, intermediate variables, or incidental algorithm sequence without a caller-visible guarantee requiring them.

## Do Not Trigger When
- The interaction itself is contractual—exactly-once publication, no external call under rejection, or a required transaction boundary—and the test double observes that public effect.
- Assertions target a documented public API or wire contract, including required ordering of that public surface.
- Characterization of a frozen third-party protocol adapter where the protocol sequence is the supported contract.
- White-box tests used only to pin a known-unsafe internal invariant that the public API cannot yet express, and that limitation is recorded.

## Distinguish From
`coverage-theater` asserts too little meaning. `weakened-test-to-pass` deliberately dilutes expectations. Tie-break: if a valid behavioral claim was relaxed to go green, use `weakened-test-to-pass`; if the test freezes private choreography that was never a promise, use this rule.

## Decision Procedure
For each assertion ask whether a conforming alternative implementation may legitimately violate it. If yes, move the assertion outward to the observable contract.

## Examples
- positive: a unit test asserts private helper call counts and intermediate field values for a refactor-safe algorithm.
- near-miss: a test asserts that a rejected command emits zero side-effecting publish calls, which is the public effect.
- counterexample: deleting an edge-case assertion solely because production fails it is `weakened-test-to-pass`.

## Nudge
Tests should freeze promises, not implementations. Assert the behavior or durable interaction the caller owns and leave private decomposition available to refactor.
