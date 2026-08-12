# guess-based-fix — Main

## What To Do Now
Turn the lucky patch back into an experiment.

Preserve the failing case. Identify the invariant that should have prevented it. Revert or separate speculative changes until you can test one causal hypothesis at a time. Keep only the change whose mechanism explains both the original failure and the repaired behavior.

Then encode that explanation in a regression capable of failing if the mechanism returns.

## Why This Matters
Guess-based fixes accumulate a particularly toxic form of technical debt: **unknown necessity**.

Every speculative line that remains after the symptom disappears becomes something future maintainers are afraid to remove because “maybe that was part of the fix.” Systems grow retries nobody can justify, locks nobody can scope, cache flushes nobody can delete, exception handlers nobody can reason about, and timeouts whose only provenance is a long-forgotten red build.

The immediate symptom may be gone, but the codebase becomes less knowable. Future incidents start from a worse epistemic state than the first one.

## Repair Strategy
Reduce before expanding:

1. make the failure reproducible enough to inspect;
2. state competing causal hypotheses;
3. design the cheapest observation that separates them;
4. locate the owner of the violated invariant;
5. implement the smallest coherent repair at that owner;
6. remove speculative edits whose causal contribution is unproven;
7. add regression evidence that fails under the old mechanism.

If perfect reproduction is impossible, you can still improve causality: instrument the suspected transition, constrain hidden variables, and choose a repair justified by the strongest available evidence. Do not pretend uncertainty vanished; record what remains unknown.

A mitigation may be valid when a root-cause repair cannot be delivered safely now. Name it as mitigation, preserve the underlying issue, and avoid turning operational containment into architectural doctrine.

## Decision Branches
- **Several speculative edits landed together:** split/revert until the causal contribution of each retained change is known.
- **A knob change hides timing:** restore the old policy where safe and investigate the missing signal/race/resource cause.
- **A broad lock fixed a race:** identify the exact shared invariant; narrow ownership/exclusion if the global lock is not semantically required.
- **An exception catch stopped failure:** prove whether swallowing/recovering the error preserves the caller contract; otherwise restore visibility.
- **A generated patch is green but unexplained:** inspect the diff as a set of hypotheses, not as an oracle answer.
- **Only mitigation is currently safe:** document the causal uncertainty and containment boundary; do not claim root-cause closure.

## Common Wrong Fixes
- Keep every speculative change because removing any one “might bring the bug back.” That is precisely why the experiment must be disentangled.
- Add a regression that merely asserts the entire current implementation output. A useful regression isolates the violated invariant.
- Write a confident post-hoc explanation unsupported by a discriminating observation.
- Treat a green suite as proof of mechanism. It proves only what those tests can distinguish.
- Rename the speculative workaround as an “architecture improvement” so nobody asks whether it was necessary.
- Stack a second workaround on top of the first when an adjacent symptom appears.

## Verification
A causal repair should make predictions.

It should explain:

- why the original failure occurred;
- why the retained change prevents it;
- why at least one plausible alternative hypothesis is less consistent with the evidence;
- what regression or invariant would detect recurrence.

Where practical, demonstrate that restoring the old mechanism makes the regression red and applying the repair makes it green.

Invariant:

> The repository retains mechanisms for reasons that can be explained, not because they happened to coexist with a successful run.

## Done When
The patch is executable knowledge.

A future maintainer can remove unrelated changes without superstition, because the actual repair owns a named invariant and a test that remembers why it exists.
