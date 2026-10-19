# domain-language-drift — Lesson

## Opinion

### Definition

Domain language drifts when one concept accumulates several names or one name is stretched across several concepts, so vocabulary no longer preserves identity. The root-cause is that vocabulary inside one context stopped being a one-to-one map from term to concept.

### Governing Principle

Names are the join keys between code, tests, documentation, and human discussion. When vocabulary drifts, those representations can no longer be joined reliably: two engineers may agree on words while disagreeing on concepts, or disagree on words while describing the same fact. The resulting ambiguity is not cosmetic; it destroys the possibility of local reasoning because every term requires contextual disambiguation.

### Trigger When

Trigger when synonyms proliferate for one domain concept, or one overloaded term carries different meanings across modules without explicit context boundaries.

### Do Not Trigger When

- Different bounded contexts intentionally use different ubiquitous languages and translate explicitly at their border.
- A single identifier is wrong about its own meaning; that is a false claim, not vocabulary split (`misleading-name`).
- Technical synonyms at an adapter (`httpStatus` vs domain `Refusal`) are explicit translations, not silent drift inside one context.
- Established public protocol names (HTTP, UUID) are shared vocabulary, not local renaming of a domain concept.

### Distinguish From

`misleading-name` concerns one identifier making a false claim. `context-model-leak` shares representation across contexts. This rule concerns the vocabulary itself losing a bijective relation with concepts. Tie-break: if the spelled meaning is locally right but the same concept has several names, or one name several concepts, this rule owns the case.

### Decision Procedure

Build a small glossary from actual code and discussion. For each term ask “one concept?” and for each concept ask “one term within this context?” Split overloads and collapse accidental synonyms.

### Examples

- positive: billing uses `amount`, `prcAmt`, and `total` for the same money fact with no stated distinction.
- near-miss: two contexts keep `Order` vs `Purchase` and translate at the border.
- counterexample: pick one canonical term in the owning context and rename code, tests, events, and docs to it.

## Advice

### What To Do Now

Choose the canonical domain term for each concept within the owning context and rename code, tests, events, and docs until that vocabulary is consistent. The bounded context is who owns each concept’s canonical term; adapters are who owns translation only at a genuine border.

### Why This Matters

A stable vocabulary compresses reasoning: once a term is learned, every occurrence recalls the same concept. Drift reverses that compression. Readers must repeatedly ask which synonym or meaning is active, and cross-team discussion loses the guarantee that identical words denote identical things.

### Repair Strategy

Start from domain distinctions, not existing identifiers. Separate overloaded concepts first, then converge accidental synonyms. At bounded-context borders, preserve local language and make translation explicit instead of forcing a false global vocabulary.

### Decision Branches

- If one concept has several names in the same context, converge on the canonical term and retire synonyms.
- If one name covers several concepts, split the names before renaming anything else.
- If two contexts intentionally differ, keep both languages and add an explicit translation at the border.

### Common Wrong Fixes

- Do not create a glossary that excuses inconsistent code indefinitely.
- Do not mechanically rename distinct context concepts merely because their English words resemble each other.
- Do not introduce a third “neutral” name that becomes another synonym.
- Do not leave events and tests on the old vocabulary after the types were renamed.

### Verification

Search for retired synonyms and overloaded uses. A domain expert and a code reader should be able to point to the same term and mean the same thing in the same context. The invariant is a one-to-one map from term to concept within a context, with translation only at genuine borders.

### Done When

Names, types, tests, and documentation form one coherent language map, with explicit translation only where genuine context boundaries require it.

### Nudge

Vocabulary is part of the model. Within a context, give one concept one stable name and do not make one name carry unrelated meanings.
