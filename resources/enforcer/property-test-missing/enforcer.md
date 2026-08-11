# property-test-missing — Enforcer

## Definition
A property test is missing when code implements a general law over a large input space but verification consists only of a few hand-picked examples.

## Governing Principle
Examples prove points; laws describe spaces. For parsers, serializers, normalization, folds, merges, and state machines, correctness often has algebraic form: round trips preserve values, normalization is idempotent, merges are associative/commutative under stated conditions, transitions preserve invariants. Testing only examples leaves most of the law’s quantifier unchecked.

## Trigger When
Trigger when a stable general invariant exists over many generated inputs and current tests exercise only a small curated sample.

## Do Not Trigger When
- One-off glue or orchestration has no meaningful general law to quantify over.
- Exhaustive finite enumeration already covers the full relevant input space.
- Existing tests already encode the law as a generative property with shrinking, even if a few examples remain as illustrations.
- The change is a single documented fixture whose acceptance criterion is that exact example, not a universal claim.

## Distinguish From
coverage-theater lacks meaningful assertions. failure-path-untested misses negative cases. This rule concerns a known universal property whose quantifier deserves generative evidence. Tie-break: fire here when the law is already known and examples merely sample it; fire coverage-theater when volume of tests hides empty assertions; fire failure-path-untested when the missing evidence is a specific negative path, not a quantified space.

## Decision Procedure
State the invariant with “for all valid x…” or a relation among generated values. If meaningful, encode generators and shrinkable property checks around that law.

## Examples
- positive: a serializer claims round-trip identity for every valid document, yet tests assert only three hand-written fixtures.
- near-miss: a finite enum of four protocol versions is exhaustively table-tested; no generator is needed because the space is already fully covered.
- counterexample: glue code maps one config flag to one CLI argument with no algebraic law; example tests are the right evidence.

## Nudge
When correctness is a law, test the law—not a handful of anecdotes. Generate the input space and let counterexamples discover cases humans did not imagine.
