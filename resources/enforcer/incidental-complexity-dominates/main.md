# incidental-complexity-dominates — Main

## What To Do Now
Make the domain operation visible again.

Identify the few distinctions the system genuinely must preserve — ownership, state, authority, persistence, failure, external contracts — and collapse machinery that does not protect one of them. Do not solve accidental complexity by wrapping it in one more abstraction.

## Why This Matters
Incidental complexity charges interest on every future change.

A redundant layer is not paid for once. It must be learned by each maintainer, mocked by tests, migrated during refactors, updated when schemas change, debugged during incidents, and preserved because nobody is certain whether it is secretly important.

Eventually the organization loses the ability to distinguish “required by the problem” from “required by our previous design.” At that point architecture becomes self-justifying: the system is complex because the architecture is complex, and the architecture must remain complex because the system has adapted around it.

## Repair Strategy
Start with a semantic inventory, not a file move:

1. name the domain action and its externally visible promise;
2. name the real owners and durable facts;
3. name effects/failure boundaries that genuinely need isolation;
4. mark every translation, wrapper, flag, registry, adapter, facade, and lifecycle object on the path;
5. for each, state the unique invariant or boundary it protects;
6. merge/delete mechanisms whose answer is duplicated, historical, or purely ceremonial;
7. update tests to protect the surviving semantic boundaries rather than old plumbing.

Prefer one representation inside one ownership boundary. Translate once at real ingress/egress. Derive state from durable truth when deterministic. Replace registration mazes with direct language references when dynamic discovery is not a real requirement.

Do not optimize for fewer files. Optimize for fewer independent concepts a maintainer must hold in mind to answer “what happens and why?”

## Decision Branches
- **Two layers own the same decision:** choose one semantic owner and make the other a mechanical adapter or delete it.
- **Two near-identical representations live inside one boundary:** collapse them unless each carries a distinct invariant.
- **Stored state can be derived from a more authoritative durable fact:** remove the duplicate writer and derive it.
- **Framework boilerplate is unavoidable:** confine it behind a narrow edge so domain code does not speak framework ontology.
- **A layer protects a real external contract or failure boundary:** keep it; document/name the distinction it owns.
- **The complexity is truly domain complexity:** improve names/types/tests, but do not flatten real states merely to reduce ceremony.

## Common Wrong Fixes
- Add a facade over the same layers and call the architecture simpler.
- Move files into smaller modules while preserving the same distributed ownership and call graph.
- Generate boilerplate automatically. Generated accidental complexity still has semantic/debugging cost.
- Replace explicit direct code with a generic framework to “standardize” the ceremony.
- Introduce one more canonical DTO and require all existing DTOs to translate through it.
- Delete real domain distinctions because “we want fewer types.” Simplicity that loses truth is corruption, not design.
- Apply arbitrary line/file count limits as the reason for splitting. Size can trigger a question; it cannot answer ownership.

## Verification
Take a representative change and compare before/after reasoning paths.

The repaired design should require fewer independent concepts and fewer synchronized edits **without losing a real invariant**. Tests should still falsify the same public/domain promises. Recovery, authority, and external boundaries must remain explicit where they matter.

A useful check is to ask a maintainer to explain the operation without using framework/plumbing nouns. If the explanation can now be mapped directly onto code owners and state, the repair is moving in the right direction.

Invariant:

> Essential distinctions remain explicit; solution-invented distinctions do not dominate the mental model.

## Done When
The implementation is again mostly about the problem it solves.

You can remove a layer and explain exactly what semantic burden disappeared — not merely where the code moved.
