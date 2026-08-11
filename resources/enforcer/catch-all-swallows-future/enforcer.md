# catch-all-swallows-future — Enforcer

## Definition
A catch-all is dangerous when it converts an open-ended future domain into a closed present assumption without forcing that assumption to be revisited.

## Governing Principle
Exhaustiveness is a maintenance alarm. In a finite domain, a new case should create compile-time or test-time pressure at every place whose meaning may change. A wildcard silences that pressure by asserting, usually accidentally, that all unknown futures are semantically equivalent. The branch does not merely handle today’s remainder; it grants itself authority over tomorrow’s cases.

## Trigger When
Trigger when wildcard/default branches, broad catches, generic fallbacks, or “unknown → ignore” paths absorb domain variants that should require an explicit decision when added.

## Do Not Trigger When
Do not trigger when the domain is intentionally open and the fallback behavior is itself the documented contract for unknown extension values.

## Distinguish From
non-exhaustive-transition concerns legal state/event pairs. stringly-typed-error concerns prose-driven branching. This rule concerns a fallback that prevents future semantic changes from becoming visible.

## Decision Procedure
Ask what should happen if a new domain case is introduced tomorrow. If the correct answer requires human judgment, remove the catch-all and make that judgment mechanically unavoidable.

## Nudge
Do not let today’s default decide tomorrow’s meaning. Make closed domains exhaustive so a new case breaks loudly until its semantics are chosen.
