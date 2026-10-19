# facade-hides-mess — Lesson

## Opinion

### Definition

A facade hides mess when a clean entry point is used as evidence that the architecture is clean while ownership, dependency direction, duplicated state, or side-effect boundaries underneath remain unresolved.

A facade can simplify a caller. It cannot, by itself, simplify the system it forwards into.

### Governing Principle

Surface area and internal structure are different properties.

A narrow API is valuable when it represents a genuine subsystem contract: callers need fewer concepts, internals retain one coherent owner, and dependency edges become easier to reason about. The anti-pattern appears when the facade is a cosmetic border around the same tangled graph.

Typical symptom: the new API looks excellent in examples, but implementing one method still requires touching five mutually dependent modules, synchronizing duplicate state, or knowing private ordering rules. The mess has not disappeared. It has acquired a receptionist.

### Trigger When

Trigger when a facade/wrapper is presented as the architectural repair but underlying responsibility remains structurally unchanged. Common forms:

- facade methods are thin one-to-one forwarding aliases over unrelated subsystems;
- callers are hidden from a cyclic dependency, but the cycle still exists behind the facade;
- duplicate owners remain and the facade simply decides which one to call based on flags/context;
- multiple incompatible representations remain, with the facade translating among them on every operation;
- tests of the facade mock a maze of internals, proving the wrapper but not repairing coupling;
- internal modules continue importing across forbidden layers; only external callers use the clean front;
- the facade accumulates orchestration/policy because nobody fixed ownership underneath, becoming a new god module;
- “migration complete” is claimed because callers use the facade even though old and new execution paths still coexist behind it.

### Do Not Trigger When

- The facade is the intentional stable contract of a coherent subsystem and internals have clear ownership.
- The wrapper isolates a real external/framework boundary and translation there is semantic work.
- A temporary migration facade has a named consumer set, bounded overlap, and concrete removal condition.
- The facade deliberately provides a capability-safe subset over a larger internal API; restricting authority is real semantic work.
- The caller-facing simplification itself is the goal, and nobody claims the internal architecture was repaired.

### Distinguish From

`half-finished-refactor` focuses on old/new ownership models coexisting. `facade-hides-mess` focuses on a clean front being mistaken for structural repair.

`boundary-collapse` is the opposite direction: boundaries fail to preserve distinctions. Here a boundary may look crisp externally while internal ownership remains broken.

`god-module` may emerge when the facade starts owning every policy. The facade is not automatically a god module; it becomes one when forwarding turns into unrelated sovereignty.

### Decision Procedure

Ignore the public API for a moment and draw the dependency/ownership graph behind one representative facade call.

Ask:

- Who owns each decision?
- How many representations of the same fact exist?
- Which module can mutate each piece of state?
- Are dependency cycles gone or merely hidden?
- Could the facade be deleted tomorrow without exposing a cleaner internal boundary underneath?

If the answer to the last question is “deleting it reveals exactly the old mess,” the facade did not repair the architecture.

### Examples

- positive: `UserService.update()` looks clean but forwards through legacy manager, new manager, compatibility adapter, and state synchronizer; both managers remain writers.
- positive: a facade breaks no cycle; A still imports B, B still imports A, and external code merely imports `Facade` instead.
- positive: a “repository facade” contains branching rules for SQL, cache, event publication, authorization, retries, and migration because those owners were never separated.
- near-miss: a narrow port hides an SDK client; the adapter owns only protocol translation, while domain decisions remain elsewhere.
- counterexample: the facade is introduced after internal ownership is consolidated, and it exposes a deliberately small stable contract over that coherent subsystem.

## Advice

### What To Do Now

Repair the structure the facade is concealing.

Choose the real owners of state and policy, remove duplicated writers and dependency cycles, collapse obsolete representations, and make internal dependency direction honest. Keep or reintroduce the facade only after it corresponds to a coherent subsystem contract or a genuinely useful capability boundary.

### Why This Matters

Cosmetic facades are dangerous because they improve local ergonomics enough to stop the refactor early.

New callers see one clean object and conclude the subsystem is fixed. Meanwhile every implementation change still traverses the same coupled graph. The organization loses pressure to finish the ownership repair because the public ugliness — the part everyone complained about — is gone.

This creates a two-story architecture: clean documentation upstairs, haunted machinery downstairs. Incidents and large changes always happen downstairs.

### Repair Strategy

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

### Decision Branches

- **Facade forwards one coherent subsystem:** keep it if the caller ergonomics/stability are useful.
- **Facade dispatches between old/new owners:** finish migration; see `half-finished-refactor`.
- **Facade translates an external protocol:** keep translation at the edge; do not spread external shapes inward.
- **Facade owns authorization/capability narrowing:** that may be a genuine semantic boundary; preserve the restriction explicitly.
- **Facade contains many unrelated policies because internals are chaotic:** move each policy to its rightful owner before shrinking the facade.
- **Only external API cleanup was required:** do not overclaim internal repair. A caller-surface task can be complete without pretending architecture changed underneath.

### Common Wrong Fixes

- Add a second facade over the first.
- Rename internal modules/services while leaving dependency direction and writers unchanged.
- Hide internals with package-private/export restrictions and call the coupling solved.
- Write integration tests only against the facade so nobody notices duplicate paths remain live.
- Move all orchestration into the facade, turning cosmetic cleanup into a new god module.
- Keep old internals forever “because callers no longer see them.” Hidden debt still executes.
- Produce architecture diagrams showing only the facade and omit the graph behind it.

### Verification

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

### Done When

The clean API is the visible consequence of clean ownership underneath, not a curtain drawn across unresolved architecture.

### Nudge

A clean door does not make the room clean.

Open it. Inspect who actually owns the mess.
