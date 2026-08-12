# coverage-theater — Main

## What To Do Now
Stop optimizing the number and write down the behavior that must become hard to break.

For each important test, replace traversal-oriented assertions with a proposition a realistic defect can falsify: exact identity, authorization, error semantics, ordering, durable state, idempotence, cancellation, boundary translation, or whatever the caller actually depends on.

Keep coverage as a map afterward. Do not let the map become the territory.

## Why This Matters
Coverage theater makes teams feel safer precisely while their tests become less adversarial.

A test suite should create resistance. Some implementations should be rejected even if they execute every line. When success is measured mainly by how much code ran, authors learn to write tests that accompany the implementation rather than challenge it.

The result is an expensive suite with low defect-detection power: refactors are slow because many tests exist, yet regressions still escape because few tests defend meaning.

## Repair Strategy
Work from the contract inward:

1. name the caller-visible promise or invariant;
2. name a plausible defect that violates it;
3. choose the smallest input that exposes the distinction;
4. assert the observable consequence;
5. only then inspect coverage to find adjacent unvisited risk.

When a test currently uses mocks, ask whether the mock interaction is itself the contract. If not, assert the outcome that interaction is supposed to create. A call count is evidence only when “called exactly once” is genuinely the behavior that matters.

Treat snapshots as compressed assertions only when review can identify the semantically significant fields. If every change produces a giant opaque snapshot diff that gets accepted by regeneration, split or replace it.

## Decision Branches
- **Only truthiness / non-null / no-throw is asserted:** strengthen the assertion to the specific result or invariant the caller needs.
- **Mock choreography dominates:** move outward to a stable contract unless the interaction itself is the public guarantee.
- **Coverage threshold is driving useless tests:** preserve or adjust the metric only after meaningful tests own the important behavior. Never manufacture tests solely to satisfy a percentage.
- **Uncovered code is genuinely unreachable/dead:** delete it rather than write a ceremonial test to paint it green.
- **Coverage is low on a high-risk branch:** use the report as a lead, then write a falsifiable behavioral test.

## Common Wrong Fixes
- Add tests that merely instantiate classes, call methods, or assert values are defined.
- Assert every private helper call so the suite becomes a mirror of current implementation.
- Generate or update huge snapshots without articulating the contract they protect.
- Lower the threshold and declare the problem solved. A bad threshold may deserve removal, but removing a metric does not create missing behavioral evidence.
- Chase 100% branch coverage on trivial glue while leaving one causal boundary effectively untested.
- Celebrate mutation score, coverage percentage, or test count without being able to name which business/system invariants those numbers defend.

## Verification
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

## Done When
The suite can explain its confidence in behavior without quoting a percentage.

Coverage may remain useful, but nobody needs it to pretend execution itself was verification.
