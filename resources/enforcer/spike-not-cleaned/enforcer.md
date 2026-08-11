# spike-not-cleaned — Enforcer

## Definition
Experimental or proof-of-concept code is promoted without replacing shortcuts, hard-coded assumptions, and missing contracts.

## Trigger When
Experimental code or a proof of concept is promoted without replacing shortcuts, hard-coded assumptions, and missing contracts.

## Do Not Trigger When
Do not fire when the spike remains clearly isolated behind a flag or sandbox and is not the production path.

## Distinguish From
leftover-scaffolding is temporary harness residue; dirty-hack is a local shortcut; this tip is promoting the spike as design.

## Decision Procedure
1. Name the concept
2. Name the boundary
3. Ask if a primitive crosses it
4. Prefer a distinct type

## Nudge
A spike is being shipped as production design. Rebuild it around explicit contracts and remove experimental shortcuts.

## Examples
### Positive
Experimental code or a proof of concept is promoted without replacing shortcuts, hard-coded assumptions, and missing contracts.

### Near miss
A related situation that shares vocabulary but does not cross this tip's boundary — see Distinguish From.

### Counterexample
Do not fire when the spike remains clearly isolated behind a flag or sandbox and is not the production path.
