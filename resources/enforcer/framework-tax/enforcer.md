# framework-tax — Enforcer

## Definition
Framework tax is the accidental complexity paid when lifecycle, configuration, registration, indirection, and generated structure occupy more of the design than the domain operation they are meant to support. The root-cause is that framework lifecycle, configuration, and registration become the dominant ontology for a smaller domain operation, so readers pay ceremony before they can see the problem.

## Governing Principle
A framework is justified when it compresses recurring complexity the application truly has. When the problem is smaller than the framework’s ceremony, the abstraction expands rather than compresses: readers must learn container lifecycles, hook order, annotations, configuration dialects, and extension rules before they can see a simple operation. The tool has become the problem’s dominant ontology.

## Trigger When
Trigger when configuration, DI wiring, lifecycle hooks, generated layers, or framework conventions materially exceed the essential domain logic and are not buying corresponding capability.

## Do Not Trigger When
- The framework genuinely centralizes difficult cross-cutting behavior whose local reimplementation would create more complexity or risk.
- The ceremony is the acquisition of an unneeded library; that decision is `dependency-bloat` before the framework is the ontology.
- A small amount of registration exists to gain real lifecycle, security, or resource management the product needs.
- Generated code is the published contract of a standard (OpenAPI, protobuf) rather than a local ritual around a simple function.

## Distinguish From
`dependency-bloat` is the decision to import excessive machinery. `incidental-complexity-dominates` is the broad symptom. This rule focuses on framework ceremony specifically. Tie-break: if readers must learn container/hook/config ontology to see a small domain operation, this rule owns the case.

## Decision Procedure
Describe the domain operation without framework nouns. Then count the additional concepts required only to make the framework perform that operation. If those concepts dominate, expose the operation more directly.

## Examples
- positive: a one-function feature requires a module, provider, interceptor, config file, and generated stub before the function is reachable.
- near-miss: a DI container owns request-scoped resources and auth filters that would be riskier to reimplement ad hoc.
- counterexample: strip unused ceremony and expose the operation through the simplest native construct that fits.

## Nudge
An abstraction should make the problem smaller. If framework ritual is larger than the domain operation, remove the ritual until the problem is visible again.
