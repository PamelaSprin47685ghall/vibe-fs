# incidental-complexity-dominates — Enforcer

## Definition
Incidental complexity dominates when glue, configuration, wrappers, lifecycle management, serialization ceremony, or tooling occupies more reasoning than the domain problem itself.

## Governing Principle
Software cannot eliminate essential complexity; it can only choose where to pay for it. Good design compresses everything not inherent to the problem so reader attention remains available for the irreducible decisions. When accidental machinery becomes the visible architecture, the system has inverted its budget: humans spend most of their scarce reasoning capacity on artifacts introduced by the solution rather than facts imposed by the domain.

## Trigger When
Trigger when understanding or changing a simple domain rule requires traversing disproportionate adapters, configuration, framework lifecycle, wrappers, translation layers, or boilerplate.

## Do Not Trigger When
- Do not trigger when the apparent complexity corresponds to real domain states, external protocols, security constraints, or scale requirements that cannot honestly be removed.
- Do not trigger for a thin adapter required by an external protocol you do not control.
- Do not trigger while a spike is still measuring whether the machinery is essential; trigger when it ships as the visible architecture.

## Distinguish From
framework-tax is framework-specific ceremony. pattern-sprawl is OO/pattern scaffolding. This rule is the higher-level condition where accidental structure eclipses essential structure regardless of source. Tie-break: if the mass is a specific framework’s ceremony, use framework-tax; if accidental structure from any source dominates the domain, use this rule.

## Decision Procedure
Separate concepts into “the problem requires this” and “our solution introduced this.” The root-cause is solution-imposed mass that eclipses essential structure; rework the second set until its cost is subordinate to the first. Prefer this over a single wrapper or framework tax when accidental structure from any source is what dominates.

## Examples
- positive: Changing one pricing rule requires navigating DI, mapper, DTO, wrapper, and lifecycle config before the rule appears.
- near-miss: A thin client for a mandated external protocol sits beside a small domain model that still dominates the code.
- counterexample: The domain types and transitions are the main visible structure; infrastructure appears only at the boundary.

## Nudge
Spend complexity only on reality. Remove solution-imposed ceremony until the domain’s essential concepts again dominate the code’s visible structure.
