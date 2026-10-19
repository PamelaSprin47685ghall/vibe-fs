# context-model-leak — Lesson

## Opinion

### Definition

A context model leaks when one data type is reused across domains that assign its fields different meanings, invariants, lifetimes, or authority. The root-cause is that one type is reused across contexts that assign its fields different meanings, so a field added for one question becomes apparently meaningful everywhere.

### Governing Principle

Sameness of shape is not sameness of concept. A user in authentication, an account in billing, and a participant in a session may share an identifier and display name while answering entirely different questions. Reusing one model makes those contexts share more knowledge than the domain does, so a field added for one purpose silently becomes visible—and apparently meaningful—everywhere.

### Trigger When

Trigger when authentication, ordering, persistence, UI, sessions, reporting, or other bounded contexts pass around one shared “master” model despite different rules and reasons to change.

### Do Not Trigger When

- The shared type is intentionally a tiny stable value object whose meaning and invariant are truly identical in every context.
- A context-local type is translated at the boundary and does not import a foreign master model.
- Identifiers shared as opaque IDs are facts, not leaked models, when no foreign fields travel with them.
- Persistence rows mapped to one context’s model inside that context are not cross-context reuse.

### Distinguish From

`boundary-collapse` is the broader loss of context isolation. `duplicated-truth` is multiple authorities for one fact. This rule concerns one representation pretending to be several distinct domain concepts. Tie-break: if the same type is asked to answer different contexts’ questions, this rule owns the case even when folders remain separate.

### Decision Procedure

For each context, ask what questions the model must answer and which fields are meaningful there. If the answers differ, define context-local concepts and translate only the shared facts.

### Examples

- positive: one `User` type carries auth credentials, billing balance, and UI theme because the columns looked similar.
- near-miss: a shared `Money` value object with identical meaning and invariant in every context.
- counterexample: each bounded context owns its model and translates only the small set of crossing facts.

## Advice

### What To Do Now

Split the shared model into context-owned types and define explicit translations for the small set of facts that legitimately cross boundaries. Each bounded context is who owns the model its questions require; a structurally similar master type is not who owns every context’s meaning.

### Why This Matters

A universal model creates semantic coupling disguised as reuse. Changes become contagious because every context can see fields introduced for every other context. Optional fields accumulate to represent “not meaningful here,” and authorization or lifecycle rules blur because the type no longer tells which interpretation is active.

### Repair Strategy

Start from each context’s questions and invariants, not from the existing field list. Create the smallest model that makes those rules natural. Translate identifiers and stable facts at the boundary instead of importing a foreign context wholesale.

### Decision Branches

- If one type answers different contexts’ questions, split it and translate only shared facts.
- If a field is meaningless in a context, it must be unrepresentable there, not nullable on a master object.
- If a tiny value object truly has identical meaning everywhere, sharing it is not a leak.

### Common Wrong Fixes

- Do not add more nullable fields to the universal model.
- Do not add view flags or context enums to keep one mega-type.
- Do not copy the master model into each package without changing ownership of meaning.
- Do not “namespace” fields on the same object (`authEmail`, `billingEmail`) as a substitute for split types.

### Verification

A context should compile and make sense using only its own model plus explicit boundary contracts. Fields irrelevant to that context should be unrepresentable there. The invariant is that each model has one semantic owner and one reason to change.

### Done When

Each model has one semantic owner and one reason to change, while cross-context communication carries facts rather than leaking another context’s representation.

### Nudge

Do not let structural similarity erase semantic boundaries. Give each bounded context the model its questions require, then translate explicitly between meanings.
