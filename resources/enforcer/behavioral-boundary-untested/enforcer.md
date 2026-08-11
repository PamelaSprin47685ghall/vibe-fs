# behavioral-boundary-untested — Enforcer

## Definition
A behavior is unverified when tests exercise the machinery beneath a public contract but never cross the contract itself. Correct helpers do not imply a correct boundary.

## Governing Principle
A module is known only by what can be observed through its supported surface. Private functions are lemmas; the public entry point is the theorem. A proof that checks only lemmas can coexist with a broken theorem because wiring, translation, defaults, identity, and effect ordering live precisely at the boundary the test avoided.

## Trigger When
Trigger when a public behavior is covered only through internal helpers, private methods, direct state mutation, or test-only shortcuts while the real caller path remains unexercised.

## Do Not Trigger When
Do not trigger when the relevant public contract is already exercised and lower-level tests merely provide finer localization.

## Distinguish From
contract-test-missing concerns an external system boundary. test-implementation-coupled concerns assertions tied to internals. This rule concerns the absence of any proof through the supported behavioral entrance.

## Decision Procedure
1. Name the behavior promised to callers.
2. Identify the supported entry point that owns that promise.
3. Trace the existing test path.
4. If the test bypasses the owning boundary, add a test through it.

## Nudge
Prove the behavior where callers depend on it. Exercise the real public entry point, not only the helpers that happen to implement it today.
