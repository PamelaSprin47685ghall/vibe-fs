# todo-bomb — Enforcer

## Definition
A TODO becomes a correctness bomb when required behavior is replaced by a placeholder, unimplemented branch, panic, or note whose future completion is necessary for the shipped path to be sound.

## Governing Principle
A TODO is not deferred work unless the current system remains complete without it. When correctness depends on the future task, the comment converts a known defect into an undocumented time bomb: execution may reach a state the implementation already knows it cannot honor. A system should either implement its contract or explicitly refuse the unsupported case at the boundary.

## Trigger When
Trigger when TODO/FIXME placeholders, temporary exceptions, `not implemented`, dummy returns, or panic branches stand in for behavior required by a reachable production contract.

## Do Not Trigger When
Do not trigger for genuine backlog notes about optional future improvement that do not weaken correctness of any currently supported path.

## Distinguish From
half-finished-refactor leaves migration incomplete. spike-not-cleaned ships prototype shortcuts. This rule specifically marks known required correctness postponed inside delivered code.

## Decision Procedure
Ask whether a valid supported input can reach the placeholder today. If yes, either implement the behavior or narrow the supported contract so the case is explicitly and intentionally rejected.

## Nudge
A comment cannot fulfill a contract. Finish required behavior before delivery, or make the unsupported case an explicit boundary rather than a hidden promise to the future.
