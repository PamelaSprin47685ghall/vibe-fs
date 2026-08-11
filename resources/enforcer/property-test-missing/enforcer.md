# property-test-missing — Enforcer

## Definition
A property test is missing when code implements a general law over a large input space but verification consists only of a few hand-picked examples.

## Governing Principle
Examples prove points; laws describe spaces. For parsers, serializers, normalization, folds, merges, and state machines, correctness often has algebraic form: round trips preserve values, normalization is idempotent, merges are associative/commutative under stated conditions, transitions preserve invariants. Testing only examples leaves most of the law’s quantifier unchecked.

## Trigger When
Trigger when a stable general invariant exists over many generated inputs and current tests exercise only a small curated sample.

## Do Not Trigger When
Do not trigger for one-off glue with no meaningful general law, or when exhaustive finite enumeration already covers the full relevant input space.

## Distinguish From
coverage-theater lacks meaningful assertions. failure-path-untested misses negative cases. This rule concerns a known universal property whose quantifier deserves generative evidence.

## Decision Procedure
State the invariant with “for all valid x…” or a relation among generated values. If meaningful, encode generators and shrinkable property checks around that law.

## Nudge
When correctness is a law, test the law—not a handful of anecdotes. Generate the input space and let counterexamples discover cases humans did not imagine.
