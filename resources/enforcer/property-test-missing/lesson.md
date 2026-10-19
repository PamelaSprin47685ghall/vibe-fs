# property-test-missing — Lesson

## Opinion

### Definition

A property test is missing when the implementation claims a **general law over a space of inputs**, while the test suite offers only a few curated anecdotes.

The trigger is the quantifier, not the sophistication of the code.

### Governing Principle

If correctness is naturally expressed with "for all," a handful of hand-picked fixtures usually leaves most of the claim unexamined.

### Trigger When

Fire this rule when code owns stable laws such as:

- serialization/codec round trips;
- normalization/canonicalization idempotency;
- parser/printer correspondence;
- algebraic merge/fold properties;
- ordering/permutation invariance;
- state-machine invariant preservation;
- encode/decode identity under generated valid structures;
- monotonicity or boundedness over broad numeric/state spaces;
- deterministic equivalence between two representations.

### Do Not Trigger When

Do **not** fire merely because "property testing is powerful." Many behaviors have no useful generative law. One-off orchestration, specific product copy, a fixed migration fixture, or a four-case closed enum already exhaustively table-tested may be better served by examples.

Random input without a law is not property testing. `forAll(randomBytes, x => doesNotThrow(x))` proves only non-crash tolerance if that is genuinely the contract; otherwise it is noise with impressive volume.

The quality of the generator matters as much as the assertion. A generator that filters away difficult states, never creates empty/maximal/duplicate/recursive combinations, or mirrors implementation constructors too faithfully can leave the dangerous region untouched.

### Distinguish From

This rule differs from `coverage-theater`: a property suite can still be theater if its assertion is meaningless. It differs from `failure-path-untested`: that rule may concern one specific negative branch, while this rule concerns a known universal relationship. `missing-regression-test` preserves a concrete discovered counterexample; property tests protect the wider law around it.

### Decision Procedure

The decisive question is:

> Can the correctness claim be stated honestly as "for all valid X..." or as an algebraic relation among generated values?

If yes, examples are illustrations, not sufficient evidence by themselves.

A strong property should have useful shrinking. When it fails, the framework should search toward a minimal counterexample humans can understand and preserve.

### Examples

Examples prove points:

```text
f(a) = b
f(c) = d
```

Properties claim spaces:

```text
for every valid x, decode(encode(x)) = x
for every x, normalize(normalize(x)) = normalize(x)
for every permitted a,b, merge(a,b) preserves invariant I
for every reachable transition, invariant P remains true
```

## Advice

### What To Do Now

Write the law before writing the generator.

A useful property test begins with a semantic statement independent of implementation:

```text
for every valid x, P(x)
for every valid x,y, relation R(f(x,y), x, y) holds
for every permutation p, result(p(inputs)) = canonicalResult
```

Only then choose generators that explore the real domain rather than merely producing easy values.

### Why This Matters

Good generators should deliberately reach boundaries the implementation is tempted to mishandle: empty and singleton collections, duplicates, maximal/minimal values, recursive depth, unusual Unicode, equivalent representations, stale/current versions, conflicting cases, permutations, malformed-but-parseable boundaries where relevant.

Avoid excessive filtering. If 99% of generated values are discarded because they are "invalid," the generator probably does not understand the domain. Prefer construction that produces valid cases directly, plus separate generators for intentionally invalid inputs when rejection is part of the law.

Shrinking is part of the evidence. A failing case like "347 nested randomly generated objects" teaches less than a minimized counterexample showing exactly which combination violates the invariant. Configure custom shrinkers when default shrinking destroys the domain condition or hides the failure.

### Repair Strategy

When a property finds a real defect, keep the minimized counterexample as a regression example when it carries explanatory value, and keep the property so nearby unseen cases remain protected.

### Decision Branches

Keep examples. They document intent, named edge cases, and historical bugs. Property tests do not replace examples; they extend evidence from selected points to a quantified space.

### Common Wrong Fixes

Common fake repairs:

- generate random values but assert only `doesNotThrow` when correctness requires more;
- seed randomness without preserving the failing seed/counterexample;
- filter until only happy inputs survive;
- make generators call the same production normalization/constructor logic whose behavior the property is meant to challenge;
- replace all readable examples with opaque generative tests;
- use property testing on a four-case finite enum instead of simply enumerating all four cases;
- run thousands of cases with a weak property and call volume rigor.

### Verification

Verification should mutate the law in plausible ways: break round-trip identity for one field, make normalization non-idempotent, make merge order-sensitive, drop a transition invariant. The property should discover a small counterexample rather than merely pass enormous random traffic.

### Done When

You are done when a general claim is guarded by general evidence, the generator genuinely reaches difficult parts of the domain, and failures shrink to explanations rather than noise.

> Property testing is not randomness. It is executable mathematics with a search strategy.

### Nudge

> When the code promises a law, test the law. Do not confuse three memorable examples with evidence about an input universe.
