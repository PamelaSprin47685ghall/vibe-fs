# guess-based-fix — Main

## What To Do Now
Reproduce the failure, form a falsifiable causal hypothesis, and keep only the change whose mechanism explains the observed defect.

## Why This Matters
A speculative patch can make a symptom vanish by changing timing or incidental state while leaving the underlying defect intact. Without causality, the team learns only that one configuration happened to pass once; it cannot predict adjacent cases or know what future refactors must preserve.

## Repair Strategy
Reduce the failure to the smallest observable mechanism. Use targeted experiments to separate competing hypotheses, then implement the fix at the owning invariant and add a regression that fails under the old mechanism.

## Wrong Fixes
Do not stack several speculative changes and call the bundle the fix. Do not keep “harmless” changes whose contribution is unknown; they destroy the experiment’s ability to teach.

## Verification
The causal explanation should predict the original failure, the corrected behavior, and at least one discriminating case beyond “tests are green.”

## Done When
The patch is an explanation encoded in code and test, not a lucky point in the space of possible edits.
