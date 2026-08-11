# failure-path-untested — Main

## What To Do Now
Add a test that induces the real failure condition and asserts the resulting state, error, cleanup, and absence of forbidden side effects.

## Why This Matters
Failure handling often contains the strongest guarantees in the system—no duplicate charge, no leaked permit, no partial commit, no stale mutation. Yet those guarantees are frequently represented only by code branches nobody has executed deliberately. That is specification by optimism.

## Repair Strategy
Inject or arrange the smallest deterministic failure at the owning boundary. Observe public outcomes rather than internal catch blocks, and test cancellation/rollback/retry semantics separately where they differ.

## Wrong Fixes
Do not call the handler directly if production reaches it through a different path. Do not assert merely that “an error occurred” when cleanup or state preservation is the real contract.

## Verification
Restore the common failure bug—missing rollback, swallowed error, leaked resource, extra retry—and confirm the test turns red.

## Done When
Every newly important failure mode has direct executable evidence for what the system does and what it must refrain from doing.
