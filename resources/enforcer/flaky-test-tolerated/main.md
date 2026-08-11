# flaky-test-tolerated — Main

## What To Do Now
Find the hidden input that changes the verdict—time, random seed, order, shared residue, race, external state—and make it explicit or remove it. The test as a measuring instrument is who owns one-input-one-verdict; retries, quarantine labels, and widened timeouts are not.

## Why This Matters
A nondeterministic test cannot serve as evidence. More importantly, tolerated flakes poison interpretation of the whole suite: engineers begin rerunning failures instead of investigating them, turning probability into a substitute for causality.

## Repair Strategy
Record seeds, inject clocks, isolate storage/global state, control concurrency, and replace external dependencies with deterministic contracts where appropriate. Keep a failing reproduction until the cause is removed.

## Decision Branches
- If a hidden input (time, seed, order, race, shared state) exists, make it explicit or eliminate it until one input yields one verdict.
- If the test cannot be made deterministic, delete or replace it; do not quarantine forever.
- If the policy is “rerun until green,” stop that policy; that is also `repeat-until-pass`.

## Common Wrong Fixes
- Do not raise retry counts or widen timing windows.
- Do not quarantine indefinitely or label the test “known flaky.”
- Do not skip the test on CI while keeping it as supposed coverage.
- Do not seed `sleep` to “make it usually pass.”

## Verification
Run the repaired test repeatedly only as a confidence check after removing the identified nondeterminism; the actual proof is the causal fix plus a deterministic assertion. The invariant is that equivalent inputs yield one verdict.

## Done When
Equivalent inputs yield one verdict, a red result is actionable again, and nobody needs luck or reruns to establish correctness.
