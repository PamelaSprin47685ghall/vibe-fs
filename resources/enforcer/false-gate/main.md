# false-gate — Main

## What To Do Now
Add a self-test or known-bad fixture that demonstrates the gate turns red for the exact property it claims to enforce.

## Why This Matters
Green has meaning only as the negation of a possible red. A gate that cannot observe violations is not neutral; it is a confidence amplifier disconnected from reality. Teams then optimize for passing a ritual rather than preserving the invariant.

## Repair Strategy
Trace the gate’s subject discovery, matching logic, exit propagation, and CI wiring. Prove each link with a fixture that should fail, then remove any fail-open behavior that converts “checked nothing” into success.

## Decision Branches
- If the gate can pass while checking zero subjects, fix discovery and fail closed on empty scope when the property requires subjects.
- If a known violation stays green, repair matching, assertions, or exit propagation until that fixture is red.
- If the detector works but CI swallows its failure, that wiring is also this rule until green implies the check ran and could fail.

## Common Wrong Fixes
- Do not merely add more logging or trust a successful manual run on clean code.
- Do not widen the glob without a failing fixture.
- Do not `|| true` the gate “so CI stays green while we iterate.”
- Do not replace a tautological assert with another tautology (`expect(true)`).

## Verification
Run the gate against both a known violation and a known valid case. It must be red for the former and green for the latter through the same standard entry point. The invariant is that green is evidence because red has been demonstrated.

## Done When
A green gate is evidence because its red state has been demonstrated, not because the command happened to exit successfully on today’s tree.
