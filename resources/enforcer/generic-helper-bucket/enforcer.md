# generic-helper-bucket — Enforcer

## Definition
A generic helper bucket is a module named by its lack of ownership—`utils`, `helpers`, `common`, `misc`, `core`—that accumulates operations whose only common property is that no domain owner was chosen.

## Governing Principle
A module boundary is useful when its name predicts what belongs inside and what does not. Generic buckets have no exclusion principle, so they grow monotonically. Every convenient orphan can enter, unrelated dependencies converge there, and the module becomes a hidden coupling hub precisely because its name makes no semantic claim that could reject new contents.

## Trigger When
Trigger when a generic utility module contains functions from unrelated domains, effects, or lifecycles and callers depend on it as a grab bag.

## Do Not Trigger When
Do not trigger for a genuinely cohesive low-level module whose name is broad but whose exported operations share one stable algebra or technical invariant.

## Distinguish From
god-module owns many responsibilities and effects. translator-layer-bloat adds forwarding layers. This rule is specifically missing conceptual ownership disguised by a generic container name.

## Decision Procedure
For each exported function ask “which concept would be incomplete without this?” Move it there. If no single sentence can define membership for the bucket, the bucket has no boundary.

## Nudge
A module needs an exclusion rule. Move each orphan operation to the concept or boundary that owns it; do not let `utils` become architecture’s lost-and-found.
