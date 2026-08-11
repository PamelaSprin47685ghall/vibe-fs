# guessed-migration — Enforcer

## Definition
A migration is guessed when old durable data is heuristically reinterpreted as a newer schema without an explicit, versioned rule that says what the old bytes meant and how they become the new meaning.

## Governing Principle
Persistence turns representation into history. Once bytes survive code versions, their meaning cannot be recovered safely from today’s shape alone. Heuristics such as “if field X exists, this was probably v2” convert ambiguity into fabricated certainty. A migration must be a function from a known old language to a known new language, not an act of archaeological optimism.

## Trigger When
Trigger when recovery infers legacy schema from field presence, shape, timestamps, file names, or best-effort parsing and silently upgrades data without a declared source version.

## Do Not Trigger When
Do not trigger when the format has an unambiguous self-describing version and deterministic migration functions cover every supported predecessor.

## Distinguish From
unversioned-schema creates the ambiguity. partial-write-assumption invents storage outcomes. This rule is the unsafe attempt to resolve historical schema ambiguity after the fact.

## Decision Procedure
Name the exact old schema version and its semantics. If that cannot be established from durable evidence, fail closed or require an explicit one-time migration decision rather than guessing.

## Nudge
Historical bytes deserve explicit semantics. Migrate from a known version with a deterministic rule; where history is ambiguous, refuse to invent it.
