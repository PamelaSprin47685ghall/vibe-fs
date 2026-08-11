# compatibility-cruft — Enforcer

## Definition
Compatibility cruft is a second representation, alias, adapter, or execution path preserved without a concrete external contract that still requires it.

## Governing Principle
Compatibility is not free kindness; it is a promise to support two histories at once. Every retained surface multiplies states the system must understand, test, document, migrate, and eventually remove. Without an identified consumer and retirement condition, “just in case” compatibility converts uncertainty into permanent architecture.

## Trigger When
Trigger when aliases, old formats, dual writes, fallback parsers, adapters, or parallel code paths are added or retained solely to reduce fear of breaking an unspecified caller.

## Do Not Trigger When
Do not trigger when a named external consumer, version contract, rollout plan, or data migration genuinely requires overlap for a bounded period.

## Distinguish From
legacy-cruft-retained violates an explicit clean-break decision. half-finished-refactor leaves migration incomplete. This rule is compatibility machinery whose obligation was never established.

## Decision Procedure
Name the consumer, the exact old contract it still exercises, and the condition that ends support. If any of those cannot be named, treat the duplicate surface as unjustified.

## Nudge
Compatibility is a contract, not a superstition. Keep a second path only for a named consumer and a bounded migration; otherwise preserve one canonical interface.
