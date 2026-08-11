# context-model-leak — Enforcer

## Definition
A context model leaks when one data type is reused across domains that assign its fields different meanings, invariants, lifetimes, or authority. The root-cause is that one type is reused across contexts that assign its fields different meanings, so a field added for one question becomes apparently meaningful everywhere.

## Governing Principle
Sameness of shape is not sameness of concept. A user in authentication, an account in billing, and a participant in a session may share an identifier and display name while answering entirely different questions. Reusing one model makes those contexts share more knowledge than the domain does, so a field added for one purpose silently becomes visible—and apparently meaningful—everywhere.

## Trigger When
Trigger when authentication, ordering, persistence, UI, sessions, reporting, or other bounded contexts pass around one shared “master” model despite different rules and reasons to change.

## Do Not Trigger When
- The shared type is intentionally a tiny stable value object whose meaning and invariant are truly identical in every context.
- A context-local type is translated at the boundary and does not import a foreign master model.
- Identifiers shared as opaque IDs are facts, not leaked models, when no foreign fields travel with them.
- Persistence rows mapped to one context’s model inside that context are not cross-context reuse.

## Distinguish From
`boundary-collapse` is the broader loss of context isolation. `duplicated-truth` is multiple authorities for one fact. This rule concerns one representation pretending to be several distinct domain concepts. Tie-break: if the same type is asked to answer different contexts’ questions, this rule owns the case even when folders remain separate.

## Decision Procedure
For each context, ask what questions the model must answer and which fields are meaningful there. If the answers differ, define context-local concepts and translate only the shared facts.

## Examples
- positive: one `User` type carries auth credentials, billing balance, and UI theme because the columns looked similar.
- near-miss: a shared `Money` value object with identical meaning and invariant in every context.
- counterexample: each bounded context owns its model and translates only the small set of crossing facts.

## Nudge
Do not let structural similarity erase semantic boundaries. Give each bounded context the model its questions require, then translate explicitly between meanings.
