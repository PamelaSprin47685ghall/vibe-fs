# repeat-until-pass — Main

## What To Do Now
Treat the first unexplained inconsistent verdict as a defect. Reproduce and remove the hidden source of nondeterminism before accepting any later green run. The first unexplained inconsistent verdict is who owns the defect; a later lucky green is not who owns the correctness claim.

## Why This Matters
Rerunning until success converts verification into selection bias. The suite no longer answers “is the system correct?” but “did we eventually observe a favorable schedule/environment?” That destroys the epistemic value of both red and green.

## Repair Strategy
Record seed, timing, order, environment, external dependencies, and shared state; isolate the changing input and control it. Keep retries only for explicitly modeled infrastructure transients outside the correctness claim.

## Decision Branches
- If the same command flips red/green with unchanged inputs, stop sampling and find the hidden variable.
- If the failure is a documented external transient, use a bounded explicit retry that still surfaces final failure.
- If the cause is fixed, accept one deterministic green run; do not require a streak of retries for confidence.

## Common Wrong Fixes
- Raise CI retry counts or loop the command locally until green.
- Average away failures or treat a rare red as noise.
- Inflate timeouts so the lucky schedule is more likely (that is timeout-inflated-to-pass, not a fix).
- Delete the assertion that observed the red while leaving the nondeterminism.

## Verification
After the causal fix, one run under explicit inputs must be meaningful. Repetition may confirm stability but must not be required to obtain confidence. The invariant is: a green verdict is accepted only from a deterministic experiment, never from selection among mixed samples.

## Done When
Green is accepted because the experiment is deterministic, not because enough samples were taken to eventually find a green one.
