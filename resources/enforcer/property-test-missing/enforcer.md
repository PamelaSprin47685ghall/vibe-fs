# property-test-missing — Enforcer

A property test is missing when the implementation claims a **general law over a space of inputs**, while the test suite offers only a few curated anecdotes.

The trigger is the quantifier, not the sophistication of the code.

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

If correctness is naturally expressed with “for all,” a handful of hand-picked fixtures usually leaves most of the claim unexamined.

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

Do **not** fire merely because “property testing is powerful.” Many behaviors have no useful generative law. One-off orchestration, specific product copy, a fixed migration fixture, or a four-case closed enum already exhaustively table-tested may be better served by examples.

Random input without a law is not property testing. `forAll(randomBytes, x => doesNotThrow(x))` proves only non-crash tolerance if that is genuinely the contract; otherwise it is noise with impressive volume.

The quality of the generator matters as much as the assertion. A generator that filters away difficult states, never creates empty/maximal/duplicate/recursive combinations, or mirrors implementation constructors too faithfully can leave the dangerous region untouched.

This rule differs from `coverage-theater`: a property suite can still be theater if its assertion is meaningless. It differs from `failure-path-untested`: that rule may concern one specific negative branch, while this rule concerns a known universal relationship. `missing-regression-test` preserves a concrete discovered counterexample; property tests protect the wider law around it.

The decisive question is:

> Can the correctness claim be stated honestly as “for all valid X...” or as an algebraic relation among generated values?

If yes, examples are illustrations, not sufficient evidence by themselves.

A strong property should have useful shrinking. When it fails, the framework should search toward a minimal counterexample humans can understand and preserve.

> When the code promises a law, test the law. Do not confuse three memorable examples with evidence about an input universe.