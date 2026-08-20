# half-finished-refactor — Main

## What To Do Now
Finish the transfer of authority.

Choose the post-refactor owner, migrate every repository-controlled caller/writer to it, remove obsolete adapters/aliases/flags/duplicate state, and make the old internal path impossible to use by accident.

Do not stabilize the transition. End it.

## Why This Matters
Half-finished refactors are often worse than the architecture they replace.

The old system had one set of rules, even if ugly. The transitional system has two sets plus routing logic, synchronization, migration conventions, and uncertainty about which behavior is canonical. Every bug now has an extra dimension: did it happen in old world, new world, or the seam?

Teams frequently stop because the new path works and mainline callers migrated. But background jobs, recovery code, aliases, tests, and rare callbacks preserve old authority for years. The transition becomes the permanent architecture nobody deliberately designed.

Session boundaries do not alter this completion criterion. If you can name a remaining repository-controlled migration step for “next session” and still have authority to perform it, you have named evidence that the refactor is still alive. A productive checkpoint or clean handoff is useful progress, not refactor closure.

## Repair Strategy
Treat the refactor as an ownership migration with a closure checklist:

1. write the target ownership rule explicitly;
2. enumerate all readers, writers, callers, exports, callbacks, jobs, recovery paths, tests, and generated surfaces touching the old owner;
3. classify which are repository-controlled and which are real external compatibility obligations;
4. migrate repository-controlled paths completely;
5. quarantine legitimate compatibility at the boundary;
6. stop dual writes and duplicated state once the migration condition is met;
7. delete obsolete aliases/flags/adapters/tests;
8. add an architecture/test gate if reintroduction of the old path would be easy.

Prefer deletion over permanent “deprecated” markers inside a closed repository. Deprecation without an external consumer is often just postponed ownership.

## Decision Branches
- **Old path has no external consumer:** delete it after migrating internal callers.
- **Real external consumer remains:** keep a narrow compatibility adapter with exit condition; internal model still stays singular.
- **Rolling deployment requires dual behavior:** encode fleet/version convergence and remove transition machinery afterward.
- **Historical recovery needs old shape:** retain decode-only boundary; do not retain old writer/owner.
- **Old/new responsibilities are actually distinct:** rename/reframe them as distinct owners rather than pretending one is replacing the other.
- **Migration is too risky in one step:** stage it, but every stage must reduce remaining old authority and preserve a concrete completion criterion.

## Common Wrong Fixes
- Add a synchronizer so both old and new sources can remain writers forever.
- Hide routing behind a facade and call migration complete.
- Leave aliases “for discoverability” after all callers moved, preserving old vocabulary indefinitely.
- Keep feature flags after rollout solely because removing them feels risky.
- Duplicate every new test on the old implementation to “ensure compatibility” without a supported compatibility contract.
- Mark old code deprecated but give nobody responsibility or condition for deletion.
- Rewrite documentation to prefer the new path while compiler/runtime still happily accepts the old one.

## Verification
Prove convergence structurally, not by intent:

- repository search finds no uncontrolled caller/writer of old owner;
- old exports/names are absent except explicit compatibility boundary;
- current state is written in one canonical representation;
- transition flags/adapters are gone or tied to a still-live bounded migration condition;
- tests exercise one internal truth, with legacy cases only where external/historical contract requires them;
- attempting to reintroduce an old-path call is caught by type/module/architecture constraints where practical.

Invariant:

> Inside the current system, one semantic fact has one post-refactor owner.

## Done When
The migration machinery has nothing left to arbitrate.

The new architecture is not “preferred.” It is simply the architecture.
