# unrecorded-decision — Main

## What To Do Now
Create or update the project’s durable decision record with the context, chosen path, credible alternatives, rationale, and consequences.

## Why This Matters
Code shows the equilibrium after a decision but not the forces that produced it. When those forces are forgotten, future refactors can resurrect previously rejected designs and pay the same analysis cost again.

## Repair Strategy
Keep the record proportional to consequence: state the problem, constraints, decision, alternatives considered, and what would justify revisiting it. Link to governing contracts rather than duplicating them.

## Decision Branches
If a future maintainer could reasonably revive a rejected alternative from code alone, write the decision record.
If the rationale already lives in an authoritative ADR or executable invariant, update that artifact instead of adding a parallel story.

## Common Wrong Fixes
- Write a retrospective that merely describes the final code.
- Paste chat logs without stating the decision, alternatives, and constraints.
- Duplicate the whole contract instead of linking it and recording only the tradeoff.

## Verification
Invariant: a future reader can tell which assumptions make the decision valid and what evidence would supersede it. The record must preserve rejected alternatives, not only the surviving shape.

## Done When
The architecture preserves not only what was chosen but enough reasoning to prevent history from becoming an unexplained shape future engineers must rediscover.
