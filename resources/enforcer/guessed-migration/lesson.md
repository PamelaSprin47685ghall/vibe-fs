# guessed-migration — Lesson

## Opinion

### Definition

A migration is guessed when old durable data is heuristically reinterpreted as a newer schema without an explicit, versioned rule that says what the old bytes meant and how they become the new meaning. The root-cause is that present shape is treated as evidence of past language, so recovery invents a version the bytes never recorded and upgrades under fabricated certainty.

### Governing Principle

Persistence turns representation into history. Once bytes survive code versions, their meaning cannot be recovered safely from today’s shape alone. Heuristics such as “if field X exists, this was probably v2” convert ambiguity into fabricated certainty. A migration must be a function from a known old language to a known new language, not an act of archaeological optimism.

### Trigger When

Trigger when recovery infers legacy schema from field presence, shape, timestamps, file names, or best-effort parsing and silently upgrades data without a declared source version.

### Do Not Trigger When

- Do not trigger when the format has an unambiguous self-describing version and deterministic migration functions cover every supported predecessor.
- Do not trigger for a one-time, operator-authorized conversion whose assumptions are written down and applied offline, not on every recovery.
- Do not trigger when unknown or mixed versions fail closed and never produce a guessed upgrade.

### Distinguish From

unversioned-schema creates the ambiguity. partial-write-assumption invents storage outcomes. This rule is the unsafe attempt to resolve historical schema ambiguity after the fact. Tie-break: if writers never recorded a version, start with unversioned-schema; if recovery invents the old language from shape, use this rule.

### Decision Procedure

Name the exact old schema version and its semantics. If that cannot be established from durable evidence, fail closed or require an explicit one-time migration decision rather than guessing.

### Examples

- positive: Recovery treats “field X present” as v2 and upgrades silently, with no stored version.
- near-miss: A documented one-time import maps a known dump format under operator approval, then stores an explicit version.
- counterexample: Bytes carry a version tag and a pure function migrates each supported predecessor.

## Advice

### What To Do Now

Replace heuristic legacy detection with explicit schema versions and deterministic migrations from each supported historical version. Durable schema identity is who owns migration: only a recorded version may authorize a deterministic transform; shape resemblance is not an owner.

### Why This Matters

Durable data is a conversation with past code. If the past did not record which language it was speaking, present code cannot safely infer meaning from resemblance. Silent guessing can produce valid-looking state whose provenance is false—the most dangerous kind of corruption because recovery appears successful.

### Repair Strategy

Establish authoritative version evidence. Write pure migration steps that transform one known schema to the next and test representative historical fixtures. For ambiguous unversioned data, fail closed or perform a separately authorized migration with documented assumptions.

### Decision Branches

- If durable evidence names the old version, apply the matching deterministic migration and persist the new version.
- If the old language cannot be proven, fail closed or run an explicit one-time authorized conversion; do not guess on every recovery.

### Common Wrong Fixes

- Do not stack more shape heuristics until fixtures pass. More guesses increase the number of ambiguous histories accepted as truth.
- Do not treat “it parsed under the new type” as proof the old bytes meant that type.
- Do not default unknown records to the latest schema because the fields look similar.

### Verification

The same old bytes must always produce the same upgraded value, independent of clock, environment, or current filesystem state, and unknown versions must be rejected explicitly. That determinism is the migration invariant: known version in, unique value out, unknown version refused.

### Done When

Recovery never asks “what schema does this look like?”; it reads a version and applies a defined semantic transformation.

### Nudge

Historical bytes deserve explicit semantics. Migrate from a known version with a deterministic rule; where history is ambiguous, refuse to invent it.
