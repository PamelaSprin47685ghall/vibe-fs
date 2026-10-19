# repeat-until-pass — Lesson

## Opinion

### Definition

Repeat-until-pass is outcome selection disguised as verification.

The same or materially equivalent experiment produces both red and green, and instead of explaining the contradiction, the operator keeps sampling until a favorable result appears. The final green is then promoted as if the earlier reds had never happened.

This is engineering p-hacking: keep drawing samples until reality says what you wanted to hear.

### Governing Principle

A contradictory observation does not become less real because a later observation is convenient.

If relevant inputs are unchanged and verdicts differ, the strongest established fact is **nondeterminism**. You do not yet know whether the system is correct. Selecting the green sample destroys information precisely because it is inconvenient.

Retries have legitimate uses when retry itself is part of a modeled protocol: transient network failure, idempotent acquisition, causal polling for readiness. What is illegitimate is using repeated executions of a failed correctness claim to shop for a verdict.

### Trigger When

Trigger when a failed correctness check is rerun under materially equivalent conditions until one attempt passes and that favorable attempt is accepted without explaining the failed attempts. Common forms:

- local test fails; engineer presses up-arrow until green and reports success;
- CI has “retry flaky tests N times” and any green attempt converts the job to passing;
- an integration check is looped in a shell until it exits 0, with prior outputs discarded;
- a model/agent is told to “try the tests again” repeatedly instead of investigating why the same code produces mixed outcomes;
- a green run after timeout/scheduler noise is treated as confirmation even though no causal change occurred between runs;
- several environments/schedules are sampled and only the passing one is cited as evidence.

### Do Not Trigger When

- The first failure was explained by a specific changed input or repaired mechanism; the later run is therefore a genuinely new experiment.
- An explicitly modeled, bounded retry handles a known external transient and final failure remains visible.
- Polling waits for a causal readiness condition and does not reinterpret an already-failed assertion as success.
- Repetition is used **after** a causal repair as stress sampling, while the correctness claim does not depend on eventually finding green.
- A property/stochastic test uses an explicit sampling contract rather than “stop when one run passes.”

### Distinguish From

`flaky-test-tolerated` is the policy of allowing an unstable instrument to remain trusted. `repeat-until-pass` is the concrete act of cherry-picking a favorable outcome.

`timeout-inflated-to-pass` changes the waiting budget so the favorable schedule becomes more likely. `sleep-based-synchronization` substitutes elapsed time for a readiness signal. These often coexist, but this rule owns the moment where mixed evidence is resolved by selecting green instead of explaining the contradiction.

### Decision Procedure

After the first unexplained red, stop sampling.

Record the exact relevant inputs and ask whether anything causally meaningful changed before the next run. If not, a later green cannot erase the red.

The next legitimate step is to identify a hidden variable — seed, time, order, scheduler, resource pressure, external state, shared residue — or prove the first failure came from a separately modeled transient.

If the process is simply “run until pass,” the rule applies.

### Examples

- positive: a test fails twice, passes on the third identical invocation, and the third result is pasted into the completion report.
- positive: CI reruns each failure three times and reports the suite green if any attempt succeeds, with no flaky-test debt recorded.
- positive: an agent repeatedly invokes the same failing command after no code/config change until one run returns 0.
- near-miss: a failed test exposes an unseeded random branch; the seed is fixed, the causal bug repaired, and the next run uses controlled inputs.
- counterexample: an HTTP client retries a documented idempotent 503 according to protocol and still surfaces failure when the bounded policy is exhausted.

## Advice

### What To Do Now

Stop rerunning the correctness check.

Treat the first unexplained conflicting verdict as the defect. Preserve its output, record the relevant inputs/environment, and identify what hidden variable can make materially equivalent runs disagree.

Accept a later green only after you can explain why it is a different experiment or why the mechanism that produced the red has been repaired.

### Why This Matters

Repeat-until-pass converts verification into selection bias.

The question silently changes from:

> Is the system correct under these conditions?

into:

> Can I eventually observe a schedule/environment in which this check looks green?

Those are not remotely the same proposition.

This habit is especially dangerous in automated coding workflows because repeated tool calls are cheap. Cheap repetition can create the illusion of additional evidence even when every invocation asks the same broken question of the same unstable system. Ten samples do not become ten independent witnesses just because they have ten timestamps.

### Repair Strategy

Freeze the experiment before touching the implementation:

- capture test name, seed, timing, order, environment, process state, external dependencies, and shared resources;
- retain the first red output;
- identify what can differ between runs despite nominally identical commands;
- construct a discriminating observation for that hidden variable;
- repair the owning cause;
- then run once under controlled conditions as the primary correctness observation.

If a retry is legitimate infrastructure behavior, move it into an explicit bounded policy with a named transient class, idempotence assumptions, backoff if appropriate, and final failure that remains visible.

### Decision Branches

- **Nothing meaningful changed between red and green:** green does not close the issue. Investigate nondeterminism.
- **A causal fix changed the system:** the next run is a new experiment; use it normally.
- **A known external transient occurred:** apply the explicit retry policy, not ad-hoc reruns. Preserve evidence that the transient was actually classified.
- **The test is itself flaky:** repair or retire the test; see `flaky-test-tolerated`.
- **The command polls readiness:** use a causal condition with a bounded deadline. Do not recast failed assertions as polling.

### Common Wrong Fixes

- Increase the retry count until the failure becomes statistically rare.
- Run the command in a loop and print only the first green result.
- Say “passed on retry” without explaining what made the first result invalid.
- Average failure rate and decide a low percentage is acceptable for a deterministic contract.
- Restart processes, clear caches, or delete temp state until green, then omit those interventions from the report. You have changed the experiment; hiding that fact destroys the evidence.
- Ask another machine/agent to run the same check merely to obtain a second chance at green.

### Verification

After repair, correctness must no longer depend on finding a favorable sample.

The primary proof is:

1. the hidden variable or transient class is identified;
2. the relevant mechanism is controlled or repaired;
3. one run under explicit conditions has a stable meaning;
4. the original failure cannot simply reappear under those same conditions without contradicting the explanation.

Repeated execution may stress the repaired system. It must not be the algorithm by which “pass” is discovered.

### Done When

A green verdict is accepted because the experiment is interpretable, not because enough attempts were purchased to eventually find one.

The earlier red has an explanation, not an eraser.

### Nudge

Do not choose evidence by outcome.

One unexplained red invalidates a lucky green until you can name what changed.
