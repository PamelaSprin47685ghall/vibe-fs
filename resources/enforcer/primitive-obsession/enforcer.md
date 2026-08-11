# primitive-obsession — Enforcer

## Definition
Primitive obsession exists when distinct domain concepts cross a meaningful boundary as the same undifferentiated string, number, or boolean, allowing substitutions the domain itself forbids. The root-cause is that a primitive representation is asked to carry distinct domain identities across a boundary, so substitutions the domain forbids still type-check.

## Governing Principle
A primitive preserves representation while erasing identity. `string` can carry an account ID, order ID, path, digest, capability, or currency code, so the type system sees all substitutions as legitimate even when the domain sees category errors. A named type restores the missing proposition: this value is not merely text; it belongs to this concept and may cross only where that concept is accepted.

## Trigger When
Trigger when identifiers, money, paths, digests, capabilities, units, or other domain values cross module/API boundaries as generic primitives and unrelated values of the same primitive type can be accidentally interchanged.

## Do Not Trigger When
- The data is truly generic textual/numeric content whose domain identity is irrelevant at that boundary.
- A validated named domain type already encloses the primitive.
- The value never leaves a local helper and is not a boundary concept.

## Distinguish From
null-ambiguity erases outcome identity in absence. illegal-state-representable admits invalid combinations. misleading-name lies in vocabulary. Tie-break: if nominal identity between values that share a representation is erased, this rule; if absence collapses outcomes, null-ambiguity; if combinations are illegal, illegal-state-representable; if the name merely lies, misleading-name.

## Decision Procedure
Name the concept and boundary, then ask whether a value of the same primitive but a different domain meaning could pass type checking there. If yes, introduce a distinct type.

## Examples
- positive: `transfer(from, to)` takes two strings, so an order id can be passed as an account id.
- near-miss: A log line formatter accepts any string because the boundary has no domain identity.
- counterexample: `AccountId` and `OrderId` are distinct types; mixing them fails at compile/construction time.

## Nudge
Representation is not identity. Give the domain concept a type so the compiler can reject substitutions that reality rejects.
