# clone-and-mutate-derived — Enforcer

## Definition
Clone-and-mutate derives a domain value by copying a mutable prototype and patching selected fields, so the new value’s meaning is defined by what happened not to be changed.

## Governing Principle
A value should state its own truth. Prototype cloning instead defines truth negatively: every inherited field is accepted by omission. As objects evolve, newly added fields silently propagate into old derivation code, turning structural reuse into semantic inheritance. The derived value then depends on the prototype’s entire future shape, not only on the facts its constructor intended to preserve.

## Trigger When
Trigger when domain values are produced by cloning/copying a mutable object and then assigning differences field by field.

## Do Not Trigger When
Do not trigger for immutable record-copy syntax where preserved fields are intentionally part of the same domain value and invariants remain constructor-safe.

## Distinguish From
in-place-mutation changes an existing shared value. runtime-checked-builder permits invalid construction phases. This rule concerns deriving a new semantic value by inheriting an overly broad prototype.

## Decision Procedure
List the facts the new value should carry. If that list is smaller or more meaningful than “everything the old object currently has except these patches,” construct from the list directly.

## Nudge
Derivation should be positive, not accidental inheritance. Construct the intended immutable value from the facts that define it.
