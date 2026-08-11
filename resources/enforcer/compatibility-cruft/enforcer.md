# compatibility-cruft — Enforcer

## Definition
Compatibility cruft is a second representation, alias, adapter, or execution path preserved without a concrete external contract that still requires it. The root-cause is that a second path is retained without a named consumer and end condition, so fear of breakage becomes permanent dual architecture.

## Governing Principle
Compatibility is not free kindness; it is a promise to support two histories at once. Every retained surface multiplies states the system must understand, test, document, migrate, and eventually remove. Without an identified consumer and retirement condition, “just in case” compatibility converts uncertainty into permanent architecture.

## Trigger When
Trigger when aliases, old formats, dual writes, fallback parsers, adapters, or parallel code paths are added or retained solely to reduce fear of breaking an unspecified caller.

## Do Not Trigger When
- A named external consumer, version contract, rollout plan, or data migration genuinely requires overlap for a bounded period.
- The second path is the current canonical interface, not a leftover alias.
- A translator that exists only at an external version boundary with a written expiry is justified compatibility.
- Internal renaming already completed with no remaining producers of the old form is not cruft to keep.

## Distinguish From
`legacy-cruft-retained` violates an explicit clean-break decision. `half-finished-refactor` leaves migration incomplete. This rule is compatibility machinery whose obligation was never established. Tie-break: if no named consumer and no end condition exist, this rule owns the duplicate surface.

## Decision Procedure
Name the consumer, the exact old contract it still exercises, and the condition that ends support. If any of those cannot be named, treat the duplicate surface as unjustified.

## Examples
- positive: keep both JSON shapes and a dual-write “in case someone still sends v1,” with no named consumer or removal date.
- near-miss: a documented v1 consumer with a rollout window and a removal ticket.
- counterexample: one canonical interface; keep a bridge only for a named external obligation with an expiry.

## Nudge
Compatibility is a contract, not a superstition. Keep a second path only for a named consumer and a bounded migration; otherwise preserve one canonical interface.
