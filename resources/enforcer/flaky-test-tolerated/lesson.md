# flaky-test-tolerated — Lesson

## Opinion

### Definition

A flaky test is not “a test that occasionally fails.” It is a measuring instrument whose verdict changes while the relevant inputs are treated as equivalent — and the organization has decided to live with that ambiguity.

The rule fires on **tolerance**, not first discovery. One unexplained intermittent red is evidence to investigate. A flake becomes institutional debt when reruns, quarantine, folklore, or selective disbelief turn nondeterminism into normal operating procedure.

### Governing Principle

A test has two jobs: detect a meaningful difference and make the verdict interpretable.

If the same relevant state can produce red or green, the suite can no longer tell product change from measurement noise. The first casualty is not CI speed; it is epistemology. Every future red now has an escape hatch: “probably flaky.” Every future green carries a discount: “maybe we got lucky.”

One tolerated flake teaches engineers a habit more expensive than the test itself — **distrust red until rerun**. Once that habit spreads, real regressions receive the same treatment as noise.

### Trigger When

Trigger when known nondeterminism remains accepted in a test that is still presented as evidence, including:

- CI automatically reruns a failing test and reports only the eventual green verdict;
- the team routinely says “rerun it” before investigating the first red;
- a test is quarantined, skipped, or labeled “known flaky” with no bounded owner/exit plan while its supposed coverage is still counted;
- timing windows are widened repeatedly without identifying the hidden input;
- failures depend on order, clock, random seed, shared residue, race, resource pressure, or external service state, and that source remains uncontrolled;
- reviewers accept “passes most of the time” as sufficient for a deterministic contract.

### Do Not Trigger When

- A stochastic/property test records its random seed and a given seed produces a deterministic verdict.
- A single intermittent failure is actively being treated as a defect and investigated rather than normalized.
- A test intentionally verifies probabilistic behavior with a statistically defined contract appropriate to the product; nondeterminism is then part of the specification, not accidental measurement noise.
- A truly external transient is retried by an explicitly modeled infrastructure policy, while correctness assertions themselves remain deterministic and final failure remains visible.

### Distinguish From

`repeat-until-pass` is the act of selecting a favorable sample from mixed outcomes. `flaky-test-tolerated` is the broader policy failure: the unstable instrument is allowed to remain authoritative.

`time-dependent-test`, `order-dependent-test`, `random-source-in-logic`, and shared-state rules often identify the mechanism. Use those when the cause is known and specific; keep this rule when the central wound is that nondeterministic verdicts have been normalized.

### Decision Procedure

Ask whether two runs that the test suite considers “the same experiment” can produce different verdicts.

If yes, identify the hidden input: clock, seed, scheduling, order, process lifetime, network state, shared database, filesystem residue, port allocation, environment, resource pressure.

Then ask the policy question: is the suite still treating this test as trustworthy evidence without controlling that input? If yes, this rule applies.

### Examples

- positive: CI retries a test up to three times; the job is green if any attempt passes, and the flake remains indefinitely.
- positive: a test occasionally fails under parallel execution, so the suite is forced to serial mode rather than repairing leaked shared state; the test is still called reliable.
- positive: a “temporary” quarantine has no owner, date, or exit criterion six months later.
- near-miss: an intermittent failure appears once, is preserved, and investigation finds an unseeded random choice; the seed is made explicit before the test returns to trusted status.
- counterexample: a property test reports seed `0xabc`; rerunning that exact seed deterministically reproduces the failure.

## Advice

### What To Do Now

Stop treating the test as ordinary trusted evidence until one experiment has one interpretable verdict.

Preserve a failing reproduction, identify the hidden input, and either control it or remove the test from the evidence chain. Do not keep the instrument authoritative while adding retries, quarantine labels, or folklore about when to believe it.

### Why This Matters

Flakiness taxes much more than CI minutes.

It corrupts the meaning of failure. The moment engineers learn that red may be harmless, every regression buys itself plausible deniability. The rational response becomes “rerun first, investigate later,” which is exactly backward: the first unexplained red is often the cheapest evidence you will ever get about a race, leak, stale state, or missing causal signal.

A flaky suite also creates social asymmetry. Green is celebrated immediately; red must win an argument. That selection bias makes defects look rarer than they are.

### Repair Strategy

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

### Decision Branches

- **Hidden input found and controllable:** make it explicit; keep the test.
- **The test depends on shared residue:** give it isolated ownership and cleanup, then prove independence under shuffled/parallel execution where relevant.
- **The test is inherently probabilistic because the product contract is probabilistic:** define the statistical acceptance criterion and seed/sample policy explicitly. Do not use ad-hoc reruns.
- **The test cannot be made deterministic enough to support the claim:** replace or delete it. A missing test is more honest than fake evidence.
- **Quarantine is necessary during an active repair:** give it an owner, concrete defect link, and bounded exit criterion; do not count it as trusted coverage meanwhile.

### Common Wrong Fixes

- Increase retries until the failure rate is socially tolerable.
- Add sleeps or widen timeouts so the favorable schedule becomes more likely.
- Force the whole suite serial because one test leaks state, unless serialization is genuinely part of the product invariant being tested.
- Catch and ignore the flaky assertion on CI while leaving it active locally.
- Keep a quarantine forever because deleting the test would “reduce coverage.” A broken witness does not become valuable by remaining present.
- Run it 100 times, observe 100 greens, and declare causality repaired without identifying what changed. Stability sampling may raise confidence after a fix; it is not the fix.

### Verification

The proof is causal, not statistical:

1. identify the input or mechanism that made verdicts diverge;
2. control or remove that mechanism;
3. show the test now has an explicit experiment definition;
4. show a relevant defect still makes it red.

Repeated runs after repair may be useful as a stress check, but no number of lucky greens can substitute for steps 1–4.

Invariant:

> Equivalent relevant inputs yield one verdict with one meaning.

### Done When

A red result is actionable again.

Nobody needs the phrase “probably flaky,” a rerun button, or luck to decide whether the test is speaking truth.

### Nudge

A flaky test is a broken instrument, not an eccentric coworker.

Fix the hidden input or retire the instrument. Do not train the organization to negotiate with red.
