# god-module — Main

## What To Do Now
Split sovereignty, not lines.

Identify independent invariants, policy owners, state lifecycles, and effect boundaries inside the module. Give each coherent cluster an owner of its own, then keep only the coordination that genuinely requires joint authority.

Do not start by choosing target file sizes.

## Why This Matters
God modules turn local change into systemic risk.

Because unrelated responsibilities share one owner, they also tend to share dependencies, mutable state, initialization order, error handling, and tests. A change to retry policy can break session lifecycle. A billing field can force construction of Git fixtures. A persistence refactor can touch authorization code because both live in the same giant context.

The module becomes organizational gravity: new work is added there because all useful dependencies are already available there, which gives the next change an even stronger reason to add more. Convenience becomes a positive feedback loop.

## Repair Strategy
Perform a sovereignty map:

1. enumerate decisions, mutable state, owned resources, and side effects;
2. group by invariant/reason-to-change;
3. identify dependencies between groups and distinguish real causality from convenience access;
4. extract independent owners with the minimum capabilities they need;
5. move state with the owner that controls its lifecycle;
6. keep cross-owner coordination in a narrow workflow/composition layer that does not steal their policies;
7. replace broad context/service access with explicit ports/values;
8. update tests so each owner can be exercised without constructing unrelated worlds.

Extraction may produce one large module and several small ones. That is fine. The goal is coherent authority, not visual symmetry.

## Decision Branches
- **One coherent state machine is large:** keep it together; improve representation/tests instead of arbitrary splitting.
- **Several independent mutable resources share one context:** give each lifecycle an owner and make borrowing explicit.
- **A composition root only wires dependencies:** leave it as composition; do not confuse knowledge of construction with policy ownership.
- **A workflow truly coordinates several owners:** keep orchestration narrow and declarative; decisions remain with owners.
- **Two responsibilities always change together because one invariant spans them:** keep them together even if names differ.
- **Extraction creates cycles:** revisit ownership; do not solve by reciprocal references or a new shared god context.

## Common Wrong Fixes
- Split a 1000-line file into `Part1`, `Part2`, `Helpers`, and `Context` while every part still reaches all shared state.
- Create a new facade over the god module and leave all sovereignty underneath.
- Move methods into classes/modules but keep one giant dependency container passed everywhere.
- Extract only pure helper functions while all policy and state remain centralized.
- Enforce arbitrary “max 200 lines” and celebrate compliance. A ten-file distributed god object is still a god object.
- Create a mediator/event bus for every call so dependencies become invisible rather than reduced.
- Introduce microservices around responsibilities that were not semantically independent; network boundaries do not manufacture ownership quality.

## Verification
Take several representative changes that previously touched unrelated regions.

After repair:

- each change should primarily touch the owner of the relevant invariant;
- tests for one owner should not require fixtures/resources from unrelated owners;
- capabilities passed to each owner should be narrower;
- mutable state should have clear lifecycle and writer;
- coordination code should be readable as sequencing, not as a second policy engine;
- deleting one owner should not require understanding the internals of all others.

Invariant:

> Things share an owner because correctness requires joint authority, not because a central object made access easy.

## Done When
The architecture can grow by adding or changing one sovereignty without reopening the whole empire.
