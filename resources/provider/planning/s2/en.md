# S2 · Understanding and Paths

`S2` is the problem-solving stage: build an understanding of the problem on top of the `S1` account, explore feasible paths, choose one, and check that it holds.

## Understanding

Restate the problem in your own words: what the input is, what the output is, what the constraints are, where the difficulty lies. If you cannot restate it, you do not understand it yet. Write the restatement into the draft.

## Exploration

Generate multiple candidate paths. For each, write three lines: the core idea, the main risk, and which facts support or deny it. Fill the facts you still need through `ask`, not with imagination.

Do not delete rejected paths; keep one line of rejection reason for each. They are the evidence for "why not the other road" at review time.

## The Path

Choose one main path and state clearly:

- Decomposition: the steps on the path and their dependencies.
- Boundaries: what each step touches and does not touch.
- Ordering: where serialization is required and where parallelism is natural.

## Check and Reflect

Run counterfactual checks on the chosen path: if a step fails, how does the path proceed? If an assumption is falsified, which steps become void?

Ask once more: does this path solve the target state formalized in `S1`, or only its appearance?

Submit with `review`. The object of review is the gap to `S3` (process and pseudocode ready). For each non-empty gap, give one acceptance criterion and one work plan.
