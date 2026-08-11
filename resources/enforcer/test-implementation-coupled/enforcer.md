# test-implementation-coupled — Enforcer

## Definition
A test is implementation-coupled when its verdict depends on private structure, helper calls, incidental ordering, internal fields, or algorithm choices that are not part of the supported behavior.

## Governing Principle
A test should constrain what must remain true while leaving implementations free to change what the contract does not promise. Assertions on private structure invert that relation: refactoring correct code becomes expensive while behaviorally wrong code can still pass if it preserves the expected choreography. The test has become a second implementation rather than an independent specification.

## Trigger When
Trigger when tests assert private methods, exact helper call counts, internal object layout, intermediate variables, or incidental algorithm sequence without a caller-visible guarantee requiring them.

## Do Not Trigger When
Do not trigger when an interaction itself is contractual—for example exactly-once publication, no external call under rejection, or a required transaction boundary—and the test double observes that public effect.

## Distinguish From
coverage-theater asserts too little meaning. weakened-test-to-pass deliberately dilutes expectations. This rule asserts details that should remain free.

## Decision Procedure
For each assertion ask whether a conforming alternative implementation may legitimately violate it. If yes, move the assertion outward to the observable contract.

## Nudge
Tests should freeze promises, not implementations. Assert the behavior or durable interaction the caller owns and leave private decomposition available to refactor.
