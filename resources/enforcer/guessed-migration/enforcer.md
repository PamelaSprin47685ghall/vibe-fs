# guessed-migration — Enforcer

## Definition
A migration is guessed when old durable data is heuristically reinterpreted as a newer schema without an explicit, versioned rule that says what the old bytes meant and how they become the new meaning.

## Governing Principle
Persistence turns representation into history. Once bytes survive code versions, their meaning cannot be recovered safely from today’s shape alone. Heuristics such as “if field X exists, this was probably v2” convert ambiguity into fabricated certainty. A migration must be a function from a known old language to a known new language, not an act of archaeological optimism.

## Trigger When
Trigger when recovery infers legacy schema from field presence, shape, timestamps, file names, or best-effort parsing and silently upgrades data without a declared source version.

## Do Not Trigger When
- Do not trigger when the format has an unambiguous self-describing version and deterministic migration functions cover every supported predecessor.
- Do not trigger for a one-time, operator-authorized conversion whose assumptions are written down and applied offline, not on every recovery.
- Do not trigger when unknown or mixed versions fail closed and never produce a guessed upgrade.

## Distinguish From
unversioned-schema creates the ambiguity. partial-write-assumption invents storage outcomes. This rule is the unsafe attempt to resolve historical schema ambiguity after the fact. Tie-break: if writers never recorded a version, start with unversioned-schema; if recovery invents the old language from shape, use this rule.

## Decision Procedure
Name the exact old schema version and its semantics. If that cannot be established from durable evidence, fail closed or require an explicit one-time migration decision rather than guessing.

## Examples
- positive: Recovery treats “field X present” as v2 and upgrades silently, with no stored version.
- near-miss: A documented one-time import maps a known dump format under operator approval, then stores an explicit version.
- counterexample: Bytes carry a version tag and a pure function migrates each supported predecessor.

## Nudge
Historical bytes deserve explicit semantics. Migrate from a known version with a deterministic rule; where history is ambiguous, refuse to invent it.
