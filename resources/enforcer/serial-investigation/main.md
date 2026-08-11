# serial-investigation — Main

## What To Do Now
Issue independent searches, reads, and diagnostics concurrently, then combine their evidence before launching questions that depend on those results. The epistemic dependency graph is who owns the investigation schedule; one-question-at-a-time habit is not.

## Why This Matters
Sequential investigation wastes elapsed time without buying additional certainty when the questions are independent. Worse, early findings can anchor later inquiry before competing evidence has arrived. Parallel evidence gathering reduces both latency and premature narrative formation.

## Repair Strategy
Partition the inquiry into independent evidence requests, bound concurrency to tool and system limits, and synthesize results before the next dependent wave. Serialize only the hypothesis branches that genuinely require a prior answer.

## Decision Branches
- If several questions are fully specified from the current context, issue them in one parallel wave.
- If a question cannot be named until another answer arrives, wait for that edge, then start the next wave.
- If tooling capacity is already at its bound, keep the queue bounded rather than serializing by habit or fanning out without limit.

## Common Wrong Fixes
- Do not fan out dozens of vague searches; parallelism does not replace well-formed questions.
- Do not read files one-by-one “to stay organized” when they share no dependency.
- Do not start the next dependent question before synthesizing the parallel wave.
- Do not ignore destructive interference and mutate the same environment from concurrent probes.

## Verification
Each parallel request should be formulable from the same starting context, and no request should require another request’s result to be correct. The invariant is that investigation elapsed time follows the epistemic dependency graph, not an arbitrary serial habit.

## Done When
Elapsed investigation time reflects true information dependencies rather than an arbitrary one-question-at-a-time workflow.
