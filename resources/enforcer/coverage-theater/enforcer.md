# coverage-theater — Enforcer

## Definition
Coverage theater happens when **execution is presented as proof**.

The code was reached. The branch counter moved. The file appeared in a report. None of those facts establish that the behavior was correct.

The defect is not “coverage is bad.” Coverage is useful reconnaissance. The defect begins when reachability metrics are allowed to impersonate behavioral evidence.

## Governing Principle
A test earns its keep by making some plausible wrong implementation unacceptable.

If a test would stay green when the returned identity is swapped, an error is swallowed, an ordering guarantee is reversed, an authorization check is bypassed, or a state transition is wrong, then hitting the relevant lines has not verified those properties.

Modern coverage dashboards make this pathology seductive because they produce precise numbers. `94.7%` looks scientific. But precision about the wrong quantity is still ignorance. Line coverage can tell you where execution went. It cannot tell you whether the journey meant anything.

## Trigger When
Trigger when coverage or traversal is used as the central evidence for correctness while the assertions fail to distinguish realistic defects. Common forms:

- tests call every method but assert only “not null,” “defined,” or “did not throw”;
- snapshot assertions are so broad that nobody can name which semantic changes should fail them;
- mocks are satisfied because calls occurred, while caller-visible outcome is never asserted;
- tests exercise both branches but do not assert the branch-specific invariant;
- a coverage threshold drives the addition of low-information tests whose only purpose is to color lines green;
- mutation of a meaningful outcome would leave the suite green even though coverage remains high.

## Do Not Trigger When
- Coverage is used as a map of unvisited risk after meaningful behavioral assertions already exist.
- A smoke test intentionally proves only “the process starts” or “the endpoint responds,” and nobody claims it proves deeper semantics.
- A broad property test touches many branches while asserting a real invariant capable of failing.
- A test is narrow but protects the exact public behavior the change threatens; low coverage elsewhere is irrelevant to that claim.

## Distinguish From
`false-gate` means green is structurally disconnected from the advertised property. `coverage-theater` can have perfectly functioning tests and CI; the problem is that the proposition being tested is too weak.

`test-implementation-coupled` may contain many assertions, but they constrain private choreography instead of useful behavior. `weakened-test-to-pass` begins when a formerly meaningful proposition is deliberately softened under pressure from red.

## Decision Procedure
For every test presented as evidence, ask:

> Name one realistic defect in the changed behavior that would make this test fail.

If the answer is “the line would not run,” “the mock would not be called,” or “coverage would drop,” keep asking. What wrong caller-visible result or violated invariant is actually rejected?

Then mentally mutate the implementation: swap IDs, drop an error, reverse order, skip authorization, return stale state. If the test remains green, its execution count is not proof of that property.

## Examples
- positive: all branches of a parser execute, but the only assertion is `result !== undefined`; malformed input is silently accepted and coverage stays 100%.
- positive: a service test snapshots a 500-line object; reviewers routinely update the snapshot wholesale without identifying which fields are contractual.
- positive: a test verifies `repository.save` was called once but never asserts that the correct durable state was saved.
- near-miss: coverage highlights an unvisited cancellation branch, prompting a new test that asserts cancellation actually prevents publication.
- counterexample: a small contract test covers few implementation lines but fails if authorization, identity, or result semantics are wrong.

## Nudge
Coverage tells you where the flashlight passed.

Verification asks whether you would notice the thief.
