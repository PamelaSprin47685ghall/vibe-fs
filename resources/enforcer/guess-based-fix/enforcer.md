# guess-based-fix — Enforcer

## Definition
A guess-based fix is a patch selected because the symptom moved, not because a causal explanation survived an attempt to falsify it.

The signature is familiar: tweak timeout, add retry, reorder calls, clear cache, add a lock, change a flag, widen parsing, catch an exception — then stop when the test turns green. The patch may work. The defect is that **nobody knows why**, so the repository has gained behavior without gained knowledge.

## Governing Principle
A passing configuration is not a causal explanation.

Complex systems contain thousands of interventions that can perturb timing, scheduler order, cache state, resource pressure, retries, error visibility, or race probability. Many of them can make a symptom disappear without repairing the violated invariant.

A real fix should answer two questions:

1. Why did the old system produce the observed failure?
2. Why does this change prevent that mechanism rather than merely hide its symptom?

If the explanation cannot predict anything beyond “the tests are green now,” the work is still an experiment pretending to be a repair.

## Trigger When
Trigger when mutation is used as blind search over the solution space and the first favorable outcome is retained without causal discrimination. Common forms:

- several unrelated edits land together and success is attributed to the bundle;
- timeout/retry/cache/concurrency knobs are changed until flakiness disappears;
- an exception is caught or error ignored because “that stops the crash,” without establishing whether the operation may now silently fail;
- a lock or serialization is added to a race without identifying which shared invariant requires exclusion;
- code is rewritten wholesale because the smaller failure mechanism was never isolated;
- an AI-generated patch changes multiple plausible sites and the green suite is treated as proof that the model found the cause;
- failed speculative edits are left behind as “harmless cleanup,” destroying the experiment's ability to tell which change mattered.

## Do Not Trigger When
- A reversible probe is explicitly used to distinguish named hypotheses and is removed when it does not support them.
- Several changes are mechanically required consequences of one already established causal repair.
- The system is explored experimentally because no model exists yet, but the final retained fix is narrowed and verified against a causal hypothesis.
- A mitigation intentionally reduces impact without claiming root-cause repair; the limitation and remaining cause are stated honestly.
- A broad rewrite is independently required by the task and each relevant behavior remains verified; breadth alone is not guessing.

## Distinguish From
`guessed-not-verified` is epistemic debt before mutation: a material premise is treated as fact without checking its owner. `guess-based-fix` is search-by-mutation: code/settings are changed until symptoms move.

`blind-edit` focuses on changing code before locating responsibility. `repeat-until-pass` changes nothing and samples executions until a favorable outcome appears. Here the search variable is the implementation itself.

## Decision Procedure
Ask the author to state the violated invariant and a falsifiable mechanism connecting it to the observed failure.

Then ask what observation would distinguish that mechanism from at least one plausible alternative. If the patch was chosen before such discrimination, reconstruct the experiment: revert unrelated changes, preserve the failing case, and reintroduce only changes justified by the mechanism.

If “it passed after this” is doing most of the explanatory work, the rule applies.

## Examples
- positive: CI flakes; timeout, retries, and worker count are all changed; the suite goes green and the bundle ships as “stability fix.”
- positive: a race disappears after adding a global lock, but nobody identifies shared state; the lock may merely serialize unrelated work and hide timing.
- positive: an agent changes three parsers, a cache layer, and error handling; tests pass; no failing input is tied to any one change.
- near-miss: two hypotheses are named; one targeted probe disproves cache corruption and another reproduces a lost update; the patch fixes the ownership conflict and adds a regression.
- counterexample: the violated invariant is already established, and several mechanical call sites are updated consistently to enforce it.

## Nudge
“It passes now” is an observation, not an explanation.

Keep the change that explains the failure. Throw away the changes that merely accompanied good luck.
