# facade-hides-mess — Main

## What To Do Now
Repair the structure the facade is concealing.

Choose the real owners of state and policy, remove duplicated writers and dependency cycles, collapse obsolete representations, and make internal dependency direction honest. Keep or reintroduce the facade only after it corresponds to a coherent subsystem contract or a genuinely useful capability boundary.

## Why This Matters
Cosmetic facades are dangerous because they improve local ergonomics enough to stop the refactor early.

New callers see one clean object and conclude the subsystem is fixed. Meanwhile every implementation change still traverses the same coupled graph. The organization loses pressure to finish the ownership repair because the public ugliness — the part everyone complained about — is gone.

This creates a two-story architecture: clean documentation upstairs, haunted machinery downstairs. Incidents and large changes always happen downstairs.

## Repair Strategy
Work from inside out:

1. select a representative facade operation;
2. map all decisions, state writers, effects, translations, and dependencies it touches;
3. assign one semantic owner per decision/fact;
4. remove or demote duplicate owners;
5. eliminate cycles by changing ownership/dependency direction, not by routing the cycle through the facade;
6. collapse compatibility/translation paths no longer backed by external contracts;
7. make the surviving internal boundary explicit;
8. then design the facade to expose that boundary cleanly.

A good facade often becomes thinner after the underlying repair because it no longer needs flags, migration routing, state reconciliation, or hidden orchestration.

## Decision Branches
- **Facade forwards one coherent subsystem:** keep it if the caller ergonomics/stability are useful.
- **Facade dispatches between old/new owners:** finish migration; see `half-finished-refactor`.
- **Facade translates an external protocol:** keep translation at the edge; do not spread external shapes inward.
- **Facade owns authorization/capability narrowing:** that may be a genuine semantic boundary; preserve the restriction explicitly.
- **Facade contains many unrelated policies because internals are chaotic:** move each policy to its rightful owner before shrinking the facade.
- **Only external API cleanup was required:** do not overclaim internal repair. A caller-surface task can be complete without pretending architecture changed underneath.

## Common Wrong Fixes
- Add a second facade over the first.
- Rename internal modules/services while leaving dependency direction and writers unchanged.
- Hide internals with package-private/export restrictions and call the coupling solved.
- Write integration tests only against the facade so nobody notices duplicate paths remain live.
- Move all orchestration into the facade, turning cosmetic cleanup into a new god module.
- Keep old internals forever “because callers no longer see them.” Hidden debt still executes.
- Produce architecture diagrams showing only the facade and omit the graph behind it.

## Verification
Delete the facade mentally (or in a branch) and inspect what boundary remains.

The repair is structural when:

- internal owners are still coherent without the facade;
- dependency direction remains acyclic/intelligible;
- each state fact has one rightful writer or explicit reconciliation law;
- no hidden legacy/new dispatch is required;
- the facade can be described as a contract, not as a bag of compensating logic.

Then verify caller behavior through the facade and internal invariants at their owners.

Invariant:

> The facade compresses access to a coherent subsystem; it does not manufacture the appearance of one.

## Done When
The clean API is the visible consequence of clean ownership underneath, not a curtain drawn across unresolved architecture.
