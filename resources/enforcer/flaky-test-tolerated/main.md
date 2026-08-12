# flaky-test-tolerated — Main

## What To Do Now
Stop treating the test as ordinary trusted evidence until one experiment has one interpretable verdict.

Preserve a failing reproduction, identify the hidden input, and either control it or remove the test from the evidence chain. Do not keep the instrument authoritative while adding retries, quarantine labels, or folklore about when to believe it.

## Why This Matters
Flakiness taxes much more than CI minutes.

It corrupts the meaning of failure. The moment engineers learn that red may be harmless, every regression buys itself plausible deniability. The rational response becomes “rerun first, investigate later,” which is exactly backward: the first unexplained red is often the cheapest evidence you will ever get about a race, leak, stale state, or missing causal signal.

A flaky suite also creates social asymmetry. Green is celebrated immediately; red must win an argument. That selection bias makes defects look rarer than they are.

## Repair Strategy
Make hidden inputs explicit and owned:

- inject or freeze time instead of racing wall clock;
- record random seeds and make replay exact;
- isolate filesystem/database/global state per test;
- remove order coupling and shared mutable fixtures;
- wait on causal signals rather than sleeps;
- control concurrency deliberately and expose races with deterministic coordination where possible;
- replace live external dependencies with a deterministic contract at the boundary when the test is not specifically about that external system;
- if the live dependency is the subject, model its transient/failure policy explicitly rather than pretending the environment is deterministic.

Keep the original failure useful. Do not “fix” the test by making the observation less capable of seeing the defect.

## Decision Branches
- **Hidden input found and controllable:** make it explicit; keep the test.
- **The test depends on shared residue:** give it isolated ownership and cleanup, then prove independence under shuffled/parallel execution where relevant.
- **The test is inherently probabilistic because the product contract is probabilistic:** define the statistical acceptance criterion and seed/sample policy explicitly. Do not use ad-hoc reruns.
- **The test cannot be made deterministic enough to support the claim:** replace or delete it. A missing test is more honest than fake evidence.
- **Quarantine is necessary during an active repair:** give it an owner, concrete defect link, and bounded exit criterion; do not count it as trusted coverage meanwhile.

## Common Wrong Fixes
- Increase retries until the failure rate is socially tolerable.
- Add sleeps or widen timeouts so the favorable schedule becomes more likely.
- Force the whole suite serial because one test leaks state, unless serialization is genuinely part of the product invariant being tested.
- Catch and ignore the flaky assertion on CI while leaving it active locally.
- Keep a quarantine forever because deleting the test would “reduce coverage.” A broken witness does not become valuable by remaining present.
- Run it 100 times, observe 100 greens, and declare causality repaired without identifying what changed. Stability sampling may raise confidence after a fix; it is not the fix.

## Verification
The proof is causal, not statistical:

1. identify the input or mechanism that made verdicts diverge;
2. control or remove that mechanism;
3. show the test now has an explicit experiment definition;
4. show a relevant defect still makes it red.

Repeated runs after repair may be useful as a stress check, but no number of lucky greens can substitute for steps 1–4.

Invariant:

> Equivalent relevant inputs yield one verdict with one meaning.

## Done When
A red result is actionable again.

Nobody needs the phrase “probably flaky,” a rerun button, or luck to decide whether the test is speaking truth.
