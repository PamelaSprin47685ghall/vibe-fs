# guessed-not-verified — Main

## What To Do Now
Identify the factual premise that the current decision relies on and verify it against the source that actually owns that fact.

## Why This Matters
A guessed premise contaminates every downstream conclusion while often remaining invisible because the reasoning itself looks coherent. The cost of checking early is usually tiny compared with repairing a design built on an API, file, lifecycle, or failure model that never existed.

## Repair Strategy
Prefer direct evidence in this order: owning source/contract, targeted executable observation, then secondary explanation. Record the result in tests or durable docs when future work will depend on it again.

## Decision Branches
- If the claim can be settled by reading the owner or contract, read that source before any dependent design.
- If only an experiment can settle it, run the smallest check and treat the outcome as the fact going forward.

## Common Wrong Fixes
- Do not accumulate more reasoning around the guess.
- Do not search only for confirming snippets, or treat a familiar name as a contract. Plausibility is not authority.
- Do not encode the guess in comments or types so later readers inherit it as fact.

## Verification
The material claim should be supportable by a concrete source or reproducible observation, and the implementation should no longer depend on unstated assumptions. The invariant is: every load-bearing premise has provenance before it shapes architecture.

## Done When
Load-bearing premises are facts with provenance, hypotheses remain labeled as hypotheses, and uncertainty is resolved before it hardens into architecture.
