# unrecorded-decision — Enforcer

## Definition
A decision is unrecorded when a material architecture, compatibility, operational, or tradeoff choice changes what future engineers are allowed to assume but its rationale and rejected alternatives exist only in the moment of discussion.

## Governing Principle
Code preserves what was chosen; it rarely preserves why alternatives were rejected. Without rationale, future maintainers encounter only the surviving shape and may rationally “simplify” it back toward a discarded design because the constraints that ruled that design out are invisible. A decision record carries the counterfactual knowledge implementation cannot express by itself.

## Trigger When
Trigger when a consequential design choice, compatibility boundary, operational compromise, or rejected alternative will influence future changes and no durable record states the reason.

## Do Not Trigger When
Do not trigger for trivial local choices whose alternatives have no lasting consequence, or when an existing authoritative decision record already captures the same rationale.

## Distinguish From
unrecorded-lesson captures reusable discovery after experience. missing-invariant-documentation captures a correctness rule. This rule preserves why one durable path was chosen over credible alternatives.

## Decision Procedure
Ask whether a future competent maintainer could reasonably choose the rejected alternative from code alone. If yes, record context, decision, alternatives, rationale, and consequences at the project’s decision authority.

## Nudge
Implementation remembers the winner but forgets the argument. Record consequential choices so future engineers inherit the constraints, not merely the shape those constraints produced.
