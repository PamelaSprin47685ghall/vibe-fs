# ignored-tdd — Enforcer

## Definition
TDD is ignored when production behavior is changed before a failing behavioral test establishes what must become true.

## Governing Principle
Red-first is not ceremony; it separates specification from implementation influence. A test written after the code is already green is vulnerable to describing what was built rather than what was required. Seeing the test fail for the right reason proves two things at once: the requirement was absent before the change, and the test is capable of detecting that absence.

## Trigger When
Trigger when new or changed production behavior is implemented first and the test is added afterward without ever demonstrating the old behavior fails the requirement.

## Do Not Trigger When
Do not trigger for pure refactors with already sufficient behavioral coverage and intentionally unchanged semantics.

## Distinguish From
missing-regression-test concerns defect fixes specifically. coverage-theater concerns weak assertions. This rule concerns the temporal order that establishes independence between requirement and implementation.

## Decision Procedure
Before production edits, write the smallest behavioral test that expresses the required observable difference. Run it and inspect the failure reason. Only then change implementation.

## Nudge
Make the requirement capable of accusing the old code before teaching the code how to satisfy it. Red proves the test is independent evidence; green then proves the change.
