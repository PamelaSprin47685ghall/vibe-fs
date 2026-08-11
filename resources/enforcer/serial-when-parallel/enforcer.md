# serial-when-parallel — Enforcer

## Definition
Independent tool calls, reads, validations, or investigations run sequentially without a dependency that requires that order.

## Trigger When
Independent tool calls, reads, validations, or investigations are performed sequentially without a dependency requiring the order.

## Do Not Trigger When
Do not fire when later work needs earlier outputs, shared mutable resources require a critical section, or ordering is part of an external protocol.

## Distinguish From
serial-investigation is the research-specific form; unbounded-fanout is the opposite extreme without bounds.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
Independent work is being serialized. Run it concurrently with a clear bound and deterministic result ordering.

## Examples
### Positive
Independent tool calls, reads, validations, or investigations are performed sequentially without a dependency requiring the order.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when later work needs earlier outputs, shared mutable resources require a critical section, or ordering is part of an external protocol.
