# half-finished-refactor — Enforcer

## Definition
A refactor is half-finished when the new ownership model has been introduced but the old one remains live enough that the system still needs routing rules, compatibility adapters, duplicated state, or social convention to decide which world is authoritative.

The code has moved. Authority has not finished moving.

## Governing Principle
A structural refactor is complete only when the post-refactor model becomes the ordinary truth of the repository.

Introducing a new service/module/type while preserving the old writer “for safety” creates a dual constitution. Every caller now has to answer a question the refactor was supposed to eliminate: old path or new path? The answer tends to leak into flags, adapters, `if legacy`, call-site folklore, mirrored tests, and reconciliation code.

Transition states are legitimate. Permanent transitions are architecture failures.

## Trigger When
Trigger when old and new internal models remain simultaneously authoritative after a refactor that was meant to replace ownership. Common forms:

- repository-owned callers are split between legacy and new APIs with no external compatibility requirement;
- both old and new modules can mutate the same semantic fact;
- adapters translate every call between old/new representations instead of completing migration;
- feature flags select architectures rather than product behavior, long after rollout uncertainty ended;
- tests are duplicated for legacy/new paths, with both expected to remain green indefinitely;
- new code reads one source of truth but old callbacks/jobs still write another;
- aliases re-export old names forever, so vocabulary never converges;
- the refactor stops at “all new code should use X,” while old code remains a first-class path nobody owns removing.

## Do Not Trigger When
- A bounded migration window intentionally supports old/new external consumers and has a real exit condition.
- Blue/green or rolling deployment temporarily requires two versions to coexist across process boundaries.
- Historical durable decode remains for recovery while current writes/ownership have fully converged.
- The “old” and “new” modules were discovered to own genuinely distinct responsibilities; coexistence is then not transitional duplication.
- A refactor intentionally scopes only one subsystem and the untouched owner remains correct outside that scope.

## Distinguish From
`compatibility-cruft` can preserve old external shapes even after internal ownership converges. `half-finished-refactor` specifically means internal authority itself has not converged.

`facade-hides-mess` often masks this state by routing between old/new paths behind a clean API. `duplicated-truth` may describe a symptom when both sides store the same fact; this rule names the unfinished ownership transfer causing it.

## Decision Procedure
State the intended post-refactor ownership in one sentence:

> After this refactor, X alone owns decision/state Y.

Then search every repository-controlled read/write/call path for Y.

If any ordinary path still requires the old owner, ask whether an external contract or bounded migration condition truly requires it. If not, the refactor is unfinished.

Pay special attention to background jobs, callbacks, tests, aliases, recovery paths, generated bindings, and “temporary” flags; these are where old authority hides after mainline callers migrate.

## Examples
- positive: `NewSessionStore` is introduced, but one retry path still writes `LegacySessionCache`; a synchronizer keeps both coherent.
- positive: all new callers use `newExecute`, but old `execute` remains exported and half the tests exercise it “for compatibility” even though no external consumer exists.
- positive: a feature flag chooses old/new persistence implementation months after rollout, and both schemas still receive writes.
- near-miss: a rolling deployment temporarily dual-reads versions until all old nodes drain; fleet convergence is the removal condition.
- counterexample: all internal callers/writers migrate to one owner; only historical v1 decode remains quarantined at recovery ingress.

## Nudge
A refactor is not finished when the new world exists.

It is finished when the repository no longer needs to remember how to live in the old one.
