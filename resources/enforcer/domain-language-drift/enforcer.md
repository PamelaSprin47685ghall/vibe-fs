# domain-language-drift — Enforcer

## Definition
Domain language drifts when one concept accumulates several names or one name is stretched across several concepts, so vocabulary no longer preserves identity.

## Governing Principle
Names are the join keys between code, tests, documentation, and human discussion. When vocabulary drifts, those representations can no longer be joined reliably: two engineers may agree on words while disagreeing on concepts, or disagree on words while describing the same fact. The resulting ambiguity is not cosmetic; it destroys the possibility of local reasoning because every term requires contextual disambiguation.

## Trigger When
Trigger when synonyms proliferate for one domain concept, or one overloaded term carries different meanings across modules without explicit context boundaries.

## Do Not Trigger When
- Different bounded contexts intentionally use different ubiquitous languages and translate explicitly at their border.
- A single identifier is wrong about its own meaning; that is a false claim, not vocabulary split (`misleading-name`).
- Technical synonyms at an adapter (`httpStatus` vs domain `Refusal`) are explicit translations, not silent drift inside one context.
- Established public protocol names (HTTP, UUID) are shared vocabulary, not local renaming of a domain concept.

## Distinguish From
`misleading-name` concerns one identifier making a false claim. `context-model-leak` shares representation across contexts. This rule concerns the vocabulary itself losing a bijective relation with concepts. Tie-break: if the spelled meaning is locally right but the same concept has several names, or one name several concepts, this rule owns the case.

## Decision Procedure
Build a small glossary from actual code and discussion. For each term ask “one concept?” and for each concept ask “one term within this context?” Split overloads and collapse accidental synonyms.

## Examples
- positive: billing uses `amount`, `prcAmt`, and `total` for the same money fact with no stated distinction.
- near-miss: two contexts keep `Order` vs `Purchase` and translate at the border.
- counterexample: pick one canonical term in the owning context and rename code, tests, events, and docs to it.

## Nudge
Vocabulary is part of the model. Within a context, give one concept one stable name and do not make one name carry unrelated meanings.
