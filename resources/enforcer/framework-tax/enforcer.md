# framework-tax — Enforcer

## Definition
Framework tax is the accidental complexity paid when lifecycle, configuration, registration, indirection, and generated structure occupy more of the design than the domain operation they are meant to support.

## Governing Principle
A framework is justified when it compresses recurring complexity the application truly has. When the problem is smaller than the framework’s ceremony, the abstraction expands rather than compresses: readers must learn container lifecycles, hook order, annotations, configuration dialects, and extension rules before they can see a simple operation. The tool has become the problem’s dominant ontology.

## Trigger When
Trigger when configuration, DI wiring, lifecycle hooks, generated layers, or framework conventions materially exceed the essential domain logic and are not buying corresponding capability.

## Do Not Trigger When
Do not trigger when the framework genuinely centralizes difficult cross-cutting behavior whose local reimplementation would create more complexity or risk.

## Distinguish From
dependency-bloat is the decision to import excessive machinery. incidental-complexity-dominates is the broad symptom. This rule focuses on framework ceremony specifically.

## Decision Procedure
Describe the domain operation without framework nouns. Then count the additional concepts required only to make the framework perform that operation. If those concepts dominate, expose the operation more directly.

## Nudge
An abstraction should make the problem smaller. If framework ritual is larger than the domain operation, remove the ritual until the problem is visible again.
