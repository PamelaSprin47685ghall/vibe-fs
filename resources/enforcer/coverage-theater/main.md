# coverage-theater — Main

## What To Do Now
Replace metric-only tests with behavioral assertions that distinguish the intended result from realistic wrong results.

## Why This Matters
A coverage report answers “did execution pass here?” It cannot answer “was what happened here correct?” Chasing the metric encourages tests that touch branches without specifying their meaning, producing numerical confidence detached from defect-detection power.

## Repair Strategy
Start from the contract or invariant, then choose inputs that expose it and assert the observable result. Use coverage afterward to find unvisited risk, never as the definition of verified behavior.

## Wrong Fixes
Do not add empty smoke tests, assertions that merely check non-null, or snapshots so broad that reviewers cannot state what matters. These raise counts while leaving semantics unexamined.

## Verification
Mutate the implementation conceptually: swap a result, drop an error, change an ID, reorder an effect. A valuable test should fail for the defect it claims to guard.

## Done When
Coverage numbers are consequences of meaningful tests, not substitutes for them, and each important test protects a caller-visible proposition or system invariant.
