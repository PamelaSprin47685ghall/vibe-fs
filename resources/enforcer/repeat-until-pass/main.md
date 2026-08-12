# repeat-until-pass — Main

## What To Do Now
Stop rerunning the correctness check.

Treat the first unexplained conflicting verdict as the defect. Preserve its output, record the relevant inputs/environment, and identify what hidden variable can make materially equivalent runs disagree.

Accept a later green only after you can explain why it is a different experiment or why the mechanism that produced the red has been repaired.

## Why This Matters
Repeat-until-pass converts verification into selection bias.

The question silently changes from:

> Is the system correct under these conditions?

into:

> Can I eventually observe a schedule/environment in which this check looks green?

Those are not remotely the same proposition.

This habit is especially dangerous in automated coding workflows because repeated tool calls are cheap. Cheap repetition can create the illusion of additional evidence even when every invocation asks the same broken question of the same unstable system. Ten samples do not become ten independent witnesses just because they have ten timestamps.

## Repair Strategy
Freeze the experiment before touching the implementation:

- capture test name, seed, timing, order, environment, process state, external dependencies, and shared resources;
- retain the first red output;
- identify what can differ between runs despite nominally identical commands;
- construct a discriminating observation for that hidden variable;
- repair the owning cause;
- then run once under controlled conditions as the primary correctness observation.

If a retry is legitimate infrastructure behavior, move it into an explicit bounded policy with a named transient class, idempotence assumptions, backoff if appropriate, and final failure that remains visible.

## Decision Branches
- **Nothing meaningful changed between red and green:** green does not close the issue. Investigate nondeterminism.
- **A causal fix changed the system:** the next run is a new experiment; use it normally.
- **A known external transient occurred:** apply the explicit retry policy, not ad-hoc reruns. Preserve evidence that the transient was actually classified.
- **The test is itself flaky:** repair or retire the test; see `flaky-test-tolerated`.
- **The command polls readiness:** use a causal condition with a bounded deadline. Do not recast failed assertions as polling.

## Common Wrong Fixes
- Increase the retry count until the failure becomes statistically rare.
- Run the command in a loop and print only the first green result.
- Say “passed on retry” without explaining what made the first result invalid.
- Average failure rate and decide a low percentage is acceptable for a deterministic contract.
- Restart processes, clear caches, or delete temp state until green, then omit those interventions from the report. You have changed the experiment; hiding that fact destroys the evidence.
- Ask another machine/agent to run the same check merely to obtain a second chance at green.

## Verification
After repair, correctness must no longer depend on finding a favorable sample.

The primary proof is:

1. the hidden variable or transient class is identified;
2. the relevant mechanism is controlled or repaired;
3. one run under explicit conditions has a stable meaning;
4. the original failure cannot simply reappear under those same conditions without contradicting the explanation.

Repeated execution may stress the repaired system. It must not be the algorithm by which “pass” is discovered.

## Done When
A green verdict is accepted because the experiment is interpretable, not because enough attempts were purchased to eventually find one.

The earlier red has an explanation, not an eraser.
