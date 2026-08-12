# framework-tax — Enforcer

## Definition
Framework tax is the accidental complexity paid when a framework's lifecycle, registration model, configuration vocabulary, extension points, generated artifacts, or indirection becomes more prominent than the domain operation it exists to support.

The framework has stopped being infrastructure and started demanding that the problem be restated in its ontology.

## Governing Principle
A framework is justified by complexity it removes, not by architecture it visibly introduces.

Useful frameworks absorb hard cross-cutting work: transport, scheduling, rendering, persistence drivers, protocol compliance, dependency construction, platform integration. The tax becomes pathological when simple operations require ceremonies whose only consumer is the framework itself.

“Standard pattern” is not free. Every container registration, decorator, provider, middleware layer, hook adapter, generated binding, config key, abstract base, and lifecycle callback becomes another place where behavior can hide.

The question is not whether the framework is popular or idiomatic. The question is whether its machinery is buying a real capability at this boundary.

## Trigger When
Trigger when framework mechanics dominate understanding/change cost without protecting an independent contract. Common forms:

- a direct function call becomes interface → implementation → provider → container registration → resolver for no real runtime substitution need;
- business control flow is distributed across annotations, middleware, hooks, interceptors, decorators, and config rather than visible in one semantic owner;
- adding one domain field requires updating multiple framework metadata/schema/registration representations of the same fact;
- generated scaffolding is treated as architecture even though it only mirrors source declarations;
- a tiny component cannot be tested without booting a large application/container because domain decisions are fused to framework lifecycle;
- internal modules adopt transport DTOs, ORM entities, request contexts, framework exceptions, or plugin shapes as their domain vocabulary;
- a generic framework abstraction is introduced before there are genuinely distinct implementations/consumers;
- migration away from a framework would require rewriting core domain logic rather than replacing boundary adapters.

## Do Not Trigger When
- The framework machinery directly enforces a real external protocol, lifecycle, transaction, security, or isolation boundary.
- Dynamic discovery/substitution is an actual runtime requirement with multiple independent implementations or deployment contexts.
- Boilerplate is localized at an adapter edge while core decisions remain framework-agnostic.
- A framework feature removes substantial bespoke machinery and its semantics are simpler than the alternative.
- The project deliberately adopts a framework convention as part of its public integration contract; the convention itself may then be real boundary knowledge.

## Distinguish From
`incidental-complexity-dominates` is broader. `framework-tax` specifically identifies the framework's ontology as the source of the accidental burden.

`pattern-sprawl` concerns hand-built or inherited design-pattern machinery that the language can express more directly. `dependency-bloat` concerns unnecessary packages even when their local integration is simple. `facade-hides-mess` puts a clean front over tangled internals.

## Decision Procedure
Describe the desired operation without framework nouns.

Then list each framework construct on the path and ask:

> What capability would be lost if this were replaced by the host language's direct construct or a narrow adapter?

Valid answers include transaction scope, host hook contract, runtime plugin discovery, request isolation, protocol decoding. Invalid answers include “that's how the framework wants it,” “it may be useful later,” and “it makes the architecture look consistent.”

If most constructs exist to satisfy each other rather than the problem, the tax dominates.

## Examples
- positive: one repository implementation is hidden behind an interface, provider class, token string, container module, factory, and resolver solely because “everything should use DI.”
- positive: a domain validation rule is scattered through request decorator metadata, middleware, ORM hooks, and serializer config, with no single semantic owner.
- positive: core domain functions accept a web framework request object because threading a small explicit context would require fewer annotations.
- near-miss: a plugin host requires a specific hook object; one adapter translates that hook into domain commands while the rest of the system ignores host types.
- counterexample: a transaction framework owns real commit/rollback semantics across multiple persistence operations and removes bespoke failure machinery.

## Nudge
A framework should make the problem smaller.

If engineers must first learn the framework's mythology before they can see a simple domain action, you are paying interest on the tool.
