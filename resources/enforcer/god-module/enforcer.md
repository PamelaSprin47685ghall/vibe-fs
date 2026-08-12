# god-module — Enforcer

## Definition
A god module is not a large file. It is a module that has accumulated **multiple independent sovereignties** — policies, state, effects, resources, or domain responsibilities that can legitimately change for different reasons — merely because centralizing them was convenient.

Size may make the problem visible. Ownership makes it a defect.

## Governing Principle
Code should be colocated when one invariant requires joint ownership, not because many callers need a convenient place to find it.

A large parser generated from one grammar may be perfectly coherent. A 120-line “Service” that decides authorization, persistence policy, retry behavior, cache invalidation, billing rules, telemetry semantics, and deployment toggles is already a god module even if it fits on one screen.

The pathology is sovereignty collapse: independent reasons to change are forced through one owner, so unrelated decisions share state, tests, dependencies, and lifecycle.

## Trigger When
Trigger when a module owns several responsibilities that do not need one another to preserve a common invariant. Typical signs:

- unrelated policy decisions are made in the same module because it is the central coordinator;
- the module imports many distinct infrastructure resources and also owns domain decisions about each;
- separate teams/features routinely edit different regions of the module without touching the same invariant;
- tests require huge fixture construction because exercising one responsibility initializes many unrelated ones;
- mutable state/lifecycle for independent resources is stored together;
- errors from unrelated domains are normalized/handled in one giant switch;
- a “manager/service/runtime/context” object becomes the route through which nearly every capability is accessed;
- extracting one responsibility feels difficult mainly because everything can reach everything else through the central module;
- the module acts as scheduler, repository, policy engine, cache owner, protocol adapter, and event publisher at once.

## Do Not Trigger When
- The module is large because one coherent algebra/protocol/state machine genuinely has many cases.
- Generated/declarative tables are extensive but have one owner and one reason to change.
- A composition root wires many dependencies but does not own their policies; constructing a system is a distinct responsibility.
- A facade exposes several operations of one coherent subsystem while policy remains with internal owners.
- A transaction boundary legitimately coordinates several effects because atomicity itself is the invariant.
- A small number of closely related responsibilities must change together to keep one contract true.

## Distinguish From
`generic-helper-bucket` accumulates ownerless odds and ends; a god module usually owns too much **with authority**, not merely too many helpers.

`incidental-complexity-dominates` is broader and may arise from too many layers instead of too much centralization. `boundary-collapse` concerns distinctions crossing a boundary incorrectly. Here the boundary itself has swallowed multiple independent domains.

## Decision Procedure
List the module's decisions and state, not its functions.

For each item ask:

- What invariant does this protect?
- What event/requirement could make it change?
- What resource/effect lifecycle does it own?
- Which other items must change with it for the same domain reason?

Cluster only items with the same answers.

If several clusters remain independent but share one module merely for convenience, the module is god-like regardless of line count.

## Examples
- positive: `AppRuntime` owns session state, auth policy, retry strategy, Git operations, PTY processes, cache, billing limits, and review logic through one mutable object.
- positive: `UserService` validates business policy, executes SQL, sends email, writes audit events, manages cache TTL, and chooses HTTP status codes.
- positive: a 150-line module has four unrelated mutable dictionaries with separate lifecycles and consumers.
- near-miss: a 900-line generated parser implements one grammar and is regenerated from one source.
- near-miss: a composition root constructs many resources but delegates every decision immediately to their owners.
- counterexample: a transaction coordinator touches storage and event publication because atomic commit/order is the one invariant it owns.

## Nudge
Large is not the crime.

The crime is making unrelated truths answer to the same sovereign.
