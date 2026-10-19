# incidental-complexity-dominates — Lesson

## Opinion

### Definition

Incidental complexity dominates when understanding or changing a simple domain fact requires reasoning through more machinery than the fact itself deserves.

The smell is not “there are many files” or “the code is long.” The smell is **semantic displacement**: wrappers, adapters, flags, configuration, lifecycle glue, serializers, registries, compatibility paths, orchestration, and framework ceremony become the real thing engineers must understand, while the domain rule they supposedly support is a footnote.

### Governing Principle

Essential complexity is the part reality refuses to let you delete. Accidental complexity is the part your chosen representation invented.

A payment system may genuinely need idempotence, durable state, authorization, and partial-failure handling. It does not therefore need six DTO translations, three lifecycle flags reconstructing the same state, two parallel configuration surfaces, or a registry whose only purpose is locating code the type system already names.

The disease begins when the implementation's invented ontology becomes richer than the problem's real ontology.

### Trigger When

Trigger when a maintainer must spend most of the reasoning budget on solution-imposed machinery rather than domain distinctions. Common signs:

- one domain action crosses several wrappers that add no authority, persistence, isolation, or meaningful contract;
- the same fact is translated repeatedly between near-identical shapes inside one trust boundary;
- lifecycle flags/status fields reconstruct facts already available from durable truth;
- configuration exists mainly to parameterize internal indirection nobody independently needs;
- a tiny behavior change requires coordinated edits across many plumbing layers with no corresponding change in semantics;
- control flow is discoverable only by following registrations, callbacks, factories, generated bindings, and side tables;
- compatibility, migration, or “temporary” paths outlive every named external consumer;
- tests spend more setup on framework wiring than on the behavior they are meant to distinguish.

### Do Not Trigger When

- The complexity protects a real boundary: process isolation, authority, persistence, external protocol, independent deployment, failure containment, or another consequence that would be lost by collapse.
- The domain itself is genuinely complex; many states or files may be the simplest honest representation.
- Repetition is deliberate at a boundary because two sides own different models and translation is semantic work.
- Infrastructure ceremony is forced by an external platform and localized behind a narrow adapter rather than spread through the core.
- The code is verbose but conceptually direct. Verbosity alone is not incidental complexity.

### Distinguish From

`framework-tax` is a common species where framework lifecycle/configuration becomes the dominant ontology. `translator-layer-bloat` concerns repeated shape conversion. `facade-hides-mess` hides complexity behind a clean front without reducing it. `god-module` centralizes unrelated sovereignty.

Use this rule when the central observation is broader: the solution has made its own machinery harder to understand than the problem.

### Decision Procedure

State the domain operation in plain technical language. Then draw the minimum facts that must exist for it to be correct: owners, states, effects, durable facts, failure boundaries.

Now walk the implementation and ask of each layer/state/translation:

> What real distinction disappears if this mechanism is removed?

If the answer is “none; another layer already knows the same thing,” “it exists because the framework pattern expects it,” or “we might need it later,” the complexity is accidental.

### Examples

- positive: updating one user preference requires Controller DTO → Service DTO → Command DTO → Domain DTO → Persistence DTO, all with the same fields and no boundary-specific validation.
- positive: three booleans (`started`, `completed`, `published`) are persisted even though a single durable state/event already determines all three.
- positive: changing one validation rule requires edits in registry metadata, factory, adapter, facade, mapper, handler, and duplicate schema with no independent owners.
- near-miss: an external wire DTO is translated once into a strong domain type because the wire contract and domain model legitimately differ.
- counterexample: a distributed workflow has several explicit states because crash recovery genuinely distinguishes them.

## Advice

### What To Do Now

Make the domain operation visible again.

Identify the few distinctions the system genuinely must preserve — ownership, state, authority, persistence, failure, external contracts — and collapse machinery that does not protect one of them. Do not solve accidental complexity by wrapping it in one more abstraction.

### Why This Matters

Incidental complexity charges interest on every future change.

A redundant layer is not paid for once. It must be learned by each maintainer, mocked by tests, migrated during refactors, updated when schemas change, debugged during incidents, and preserved because nobody is certain whether it is secretly important.

Eventually the organization loses the ability to distinguish “required by the problem” from “required by our previous design.” At that point architecture becomes self-justifying: the system is complex because the architecture is complex, and the architecture must remain complex because the system has adapted around it.

### Repair Strategy

Start with a semantic inventory, not a file move:

1. name the domain action and its externally visible promise;
2. name the real owners and durable facts;
3. name effects/failure boundaries that genuinely need isolation;
4. mark every translation, wrapper, flag, registry, adapter, facade, and lifecycle object on the path;
5. for each, state the unique invariant or boundary it protects;
6. merge/delete mechanisms whose answer is duplicated, historical, or purely ceremonial;
7. update tests to protect the surviving semantic boundaries rather than old plumbing.

Prefer one representation inside one ownership boundary. Translate once at real ingress/egress. Derive state from durable truth when deterministic. Replace registration mazes with direct language references when dynamic discovery is not a real requirement.

Do not optimize for fewer files. Optimize for fewer independent concepts a maintainer must hold in mind to answer “what happens and why?”

### Decision Branches

- **Two layers own the same decision:** choose one semantic owner and make the other a mechanical adapter or delete it.
- **Two near-identical representations live inside one boundary:** collapse them unless each carries a distinct invariant.
- **Stored state can be derived from a more authoritative durable fact:** remove the duplicate writer and derive it.
- **Framework boilerplate is unavoidable:** confine it behind a narrow edge so domain code does not speak framework ontology.
- **A layer protects a real external contract or failure boundary:** keep it; document/name the distinction it owns.
- **The complexity is truly domain complexity:** improve names/types/tests, but do not flatten real states merely to reduce ceremony.

### Common Wrong Fixes

- Add a facade over the same layers and call the architecture simpler.
- Move files into smaller modules while preserving the same distributed ownership and call graph.
- Generate boilerplate automatically. Generated accidental complexity still has semantic/debugging cost.
- Replace explicit direct code with a generic framework to “standardize” the ceremony.
- Introduce one more canonical DTO and require all existing DTOs to translate through it.
- Delete real domain distinctions because “we want fewer types.” Simplicity that loses truth is corruption, not design.
- Apply arbitrary line/file count limits as the reason for splitting. Size can trigger a question; it cannot answer ownership.

### Verification

Take a representative change and compare before/after reasoning paths.

The repaired design should require fewer independent concepts and fewer synchronized edits **without losing a real invariant**. Tests should still falsify the same public/domain promises. Recovery, authority, and external boundaries must remain explicit where they matter.

A useful check is to ask a maintainer to explain the operation without using framework/plumbing nouns. If the explanation can now be mapped directly onto code owners and state, the repair is moving in the right direction.

Invariant:

> Essential distinctions remain explicit; solution-invented distinctions do not dominate the mental model.

### Done When

The implementation is again mostly about the problem it solves.

You can remove a layer and explain exactly what semantic burden disappeared — not merely where the code moved.

### Nudge

If the machinery is more memorable than the invariant, the machinery has become the product.

Make the real problem dominate the representation again.
