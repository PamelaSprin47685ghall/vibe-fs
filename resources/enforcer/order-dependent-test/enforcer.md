# order-dependent-test — Enforcer

An order-dependent test has hidden premises supplied by **what the suite happened to run before it**.

A test case should be a proposition with local premises:

```text
given this setup
when this action occurs
then this observable must hold
```

Order dependence quietly changes that proposition into:

```text
given this setup
plus whatever globals/files/rows/env/caches/ports/mocks the previous tests left behind
when this action occurs
then maybe this observable holds
```

At that point the suite is no longer a set of independent proofs. It is one giant undocumented state machine whose transition order happens to be the test runner's schedule.

Fire this rule when:

- a test passes in the full suite but fails alone;
- a test fails first but passes after another case warms cache or creates data;
- cases share mutable database rows, temp directories, process-wide registries, singleton state, mock cursors, clocks, environment variables, current working directory, ports, or files;
- cleanup for test A is actually performed by test B or a later global teardown;
- `beforeAll` creates mutable state that individual tests consume/modify in sequence;
- the runner must be forced into a fixed order to keep verdicts stable;
- parallelization reveals failures because supposedly independent cases are really sharing premises;
- a test relies on an ID counter / random seed / global provider preference already advanced by neighboring tests.

Do not fire when the order is the **scenario itself**. “Create order, approve it, then ship it” is a legitimate lifecycle if those steps are modeled as one explicit test/scenario with one owner and one setup/teardown. The smell appears when those steps masquerade as independent tests whose names and runner order secretly provide the lifecycle.

Shared fixtures are not automatically wrong either. Immutable package data, read-only constants, expensive process-wide services with isolated per-test namespaces, or a database fixture that gives each case a fresh transaction/schema can be shared without sharing semantic premises.

Distinguish from `flaky-test-tolerated`: order dependence is one concrete source of nondeterminism; `flaky-test-tolerated` is the policy failure of accepting unstable verdicts. `mock-hidden-state` is more specific when the invisible premise lives inside a stateful mock. `resource-not-scoped` may explain why residue survives, but this rule is about residue changing another test's truth value.

The decisive experiment is not “shuffle a few times and see.” Run the case:

- alone;
- first;
- last;
- after each plausible contaminating neighbor;
- under parallel/randomized order where supported.

If its verdict changes while its own explicit inputs do not, suite history is an undeclared input.

The repair is either to make the test own every mutable premise and discharge it locally, or to admit the operations form one lifecycle and combine them into one explicit scenario. Do not encode hidden causality as filename order, numeric test prefixes, `--runInBand`, or “please don't parallelize this folder.”

> A test should remember only what its scenario explicitly gives it. Suite history is not a legitimate fixture.