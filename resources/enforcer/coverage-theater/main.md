# coverage-theater — Main

## What To Do Now
Replace metric-only tests with behavioral assertions that distinguish the intended result from realistic wrong results. A falsifiable caller-visible proposition is who owns verification; coverage counts are not who owns the claim that behavior is correct.

## Why This Matters
A coverage report answers “did execution pass here?” It cannot answer “was what happened here correct?” Chasing the metric encourages tests that touch branches without specifying their meaning, producing numerical confidence detached from defect-detection power.

## Repair Strategy
Start from the contract or invariant, then choose inputs that expose it and assert the observable result. Use coverage afterward to find unvisited risk, never as the definition of verified behavior.

## Decision Branches
- If a test raises coverage but would stay green under a realistic wrong result, replace its assertions first.
- If coverage is only a map of unvisited risk beside real behavioral tests, keep the map; do not treat it as proof.
- If an assertion exists but checks only non-null or “did not throw,” treat it as theater and strengthen it.

## Common Wrong Fixes
- Do not add empty smoke tests to raise line counts.
- Do not assert merely that a value is non-null.
- Do not add snapshots so broad that reviewers cannot state what matters.
- Do not lower the coverage threshold instead of adding a falsifiable proposition.

## Verification
Mutate the implementation conceptually: swap a result, drop an error, change an ID, reorder an effect. A valuable test should fail for the defect it claims to guard. The invariant is that coverage numbers are consequences of meaningful tests, not substitutes for caller-visible propositions.

## Done When
Coverage numbers are consequences of meaningful tests, not substitutes for them, and each important test protects a caller-visible proposition or system invariant.
