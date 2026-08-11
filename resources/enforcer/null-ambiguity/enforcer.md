# null-ambiguity — Enforcer

## Definition
Null ambiguity exists when `null`, missing, empty, or optional presence is used to represent several semantically different outcomes.

## Governing Principle
Absence is not a domain; it is a missing proposition. "Not found," "not authorized," "not loaded," "failed," and "not applicable" may all lack a value, but they differ in cause and required response. Collapsing them to one empty representation destroys information at the boundary and forces downstream code to reconstruct meaning from context it no longer has.

## Trigger When
Trigger when callers must infer why a value is absent from surrounding flags, error strings, status codes, timing, or comments because the return representation distinguishes only value versus no value.

## Do Not Trigger When
- There is genuinely one semantic notion of optionality and callers need no further distinction to behave correctly.
- Absence is already a named result case, not a bare null.
- The optional wraps a value whose missingness has one meaning at that boundary.

## Distinguish From
illegal-state-representable concerns invalid combinations. expected-failure-as-exception hides named failures in exceptions. Tie-break: if distinct outcomes are collapsed into one absence value, this rule; if a product of fields can be contradictory, illegal-state-representable; if named failures are thrown instead of returned, expected-failure-as-exception.

## Decision Procedure
List every reason the value may be absent and every caller action those reasons require. If actions differ, encode the reasons as distinct result cases.

## Examples
- positive: `getUser` returns `null` for both missing and forbidden, and callers inspect HTTP status afterward.
- near-miss: `findById` returns `Option` meaning only "present or not," and unauthorized is a separate error type.
- counterexample: A closed result `Found | NotFound | Forbidden` is matched at the call site.

## Nudge
Do not throw away the reason at the boundary and ask callers to guess it later. Return the actual alternatives the domain distinguishes.
