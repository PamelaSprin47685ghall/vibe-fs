# incidental-complexity-dominates — Enforcer

## Definition
Incidental complexity dominates when glue, configuration, wrappers, lifecycle management, serialization ceremony, or tooling occupies more reasoning than the domain problem itself.

## Governing Principle
Software cannot eliminate essential complexity; it can only choose where to pay for it. Good design compresses everything not inherent to the problem so reader attention remains available for the irreducible decisions. When accidental machinery becomes the visible architecture, the system has inverted its budget: humans spend most of their scarce reasoning capacity on artifacts introduced by the solution rather than facts imposed by the domain.

## Trigger When
Trigger when understanding or changing a simple domain rule requires traversing disproportionate adapters, configuration, framework lifecycle, wrappers, translation layers, or boilerplate.

## Do Not Trigger When
Do not trigger when the apparent complexity corresponds to real domain states, external protocols, security constraints, or scale requirements that cannot honestly be removed.

## Distinguish From
framework-tax is framework-specific ceremony. pattern-sprawl is OO/pattern scaffolding. This rule is the higher-level condition where accidental structure eclipses essential structure regardless of source.

## Decision Procedure
Separate concepts into “the problem requires this” and “our solution introduced this.” Rework the second set until its cost is subordinate to the first.

## Nudge
Spend complexity only on reality. Remove solution-imposed ceremony until the domain’s essential concepts again dominate the code’s visible structure.
