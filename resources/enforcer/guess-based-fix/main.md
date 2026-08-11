# guess-based-fix — Main

## What To Do Now
Reproduce the failure, form a falsifiable causal hypothesis, and keep only the change whose mechanism explains the observed defect.

## Why This Matters
A speculative patch can make a symptom vanish by changing timing or incidental state while leaving the underlying defect intact. Without causality, the team learns only that one configuration happened to pass once; it cannot predict adjacent cases or know what future refactors must preserve.

## Repair Strategy
Reduce the failure to the smallest observable mechanism. Use targeted experiments to separate competing hypotheses, then implement the fix at the owning invariant and add a regression that fails under the old mechanism.

## Decision Branches
- If a discriminating observation can falsify the hypothesis, run that observation before keeping any patch.
- If several edits already landed without a mechanism, revert to the last known causal baseline and reintroduce only the proven change.

## Common Wrong Fixes
- Do not stack several speculative changes and call the bundle the fix.
- Do not keep “harmless” changes whose contribution is unknown; they destroy the experiment’s ability to teach.
- Do not treat a newly green test suite as proof of cause without a regression that isolates the old mechanism.

## Verification
The causal explanation should predict the original failure, the corrected behavior, and at least one discriminating case beyond “tests are green.” The restored invariant must fail under the old mechanism and hold under the new one.

## Done When
The patch is an explanation encoded in code and test, not a lucky point in the space of possible edits.
