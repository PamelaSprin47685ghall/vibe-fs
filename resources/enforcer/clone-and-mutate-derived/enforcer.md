# clone-and-mutate-derived — Enforcer

## Definition
Clone-and-mutate derives a domain value by copying a mutable prototype and patching selected fields, so the new value’s meaning is defined by what happened not to be changed. The root-cause is that derivation is defined by omitted patches on a mutable prototype, so future source fields silently inherit into a value nobody constructed.

## Governing Principle
A value should state its own truth. Prototype cloning instead defines truth negatively: every inherited field is accepted by omission. As objects evolve, newly added fields silently propagate into old derivation code, turning structural reuse into semantic inheritance. The derived value then depends on the prototype’s entire future shape, not only on the facts its constructor intended to preserve.

## Trigger When
Trigger when domain values are produced by cloning/copying a mutable object and then assigning differences field by field.

## Do Not Trigger When
- Immutable record-copy syntax preserves fields that are intentionally part of the same domain value, and invariants remain constructor-safe.
- A shallow copy is an implementation detail inside a constructor that then freezes or seals the result before escape.
- Updating a truly mutable entity in place is not derivation of a new semantic value (see `in-place-mutation`).
- Test fixtures that build independent literals per case are not prototype inheritance.

## Distinguish From
`in-place-mutation` changes an existing shared value. `runtime-checked-builder` permits invalid construction phases. This rule concerns deriving a new semantic value by inheriting an overly broad prototype. Tie-break: if the new value’s contents are “whatever the prototype had, minus these patches,” this rule owns the case.

## Decision Procedure
List the facts the new value should carry. If that list is smaller or more meaningful than “everything the old object currently has except these patches,” construct from the list directly.

## Examples
- positive: `const next = clone(order); next.status = 'paid'` so future fields on `order` silently appear on `next`.
- near-miss: an immutable record update that names the preserved value and constructor-checks invariants.
- counterexample: construct the derived value from the explicit facts the domain relation preserves, requiring a decision on new fields.

## Nudge
Derivation should be positive, not accidental inheritance. Construct the intended immutable value from the facts that define it.
