# flaky-test-tolerated — Main

## What To Do Now
Find the hidden input that changes the verdict—time, random seed, order, shared residue, race, external state—and make it explicit or remove it.

## Why This Matters
A nondeterministic test cannot serve as evidence. More importantly, tolerated flakes poison interpretation of the whole suite: engineers begin rerunning failures instead of investigating them, turning probability into a substitute for causality.

## Repair Strategy
Record seeds, inject clocks, isolate storage/global state, control concurrency, and replace external dependencies with deterministic contracts where appropriate. Keep a failing reproduction until the cause is removed.

## Wrong Fixes
Do not raise retry counts, widen timing windows, quarantine indefinitely, or call a test “known flaky.” Those actions reduce pain by reducing the authority of the test suite.

## Verification
Run the repaired test repeatedly only as a confidence check after removing the identified nondeterminism; the actual proof is the causal fix plus a deterministic assertion.

## Done When
Equivalent inputs yield one verdict, a red result is actionable again, and nobody needs luck or reruns to establish correctness.
