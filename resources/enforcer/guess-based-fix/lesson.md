# guess-based-fix — Lesson

## Opinion

### Definition

A guess-based fix is a patch selected because the symptom moved, not because a causal explanation survived an attempt to falsify it.

The signature is familiar: tweak timeout, add retry, reorder calls, clear cache, add a lock, change a flag, widen parsing, catch an exception — then stop when the test turns green. The patch may work. The defect is that **nobody knows why**, so the repository has gained behavior without gained knowledge.

### Governing Principle

A passing configuration is not a causal explanation.

Complex systems contain thousands of interventions that can perturb timing, scheduler order, cache state, resource pressure, retries, error visibility, or race probability. Many of them can make a symptom disappear without repairing the violated invariant.

A real fix should answer two questions:

1. Why did the old system produce the observed failure?
2. Why does this change prevent that mechanism rather than merely hide its symptom?

If the explanation cannot predict anything beyond “the tests are green now,” the work is still an experiment pretending to be a repair.

### Trigger When

Trigger when mutation is used as blind search over the solution space and the first favorable outcome is retained without causal discrimination. Common forms:

- several unrelated edits land together and success is attributed to the bundle;
- timeout/retry/cache/concurrency knobs are changed until flakiness disappears;
- an exception is caught or error ignored because “that stops the crash,” without establishing whether the operation may now silently fail;
- a lock or serialization is added to a race without identifying which shared invariant requires exclusion;
- code is rewritten wholesale because the smaller failure mechanism was never isolated;
- an AI-generated patch changes multiple plausible sites and the green suite is treated as proof that the model found the cause;
- failed speculative edits are left behind as “harmless cleanup,” destroying the experiment's ability to tell which change mattered.

### Do Not Trigger When

- A reversible probe is explicitly used to distinguish named hypotheses and is removed when it does not support them.
- Several changes are mechanically required consequences of one already established causal repair.
- The system is explored experimentally because no model exists yet, but the final retained fix is narrowed and verified against a causal hypothesis.
- A mitigation intentionally reduces impact without claiming root-cause repair; the limitation and remaining cause are stated honestly.
- A broad rewrite is independently required by the task and each relevant behavior remains verified; breadth alone is not guessing.

### Distinguish From

`guessed-not-verified` is epistemic debt before mutation: a material premise is treated as fact without checking its owner. `guess-based-fix` is search-by-mutation: code/settings are changed until symptoms move.

`blind-edit` focuses on changing code before locating responsibility. `repeat-until-pass` changes nothing and samples executions until a favorable outcome appears. Here the search variable is the implementation itself.

### Decision Procedure

Ask the author to state the violated invariant and a falsifiable mechanism connecting it to the observed failure.

Then ask what observation would distinguish that mechanism from at least one plausible alternative. If the patch was chosen before such discrimination, reconstruct the experiment: revert unrelated changes, preserve the failing case, and reintroduce only changes justified by the mechanism.

If “it passed after this” is doing most of the explanatory work, the rule applies.

### Examples

- positive: CI flakes; timeout, retries, and worker count are all changed; the suite goes green and the bundle ships as “stability fix.”
- positive: a race disappears after adding a global lock, but nobody identifies shared state; the lock may merely serialize unrelated work and hide timing.
- positive: an agent changes three parsers, a cache layer, and error handling; tests pass; no failing input is tied to any one change.
- near-miss: two hypotheses are named; one targeted probe disproves cache corruption and another reproduces a lost update; the patch fixes the ownership conflict and adds a regression.
- counterexample: the violated invariant is already established, and several mechanical call sites are updated consistently to enforce it.

## Advice

### What To Do Now

Turn the lucky patch back into an experiment.

Preserve the failing case. Identify the invariant that should have prevented it. Revert or separate speculative changes until you can test one causal hypothesis at a time. Keep only the change whose mechanism explains both the original failure and the repaired behavior.

Then encode that explanation in a regression capable of failing if the mechanism returns.

### Why This Matters

Guess-based fixes accumulate a particularly toxic form of technical debt: **unknown necessity**.

Every speculative line that remains after the symptom disappears becomes something future maintainers are afraid to remove because “maybe that was part of the fix.” Systems grow retries nobody can justify, locks nobody can scope, cache flushes nobody can delete, exception handlers nobody can reason about, and timeouts whose only provenance is a long-forgotten red build.

The immediate symptom may be gone, but the codebase becomes less knowable. Future incidents start from a worse epistemic state than the first one.

### Repair Strategy

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

### Decision Branches

- **Several speculative edits landed together:** split/revert until the causal contribution of each retained change is known.
- **A knob change hides timing:** restore the old policy where safe and investigate the missing signal/race/resource cause.
- **A broad lock fixed a race:** identify the exact shared invariant; narrow ownership/exclusion if the global lock is not semantically required.
- **An exception catch stopped failure:** prove whether swallowing/recovering the error preserves the caller contract; otherwise restore visibility.
- **A generated patch is green but unexplained:** inspect the diff as a set of hypotheses, not as an oracle answer.
- **Only mitigation is currently safe:** document the causal uncertainty and containment boundary; do not claim root-cause closure.

### Common Wrong Fixes

- Keep every speculative change because removing any one “might bring the bug back.” That is precisely why the experiment must be disentangled.
- Add a regression that merely asserts the entire current implementation output. A useful regression isolates the violated invariant.
- Write a confident post-hoc explanation unsupported by a discriminating observation.
- Treat a green suite as proof of mechanism. It proves only what those tests can distinguish.
- Rename the speculative workaround as an “architecture improvement” so nobody asks whether it was necessary.
- Stack a second workaround on top of the first when an adjacent symptom appears.

### Verification

A causal repair should make predictions.

It should explain:

- why the original failure occurred;
- why the retained change prevents it;
- why at least one plausible alternative hypothesis is less consistent with the evidence;
- what regression or invariant would detect recurrence.

Where practical, demonstrate that restoring the old mechanism makes the regression red and applying the repair makes it green.

Invariant:

> The repository retains mechanisms for reasons that can be explained, not because they happened to coexist with a successful run.

### Done When

The patch is executable knowledge.

A future maintainer can remove unrelated changes without superstition, because the actual repair owns a named invariant and a test that remembers why it exists.

### Nudge

“It passes now” is an observation, not an explanation.

Keep the change that explains the failure. Throw away the changes that merely accompanied good luck.
