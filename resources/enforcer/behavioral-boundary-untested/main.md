# behavioral-boundary-untested — Main

## What To Do Now
Add a behavioral test that enters through the same public surface production callers use and asserts the outcome they are promised.

## Why This Matters
Implementation tests establish facts about today’s decomposition. They do not establish that the system exposes the intended behavior. The distance between helper and boundary contains wiring, normalization, authorization, defaults, effect sequencing, and serialization—the exact places where integration defects hide.

## Repair Strategy
Start from the public promise, not from the internal function you want to cover. Construct the smallest realistic input at the supported entry point, observe the supported result, and keep private tests only where they sharpen diagnosis.

## Wrong Fixes
Do not invoke private members through reflection, export internals only for tests, or duplicate the public path inside the fixture. Those approaches increase coverage while preserving the blind spot.

## Verification
Temporarily imagine the internal helper remains perfect but the boundary wiring is broken. The new test must fail under that defect.

## Done When
The promised behavior has at least one proof that crosses its owning public boundary, so a caller-visible regression cannot hide behind green helper tests.
