# todo-bomb — Main

## What To Do Now
Complete the correctness-critical behavior or explicitly remove that case from the supported contract before delivery.

## Why This Matters
A reachable TODO is a known hole hidden behind future tense. The code already knows a valid path can arrive somewhere it cannot honor, yet shifts the cost to whichever user or maintainer encounters it first.

## Repair Strategy
Trace whether the placeholder is reachable from supported inputs. Implement the real behavior and test it, or move rejection to the public boundary with a typed unsupported outcome if the product intentionally does not support the case.

## Wrong Fixes
Do not replace `TODO` with a quieter default, empty result, or generic catch. Silencing the marker preserves the missing semantics while removing the warning.

## Verification
Every reachable supported case must have defined behavior and a test; every intentionally unsupported case must be rejected explicitly before entering an incomplete path.

## Done When
No production path relies on a promise that only a comment makes about future work.
