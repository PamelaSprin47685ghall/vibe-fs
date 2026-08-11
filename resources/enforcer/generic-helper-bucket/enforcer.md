# generic-helper-bucket — Enforcer

## Definition
A generic helper bucket is a module named by its lack of ownership—`utils`, `helpers`, `common`, `misc`, `core`—that accumulates operations whose only common property is that no domain owner was chosen. The root-cause is that a nameless module has no exclusion rule, so unrelated orphans and their dependencies accumulate into a coupling hub.

## Governing Principle
A module boundary is useful when its name predicts what belongs inside and what does not. Generic buckets have no exclusion principle, so they grow monotonically. Every convenient orphan can enter, unrelated dependencies converge there, and the module becomes a hidden coupling hub precisely because its name makes no semantic claim that could reject new contents.

## Trigger When
Trigger when a generic utility module contains functions from unrelated domains, effects, or lifecycles and callers depend on it as a grab bag.

## Do Not Trigger When
- The module is a genuinely cohesive low-level unit whose name is broad but whose exported operations share one stable algebra or technical invariant.
- A narrowly named technical module (`utf8`, `duration`, `hash`) holds primitives that belong together.
- Domain operations already live with their owner and a tiny private helper is not a public grab bag.
- `core` names a real kernel with an exclusion rule, not a dumping ground.

## Distinguish From
`god-module` owns many responsibilities and effects. `translator-layer-bloat` adds forwarding layers. This rule is specifically missing conceptual ownership disguised by a generic container name. Tie-break: if the module’s name cannot reject unrelated functions, this rule owns the case even when each function is small.

## Decision Procedure
For each exported function ask “which concept would be incomplete without this?” Move it there. If no single sentence can define membership for the bucket, the bucket has no boundary.

## Examples
- positive: `utils` exports date formatting, HTTP retries, and invoice totals; callers import the bag for any of them.
- near-miss: `duration` exports only duration arithmetic with one algebra and a clear exclusion rule.
- counterexample: move each operation to the domain, boundary, or technical concept that owns it.

## Nudge
A module needs an exclusion rule. Move each orphan operation to the concept or boundary that owns it; do not let `utils` become architecture’s lost-and-found.
