# serial-investigation — Main

## What To Do Now
Issue independent searches, reads, and diagnostics concurrently, then combine their evidence before launching questions that depend on those results.

## Why This Matters
Sequential investigation wastes elapsed time without buying additional certainty when the questions are independent. Worse, early findings can anchor later inquiry before competing evidence has arrived. Parallel evidence gathering reduces both latency and premature narrative formation.

## Repair Strategy
Partition the inquiry into independent evidence requests, bound concurrency to tool/system limits, and synthesize results in deterministic order. Serialize only the hypothesis branches that genuinely require a prior answer.

## Wrong Fixes
Do not fan out dozens of vague searches. Parallelism improves a well-formed investigation graph; it does not compensate for poorly specified questions.

## Verification
Each parallel request should be formulable from the same starting context, and no request should require another request’s result to be correct.

## Done When
Elapsed investigation time reflects true information dependencies rather than an arbitrary one-question-at-a-time workflow.
