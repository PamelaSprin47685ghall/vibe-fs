# failure-path-untested — Main

## What To Do Now
Add a test that induces the real failure condition and asserts the resulting state, error, cleanup, and absence of forbidden side effects.

## Why This Matters
Failure handling often contains the strongest guarantees in the system—no duplicate charge, no leaked permit, no partial commit, no stale mutation. Yet those guarantees are frequently represented only by code branches nobody has executed deliberately. That is specification by optimism.

## Repair Strategy
Inject or arrange the smallest deterministic failure at the owning boundary. Observe public outcomes rather than internal catch blocks, and test cancellation/rollback/retry semantics separately where they differ.

## Decision Branches
- If the failure branch is new or changed and unforced, add a test that induces that exact precondition.
- If a test already drives the same boundary and observables, do not add a redundant catch-block unit test.
- If the defect already escaped once, also record it as `missing-regression-test` while still forcing this path.

## Common Wrong Fixes
- Do not call the handler directly if production reaches it through a different path.
- Do not assert merely that “an error occurred” when cleanup or state preservation is the real contract.
- Do not mock so much that the failure never touches the owning boundary.
- Do not rely on coverage percentage as proof the branch’s semantics were observed.

## Verification
Restore the common failure bug—missing rollback, swallowed error, leaked resource, extra retry—and confirm the test turns red. The invariant is that every newly important failure mode has executable evidence for what the system does and must refrain from doing.

## Done When
Every newly important failure mode has direct executable evidence for what the system does and what it must refrain from doing.
