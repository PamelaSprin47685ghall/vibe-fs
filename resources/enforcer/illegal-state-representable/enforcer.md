# illegal-state-representable — Enforcer

## Definition
An illegal state is representable when the program’s data type admits combinations of fields or flags that the domain says can never legitimately exist.

## Governing Principle
A type is not merely storage; it is a theorem about possibility. Every constructible value is a state the rest of the program must be prepared to handle. If nullable fields and flags admit contradictory combinations, the type has enlarged reality and pushed the burden of disproving those invented worlds into every consumer. Correctness then depends on repeated runtime discipline instead of construction.

## Trigger When
Trigger when optional fields or boolean/state flags can be combined into values that have no valid domain interpretation, or when callers repeatedly assert “this field must be present when that flag is true.”

## Do Not Trigger When
- Do not trigger when every representable combination is meaningful by contract, even if some cases are uncommon.
- Do not trigger for transport DTOs that must round-trip unknown combinations until a validated constructor admits them into the domain.
- Do not trigger when construction is already closed and illegal combinations cannot leak past the constructor.

## Distinguish From
boolean-blindness focuses on booleans erasing named choices. null-ambiguity focuses on one absence value carrying multiple outcomes. This rule is the broader mismatch between representable and legitimate state spaces. Tie-break: if the lie is a product of fields that should be a sum of cases, use this rule; if a single boolean erased named alternatives, use boolean-blindness.

## Decision Procedure
Enumerate valid domain states first. Compare that set with the Cartesian product admitted by the current fields. If the type permits extra worlds, replace the product with a sum of valid cases.

## Examples
- positive: `isPaid` plus nullable `receiptId` can be true-with-null or false-with-receipt; callers re-assert the pairing.
- near-miss: A wire DTO carries optional fields; a smart constructor refuses illegal combinations before domain use.
- counterexample: A sum type encodes Paid and Unpaid with receipt data only on Paid.

## Nudge
Make the type tell the truth about possibility. Encode valid states directly and attach only the data meaningful in each state, so contradictions cannot be constructed.
