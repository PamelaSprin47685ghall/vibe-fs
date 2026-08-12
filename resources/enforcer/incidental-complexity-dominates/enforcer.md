# incidental-complexity-dominates — Enforcer

## Definition
Incidental complexity dominates when understanding or changing a simple domain fact requires reasoning through more machinery than the fact itself deserves.

The smell is not “there are many files” or “the code is long.” The smell is **semantic displacement**: wrappers, adapters, flags, configuration, lifecycle glue, serializers, registries, compatibility paths, orchestration, and framework ceremony become the real thing engineers must understand, while the domain rule they supposedly support is a footnote.

## Governing Principle
Essential complexity is the part reality refuses to let you delete. Accidental complexity is the part your chosen representation invented.

A payment system may genuinely need idempotence, durable state, authorization, and partial-failure handling. It does not therefore need six DTO translations, three lifecycle flags reconstructing the same state, two parallel configuration surfaces, or a registry whose only purpose is locating code the type system already names.

The disease begins when the implementation's invented ontology becomes richer than the problem's real ontology.

## Trigger When
Trigger when a maintainer must spend most of the reasoning budget on solution-imposed machinery rather than domain distinctions. Common signs:

- one domain action crosses several wrappers that add no authority, persistence, isolation, or meaningful contract;
- the same fact is translated repeatedly between near-identical shapes inside one trust boundary;
- lifecycle flags/status fields reconstruct facts already available from durable truth;
- configuration exists mainly to parameterize internal indirection nobody independently needs;
- a tiny behavior change requires coordinated edits across many plumbing layers with no corresponding change in semantics;
- control flow is discoverable only by following registrations, callbacks, factories, generated bindings, and side tables;
- compatibility, migration, or “temporary” paths outlive every named external consumer;
- tests spend more setup on framework wiring than on the behavior they are meant to distinguish.

## Do Not Trigger When
- The complexity protects a real boundary: process isolation, authority, persistence, external protocol, independent deployment, failure containment, or another consequence that would be lost by collapse.
- The domain itself is genuinely complex; many states or files may be the simplest honest representation.
- Repetition is deliberate at a boundary because two sides own different models and translation is semantic work.
- Infrastructure ceremony is forced by an external platform and localized behind a narrow adapter rather than spread through the core.
- The code is verbose but conceptually direct. Verbosity alone is not incidental complexity.

## Distinguish From
`framework-tax` is a common species where framework lifecycle/configuration becomes the dominant ontology. `translator-layer-bloat` concerns repeated shape conversion. `facade-hides-mess` hides complexity behind a clean front without reducing it. `god-module` centralizes unrelated sovereignty.

Use this rule when the central observation is broader: the solution has made its own machinery harder to understand than the problem.

## Decision Procedure
State the domain operation in plain technical language. Then draw the minimum facts that must exist for it to be correct: owners, states, effects, durable facts, failure boundaries.

Now walk the implementation and ask of each layer/state/translation:

> What real distinction disappears if this mechanism is removed?

If the answer is “none; another layer already knows the same thing,” “it exists because the framework pattern expects it,” or “we might need it later,” the complexity is accidental.

## Examples
- positive: updating one user preference requires Controller DTO → Service DTO → Command DTO → Domain DTO → Persistence DTO, all with the same fields and no boundary-specific validation.
- positive: three booleans (`started`, `completed`, `published`) are persisted even though a single durable state/event already determines all three.
- positive: changing one validation rule requires edits in registry metadata, factory, adapter, facade, mapper, handler, and duplicate schema with no independent owners.
- near-miss: an external wire DTO is translated once into a strong domain type because the wire contract and domain model legitimately differ.
- counterexample: a distributed workflow has several explicit states because crash recovery genuinely distinguishes them.

## Nudge
If the machinery is more memorable than the invariant, the machinery has become the product.

Make the real problem dominate the representation again.
