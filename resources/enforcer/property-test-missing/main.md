# property-test-missing — Main

Write the law before writing the generator.

A useful property test begins with a semantic statement independent of implementation:

```text
for every valid x, P(x)
for every valid x,y, relation R(f(x,y), x, y) holds
for every permutation p, result(p(inputs)) = canonicalResult
```

Only then choose generators that explore the real domain rather than merely producing easy values.

Good generators should deliberately reach boundaries the implementation is tempted to mishandle: empty and singleton collections, duplicates, maximal/minimal values, recursive depth, unusual Unicode, equivalent representations, stale/current versions, conflicting cases, permutations, malformed-but-parseable boundaries where relevant.

Avoid excessive filtering. If 99% of generated values are discarded because they are “invalid,” the generator probably does not understand the domain. Prefer construction that produces valid cases directly, plus separate generators for intentionally invalid inputs when rejection is part of the law.

Shrinking is part of the evidence. A failing case like “347 nested randomly generated objects” teaches less than a minimized counterexample showing exactly which combination violates the invariant. Configure custom shrinkers when default shrinking destroys the domain condition or hides the failure.

Common fake repairs:

- generate random values but assert only `doesNotThrow` when correctness requires more;
- seed randomness without preserving the failing seed/counterexample;
- filter until only happy inputs survive;
- make generators call the same production normalization/constructor logic whose behavior the property is meant to challenge;
- replace all readable examples with opaque generative tests;
- use property testing on a four-case finite enum instead of simply enumerating all four cases;
- run thousands of cases with a weak property and call volume rigor.

When a property finds a real defect, keep the minimized counterexample as a regression example when it carries explanatory value, and keep the property so nearby unseen cases remain protected.

Verification should mutate the law in plausible ways: break round-trip identity for one field, make normalization non-idempotent, make merge order-sensitive, drop a transition invariant. The property should discover a small counterexample rather than merely pass enormous random traffic.

Keep examples. They document intent, named edge cases, and historical bugs. Property tests do not replace examples; they extend evidence from selected points to a quantified space.

You are done when a general claim is guarded by general evidence, the generator genuinely reaches difficult parts of the domain, and failures shrink to explanations rather than noise.

> Property testing is not randomness. It is executable mathematics with a search strategy.