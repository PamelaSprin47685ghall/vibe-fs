# todo-bomb — Main

## What To Do Now
Complete the correctness-critical behavior or explicitly remove that case from the supported contract before delivery.

## Why This Matters
A reachable TODO is a known hole hidden behind future tense. The code already knows a valid path can arrive somewhere it cannot honor, yet shifts the cost to whichever user or maintainer encounters it first.

## Repair Strategy
Trace whether the placeholder is reachable from supported inputs. Implement the real behavior and test it, or move rejection to the public boundary with a typed unsupported outcome if the product intentionally does not support the case.

## Decision Branches
If a supported input can reach the placeholder, implement the behavior or reject that case at the public boundary.
If the note is optional future work on an unsupported path, leave it as backlog and keep it unreachable.

## Common Wrong Fixes
- Replace `TODO` with a quieter default, empty result, or generic catch.
- Comment out the incomplete branch so the path silently skips required work.
- Ship the placeholder and file a ticket as if the ticket fulfilled the contract.

## Verification
Invariant: every reachable supported case has defined behavior and a test. Intentionally unsupported cases must be rejected explicitly before entering an incomplete path.

## Done When
No production path relies on a promise that only a comment makes about future work.
