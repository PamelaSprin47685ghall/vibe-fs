# primitive-obsession — Enforcer

## Definition
Primitive obsession exists when distinct domain concepts cross a meaningful boundary as the same undifferentiated string, number, or boolean, allowing substitutions the domain itself forbids.

## Governing Principle
A primitive preserves representation while erasing identity. `string` can carry an account ID, order ID, path, digest, capability, or currency code, so the type system sees all substitutions as legitimate even when the domain sees category errors. A named type restores the missing proposition: this value is not merely text; it belongs to this concept and may cross only where that concept is accepted.

## Trigger When
Trigger when identifiers, money, paths, digests, capabilities, units, or other domain values cross module/API boundaries as generic primitives and unrelated values of the same primitive type can be accidentally interchanged.

## Do Not Trigger When
Do not trigger for truly generic textual/numeric data whose domain identity is irrelevant at that boundary, or when a validated named domain type already encloses the primitive.

## Distinguish From
null-ambiguity erases outcome identity in absence. illegal-state-representable admits invalid combinations. misleading-name lies in vocabulary. This rule concerns erased nominal identity between values with the same physical representation.

## Decision Procedure
Name the concept and boundary, then ask whether a value of the same primitive but a different domain meaning could pass type checking there. If yes, introduce a distinct type.

## Nudge
Representation is not identity. Give the domain concept a type so the compiler can reject substitutions that reality rejects.
