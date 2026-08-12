# facade-hides-mess — Enforcer

## Definition
A facade hides mess when a clean entry point is used as evidence that the architecture is clean while ownership, dependency direction, duplicated state, or side-effect boundaries underneath remain unresolved.

A facade can simplify a caller. It cannot, by itself, simplify the system it forwards into.

## Governing Principle
Surface area and internal structure are different properties.

A narrow API is valuable when it represents a genuine subsystem contract: callers need fewer concepts, internals retain one coherent owner, and dependency edges become easier to reason about. The anti-pattern appears when the facade is a cosmetic border around the same tangled graph.

Typical symptom: the new API looks excellent in examples, but implementing one method still requires touching five mutually dependent modules, synchronizing duplicate state, or knowing private ordering rules. The mess has not disappeared. It has acquired a receptionist.

## Trigger When
Trigger when a facade/wrapper is presented as the architectural repair but underlying responsibility remains structurally unchanged. Common forms:

- facade methods are thin one-to-one forwarding aliases over unrelated subsystems;
- callers are hidden from a cyclic dependency, but the cycle still exists behind the facade;
- duplicate owners remain and the facade simply decides which one to call based on flags/context;
- multiple incompatible representations remain, with the facade translating among them on every operation;
- tests of the facade mock a maze of internals, proving the wrapper but not repairing coupling;
- internal modules continue importing across forbidden layers; only external callers use the clean front;
- the facade accumulates orchestration/policy because nobody fixed ownership underneath, becoming a new god module;
- “migration complete” is claimed because callers use the facade even though old and new execution paths still coexist behind it.

## Do Not Trigger When
- The facade is the intentional stable contract of a coherent subsystem and internals have clear ownership.
- The wrapper isolates a real external/framework boundary and translation there is semantic work.
- A temporary migration facade has a named consumer set, bounded overlap, and concrete removal condition.
- The facade deliberately provides a capability-safe subset over a larger internal API; restricting authority is real semantic work.
- The caller-facing simplification itself is the goal, and nobody claims the internal architecture was repaired.

## Distinguish From
`half-finished-refactor` focuses on old/new ownership models coexisting. `facade-hides-mess` focuses on a clean front being mistaken for structural repair.

`boundary-collapse` is the opposite direction: boundaries fail to preserve distinctions. Here a boundary may look crisp externally while internal ownership remains broken.

`god-module` may emerge when the facade starts owning every policy. The facade is not automatically a god module; it becomes one when forwarding turns into unrelated sovereignty.

## Decision Procedure
Ignore the public API for a moment and draw the dependency/ownership graph behind one representative facade call.

Ask:

- Who owns each decision?
- How many representations of the same fact exist?
- Which module can mutate each piece of state?
- Are dependency cycles gone or merely hidden?
- Could the facade be deleted tomorrow without exposing a cleaner internal boundary underneath?

If the answer to the last question is “deleting it reveals exactly the old mess,” the facade did not repair the architecture.

## Examples
- positive: `UserService.update()` looks clean but forwards through legacy manager, new manager, compatibility adapter, and state synchronizer; both managers remain writers.
- positive: a facade breaks no cycle; A still imports B, B still imports A, and external code merely imports `Facade` instead.
- positive: a “repository facade” contains branching rules for SQL, cache, event publication, authorization, retries, and migration because those owners were never separated.
- near-miss: a narrow port hides an SDK client; the adapter owns only protocol translation, while domain decisions remain elsewhere.
- counterexample: the facade is introduced after internal ownership is consolidated, and it exposes a deliberately small stable contract over that coherent subsystem.

## Nudge
A clean door does not make the room clean.

Open it. Inspect who actually owns the mess.
