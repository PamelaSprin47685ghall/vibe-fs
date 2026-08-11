# phase-flag-accumulation — Enforcer

## Definition
Phase flags accumulate when lifecycle behavior is patched by adding booleans or counters whose combinations implicitly encode a state machine no type or transition table names.

## Governing Principle
Each independent flag multiplies possible worlds. When flags actually describe one lifecycle, the product type invents combinations the process can never legitimately enter and hides which transitions connect the valid ones. The resulting system is a state machine expressed as arithmetic over bits, so correctness requires readers to reconstruct states from combinations and control history.

## Trigger When
Trigger when new lifecycle bugs are fixed by adding flags such as `started`, `done`, `waiting`, `retrying`, `cancelled`, `hasLease`, or similar fields whose interactions grow combinatorially.

## Do Not Trigger When
- The flags represent genuinely independent predicates that may combine freely and do not determine lifecycle control.
- A single named state/enum already owns the lifecycle and flags are unrelated features.
- The boolean is a local temporary inside one function, not accumulated durable phase.

## Distinguish From
program-counter-state stores the next control position directly. boolean-blindness is broader loss of named meaning. Tie-break: if repeated flag patches form an implicit lifecycle automaton, this rule; if a field is the interpreter's resume position, program-counter-state; if a boolean erases meaning without forming a lifecycle, boolean-blindness.

## Decision Procedure
Enumerate meaningful flag combinations as named states. If only a small subset of the bit product is valid, replace the flags with those states or with structured control flow that makes phase local.

## Examples
- positive: A job gains `started`, `waiting`, `retrying`, and `done` booleans; `started && done && retrying` is representable and buggy.
- near-miss: `notifyEmail` and `notifySms` are independent preferences, not lifecycle phases.
- counterexample: Lifecycle is a closed `Pending | Running | Failed | Succeeded` with explicit transitions.

## Nudge
If booleans collectively answer "where are we in the lifecycle?", stop multiplying flags. Model the lifecycle directly and make legal transitions explicit.
