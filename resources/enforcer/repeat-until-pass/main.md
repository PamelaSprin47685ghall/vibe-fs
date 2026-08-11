# repeat-until-pass — Main

## What To Do Now
Treat the first unexplained inconsistent verdict as a defect. Reproduce and remove the hidden source of nondeterminism before accepting any later green run.

## Why This Matters
Rerunning until success converts verification into selection bias. The suite no longer answers “is the system correct?” but “did we eventually observe a favorable schedule/environment?” That destroys the epistemic value of both red and green.

## Repair Strategy
Record seed, timing, order, environment, external dependencies, and shared state; isolate the changing input and control it. Keep retries only for explicitly modeled infrastructure transients outside the correctness claim.

## Wrong Fixes
Do not raise CI retry counts, loop the command locally, or average away failures. A rare failure is still evidence of a reachable bad state.

## Verification
After the causal fix, one run under explicit inputs must be meaningful. Repetition may confirm stability but must not be required to obtain confidence.

## Done When
Green is accepted because the experiment is deterministic, not because enough samples were taken to eventually find a green one.
