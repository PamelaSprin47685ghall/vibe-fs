# coverage-theater — Lesson

## Opinion

### Definition

Coverage theater happens when **execution is presented as proof**.

The code was reached. The branch counter moved. The file appeared in a report. None of those facts establish that the behavior was correct.

The defect is not “coverage is bad.” Coverage is useful reconnaissance. The defect begins when reachability metrics are allowed to impersonate behavioral evidence.

### Governing Principle

A test earns its keep by making some plausible wrong implementation unacceptable.

If a test would stay green when the returned identity is swapped, an error is swallowed, an ordering guarantee is reversed, an authorization check is bypassed, or a state transition is wrong, then hitting the relevant lines has not verified those properties.

Modern coverage dashboards make this pathology seductive because they produce precise numbers. `94.7%` looks scientific. But precision about the wrong quantity is still ignorance. Line coverage can tell you where execution went. It cannot tell you whether the journey meant anything.

### Trigger When

Trigger when coverage or traversal is used as the central evidence for correctness while the assertions fail to distinguish realistic defects. Common forms:

- tests call every method but assert only “not null,” “defined,” or “did not throw”;
- snapshot assertions are so broad that nobody can name which semantic changes should fail them;
- mocks are satisfied because calls occurred, while caller-visible outcome is never asserted;
- tests exercise both branches but do not assert the branch-specific invariant;
- a coverage threshold drives the addition of low-information tests whose only purpose is to color lines green;
- mutation of a meaningful outcome would leave the suite green even though coverage remains high.

### Do Not Trigger When

- Coverage is used as a map of unvisited risk after meaningful behavioral assertions already exist.
- A smoke test intentionally proves only “the process starts” or “the endpoint responds,” and nobody claims it proves deeper semantics.
- A broad property test touches many branches while asserting a real invariant capable of failing.
- A test is narrow but protects the exact public behavior the change threatens; low coverage elsewhere is irrelevant to that claim.

### Distinguish From

`false-gate` means green is structurally disconnected from the advertised property. `coverage-theater` can have perfectly functioning tests and CI; the problem is that the proposition being tested is too weak.

`test-implementation-coupled` may contain many assertions, but they constrain private choreography instead of useful behavior. `weakened-test-to-pass` begins when a formerly meaningful proposition is deliberately softened under pressure from red.

### Decision Procedure

For every test presented as evidence, ask:

> Name one realistic defect in the changed behavior that would make this test fail.

If the answer is “the line would not run,” “the mock would not be called,” or “coverage would drop,” keep asking. What wrong caller-visible result or violated invariant is actually rejected?

Then mentally mutate the implementation: swap IDs, drop an error, reverse order, skip authorization, return stale state. If the test remains green, its execution count is not proof of that property.

### Examples

- positive: all branches of a parser execute, but the only assertion is `result !== undefined`; malformed input is silently accepted and coverage stays 100%.
- positive: a service test snapshots a 500-line object; reviewers routinely update the snapshot wholesale without identifying which fields are contractual.
- positive: a test verifies `repository.save` was called once but never asserts that the correct durable state was saved.
- near-miss: coverage highlights an unvisited cancellation branch, prompting a new test that asserts cancellation actually prevents publication.
- counterexample: a small contract test covers few implementation lines but fails if authorization, identity, or result semantics are wrong.

## Advice

### What To Do Now

Stop optimizing the number and write down the behavior that must become hard to break.

For each important test, replace traversal-oriented assertions with a proposition a realistic defect can falsify: exact identity, authorization, error semantics, ordering, durable state, idempotence, cancellation, boundary translation, or whatever the caller actually depends on.

Keep coverage as a map afterward. Do not let the map become the territory.

### Why This Matters

Coverage theater makes teams feel safer precisely while their tests become less adversarial.

A test suite should create resistance. Some implementations should be rejected even if they execute every line. When success is measured mainly by how much code ran, authors learn to write tests that accompany the implementation rather than challenge it.

The result is an expensive suite with low defect-detection power: refactors are slow because many tests exist, yet regressions still escape because few tests defend meaning.

### Repair Strategy

Work from the contract inward:

1. name the caller-visible promise or invariant;
2. name a plausible defect that violates it;
3. choose the smallest input that exposes the distinction;
4. assert the observable consequence;
5. only then inspect coverage to find adjacent unvisited risk.

When a test currently uses mocks, ask whether the mock interaction is itself the contract. If not, assert the outcome that interaction is supposed to create. A call count is evidence only when “called exactly once” is genuinely the behavior that matters.

Treat snapshots as compressed assertions only when review can identify the semantically significant fields. If every change produces a giant opaque snapshot diff that gets accepted by regeneration, split or replace it.

### Decision Branches

- **Only truthiness / non-null / no-throw is asserted:** strengthen the assertion to the specific result or invariant the caller needs.
- **Mock choreography dominates:** move outward to a stable contract unless the interaction itself is the public guarantee.
- **Coverage threshold is driving useless tests:** preserve or adjust the metric only after meaningful tests own the important behavior. Never manufacture tests solely to satisfy a percentage.
- **Uncovered code is genuinely unreachable/dead:** delete it rather than write a ceremonial test to paint it green.
- **Coverage is low on a high-risk branch:** use the report as a lead, then write a falsifiable behavioral test.

### Common Wrong Fixes

- Add tests that merely instantiate classes, call methods, or assert values are defined.
- Assert every private helper call so the suite becomes a mirror of current implementation.
- Generate or update huge snapshots without articulating the contract they protect.
- Lower the threshold and declare the problem solved. A bad threshold may deserve removal, but removing a metric does not create missing behavioral evidence.
- Chase 100% branch coverage on trivial glue while leaving one causal boundary effectively untested.
- Celebrate mutation score, coverage percentage, or test count without being able to name which business/system invariants those numbers defend.

### Verification

Perform a deliberate semantic mutation against the property the test claims to protect. Examples:

- swap two domain IDs;
- suppress the expected error;
- publish before persistence;
- accept an unauthorized caller;
- return stale state;
- drop cancellation;
- reverse a required order.

The relevant test must turn red for the right reason.

The invariant is:

> Important tests reject plausible wrong behavior; coverage is merely a by-product of asking those questions.

### Done When

The suite can explain its confidence in behavior without quoting a percentage.

Coverage may remain useful, but nobody needs it to pretend execution itself was verification.

### Nudge

Coverage tells you where the flashlight passed.

Verification asks whether you would notice the thief.
