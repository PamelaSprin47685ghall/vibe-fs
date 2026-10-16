# phase-flag-accumulation — Lesson

## Opinion

### Definition

Phase flags accumulate when lifecycle behavior is patched by adding booleans or counters whose combinations implicitly encode a state machine no type or transition table names. The root-cause is that lifecycle is patched as a product of flags, so the representable combinations exceed the valid phases and transitions stay unnamed.

### Governing Principle

Each independent flag multiplies possible worlds. When flags actually describe one lifecycle, the product type invents combinations the process can never legitimately enter and hides which transitions connect the valid ones. The resulting system is a state machine expressed as arithmetic over bits, so correctness requires readers to reconstruct states from combinations and control history.

### Trigger When

Trigger when new lifecycle bugs are fixed by adding flags such as `started`, `done`, `waiting`, `retrying`, `cancelled`, `hasLease`, or similar fields whose interactions grow combinatorially.

### Do Not Trigger When

- The flags represent genuinely independent predicates that may combine freely and do not determine lifecycle control.
- A single named state/enum already owns the lifecycle and flags are unrelated features.
- The boolean is a local temporary inside one function, not accumulated durable phase.

### Distinguish From

program-counter-state stores the next control position directly. boolean-blindness is broader loss of named meaning. Tie-break: if repeated flag patches form an implicit lifecycle automaton, this rule; if a field is the interpreter's resume position, program-counter-state; if a boolean erases meaning without forming a lifecycle, boolean-blindness.

### Decision Procedure

Enumerate meaningful flag combinations as named states. If only a small subset of the bit product is valid, replace the flags with those states or with structured control flow that makes phase local.

### Examples

- positive: A job gains `started`, `waiting`, `retrying`, and `done` booleans; `started && done && retrying` is representable and buggy.
- near-miss: `notifyEmail` and `notifySms` are independent preferences, not lifecycle phases.
- counterexample: Lifecycle is a closed `Pending | Running | Failed | Succeeded` with explicit transitions.

## Advice

### What To Do Now

Replace interacting lifecycle flags with a smaller explicit state model or structured control flow whose valid phases and transitions are named. The named lifecycle type (or the local control scope that owns phase) is who owns the state-space invariant that representable phases equal the valid lifecycle, not the boolean product.

### Why This Matters

Flags scale as combinations, while real lifecycles usually scale as a sequence or small graph. The gap becomes illegal states and conditional logic whose purpose is to rule out worlds the representation should never have admitted.

### Repair Strategy

List valid lifecycle states and the data meaningful in each. Encode them as closed cases or keep phase-local data inside the control scope that owns it. Remove obsolete flags rather than mirroring the new state with old booleans.

### Decision Branches

- If flags encode one lifecycle, replace the product with named states and legal transitions.
- If flags are independent predicates that may combine freely, leave them; they are not a hidden automaton.

### Common Wrong Fixes

- Add a master `phase` enum while retaining all old flags "for convenience," recreating duplicated truth.
- Add another boolean to forbid the latest illegal combination.
- Keep flags and sprinkle assertions that reconstruct the state machine in prose.

### Verification

Every valid transition should be explicit and every former contradictory flag combination should become unconstructable. The invariant is that the lifecycle state space matches the valid phases, not the boolean product.

### Done When

The lifecycle has one representation whose state space matches reality, and new behavior extends named transitions rather than adding another boolean patch.

### Nudge

If booleans collectively answer "where are we in the lifecycle?", stop multiplying flags. Model the lifecycle directly and make legal transitions explicit.
