# unrecorded-decision — Lesson

## Opinion

### Definition

A decision is unrecorded when a material architecture, compatibility, operational, or tradeoff choice changes what future engineers are allowed to assume but its rationale and rejected alternatives exist only in the moment of discussion.

### Governing Principle

Code preserves what was chosen; it rarely preserves why alternatives were rejected. The root-cause is that rejected alternatives live only in conversation. Without rationale, future maintainers encounter only the surviving shape and may rationally “simplify” it back toward a discarded design because the constraints that ruled that design out are invisible. A decision record carries the counterfactual knowledge implementation cannot express by itself.

### Trigger When

Trigger when a consequential design choice, compatibility boundary, operational compromise, or rejected alternative will influence future changes and no durable record states the reason.

### Do Not Trigger When

- Trivial local choices whose alternatives have no lasting consequence.
- An existing authoritative decision record already captures the same rationale.
- Purely mechanical refactors that do not change what future engineers may assume.
- Encoding the choice as an executable invariant or schema that already states the rejected alternative’s failure.

### Distinguish From

`unrecorded-lesson` captures reusable discovery after experience. `missing-invariant-documentation` captures a correctness rule. Tie-break: if the missing artifact is why one durable path was chosen over credible alternatives, use this rule; if a reusable operational/debug fact was learned and not stored, use `unrecorded-lesson`.

### Decision Procedure

Ask whether a future competent maintainer could reasonably choose the rejected alternative from code alone. If yes, record context, decision, alternatives, rationale, and consequences at the project’s decision authority.

### Examples

- positive: the team rejects multi-region writes for consistency reasons, ships single-region, and leaves no ADR.
- near-miss: an ADR already records the constraint, and the code change merely implements it.
- counterexample: an incident reveals a provider retry quirk that is never written into a runbook — that is `unrecorded-lesson`.

## Advice

### What To Do Now

Create or update the project’s durable decision record with the context, chosen path, credible alternatives, rationale, and consequences. Write it where the architecture is decided: who owns the choice owns the durable rationale.

### Why This Matters

Code shows the equilibrium after a decision but not the forces that produced it. When those forces are forgotten, future refactors can resurrect previously rejected designs and pay the same analysis cost again.

### Repair Strategy

Keep the record proportional to consequence: state the problem, constraints, decision, alternatives considered, and what would justify revisiting it. Link to governing contracts rather than duplicating them.

### Decision Branches

If a future maintainer could reasonably revive a rejected alternative from code alone, write the decision record.
If the rationale already lives in an authoritative ADR or executable invariant, update that artifact instead of adding a parallel story.

### Common Wrong Fixes

- Write a retrospective that merely describes the final code.
- Paste chat logs without stating the decision, alternatives, and constraints.
- Duplicate the whole contract instead of linking it and recording only the tradeoff.

### Verification

Invariant: a future reader can tell which assumptions make the decision valid and what evidence would supersede it. The record must preserve rejected alternatives, not only the surviving shape.

### Done When

The architecture preserves not only what was chosen but enough reasoning to prevent history from becoming an unexplained shape future engineers must rediscover.

### Nudge

Implementation remembers the winner but forgets the argument. Record consequential choices so future engineers inherit the constraints, not merely the shape those constraints produced.
