# unrecorded-decision — Main

## What To Do Now
Create or update the project’s durable decision record with the context, chosen path, credible alternatives, rationale, and consequences.

## Why This Matters
Code shows the equilibrium after a decision but not the forces that produced it. When those forces are forgotten, future refactors can resurrect previously rejected designs and pay the same analysis cost again.

## Repair Strategy
Keep the record proportional to consequence: state the problem, constraints, decision, alternatives considered, and what would justify revisiting it. Link to governing contracts rather than duplicating them.

## Wrong Fixes
Do not write a retrospective that merely describes the final code. The valuable information is why this path defeated plausible alternatives under the constraints that mattered.

## Verification
A future reader should be able to tell which assumptions make the decision valid and what evidence would be required to supersede it.

## Done When
The architecture preserves not only what was chosen but enough reasoning to prevent history from becoming an unexplained shape future engineers must rediscover.
