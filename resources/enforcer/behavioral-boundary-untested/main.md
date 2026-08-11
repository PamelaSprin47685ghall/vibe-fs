# behavioral-boundary-untested — Main

## What To Do Now
Add a behavioral test that enters through the same public surface production callers use and asserts the outcome they are promised.

## Why This Matters
Implementation tests establish facts about today’s decomposition. They do not establish that the system exposes the intended behavior. The distance between helper and boundary contains wiring, normalization, authorization, defaults, effect sequencing, and serialization—the exact places where integration defects hide.

## Repair Strategy
Start from the public promise, not from the internal function you want to cover. Construct the smallest realistic input at the supported entry point, observe the supported result, and keep private tests only where they sharpen diagnosis.

## Decision Branches
- If no test crosses the owning public entry, add that test before adding more helper coverage.
- If helper tests already exist, keep them only as diagnostic complements after the boundary proof is in place.
- If the “public” method is a test-only export, stop using it; test the real supported surface instead.

## Common Wrong Fixes
- Do not invoke private members through reflection to raise coverage.
- Do not export internals only so tests can reach them.
- Do not duplicate the public path inside the fixture while still calling helpers directly.
- Do not treat a green helper suite as proof that wiring, defaults, or identity at the boundary work.

## Verification
Temporarily imagine the internal helper remains perfect but the boundary wiring is broken. The new test must fail under that defect. The invariant is that every promised behavior has at least one proof that would turn red if the owning public entrance were miswired.

## Done When
The promised behavior has at least one proof that crosses its owning public boundary, so a caller-visible regression cannot hide behind green helper tests.
