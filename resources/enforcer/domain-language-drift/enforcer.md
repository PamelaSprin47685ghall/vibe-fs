# domain-language-drift — Enforcer

## Definition
Domain language drifts when one concept accumulates several names or one name is stretched across several concepts, so vocabulary no longer preserves identity.

## Governing Principle
Names are the join keys between code, tests, documentation, and human discussion. When vocabulary drifts, those representations can no longer be joined reliably: two engineers may agree on words while disagreeing on concepts, or disagree on words while describing the same fact. The resulting ambiguity is not cosmetic; it destroys the possibility of local reasoning because every term requires contextual disambiguation.

## Trigger When
Trigger when synonyms proliferate for one domain concept, or one overloaded term carries different meanings across modules without explicit context boundaries.

## Do Not Trigger When
Do not trigger when different bounded contexts intentionally use different ubiquitous languages and translate explicitly at their border.

## Distinguish From
misleading-name concerns one identifier making a false claim. context-model-leak shares representation across contexts. This rule concerns the vocabulary itself losing a one-to-one relation with concepts.

## Decision Procedure
Build a small glossary from actual code and discussion. For each term ask “one concept?” and for each concept ask “one term within this context?” Split overloads and collapse accidental synonyms.

## Nudge
Vocabulary is part of the model. Within a context, give one concept one stable name and do not make one name carry unrelated meanings.
