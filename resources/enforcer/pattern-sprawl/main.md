# pattern-sprawl — Main

## What To Do Now
Name the semantic capability the pattern is supposed to provide, then collapse the machinery into the simplest host-language construct that preserves that capability.

Do not remove indirection merely to be clever. Remove it when the direct form states the law more clearly and loses no real extension, lifecycle, durability, or authority boundary.

## Why This Matters
Pattern sprawl makes code look architecturally intentional while hiding that many layers are doing no semantic work.

Every pattern participant adds vocabulary: visitor, element, strategy, context, command, handler, factory, provider, builder, director, mediator. When those names correspond to real responsibilities, they are useful. When they only recreate language features, maintainers must translate the pattern back into the underlying idea before reasoning can begin.

The result is abstraction latency: a simple closed choice takes several files and dynamic calls to understand. Refactors become harder because the scaffolding itself acquires tests and consumers, then starts defending its own existence.

## Repair Strategy
Work pattern by pattern:

- **closed variants:** prefer algebraic data / enum + exhaustive match when the repository owns the case set;
- **stateless strategies:** prefer first-class functions or small modules when no independent identity/lifecycle exists;
- **construction:** prefer immutable constructors/smart constructors/types; keep builders only for genuinely staged or complex construction;
- **factory:** call constructors directly when selection is local/static; keep factory/registry for real runtime discovery or boundary substitution;
- **command:** use function/call for ephemeral local action; keep command object when it has durable identity, queue/replay/audit/undo semantics;
- **visitor:** use direct matching/traversal when the hierarchy is closed and owned; keep visitor when operations must vary independently over an external/stable hierarchy;
- **mediator/event:** use direct dependency for direct causality; keep messaging when decoupled temporal/distributed semantics are real.

Delete interfaces/classes/registrations/tests that only existed to maintain the obsolete pattern choreography.

## Decision Branches
- **Pattern buys open runtime extension:** keep it and test the extension contract.
- **Pattern buys durability/serialization/queueing:** keep the first-class message semantics.
- **Language has a closed/exhaustive form:** collapse inheritance/visitor ceremony if the case set is truly owned and closed.
- **Only reason is test mocking:** inject the actual effect/capability boundary rather than abstract every pure class.
- **Future implementations are merely hypothetical:** do not prepay their abstraction cost. Extract when independent variation becomes real.
- **Pattern vocabulary is already the public/external contract:** changing it may be a compatibility task, not a local cleanup.

## Common Wrong Fixes
- Replace one classic pattern with a fashionable pattern that has the same number of moving parts.
- Convert classes to functions but retain registry/factory/mediator layers that no longer serve a purpose.
- Delete a real runtime extension point because “functions are simpler.” Simplicity cannot erase an actual capability.
- Create generic combinator frameworks so the “simplified” version becomes more abstract than the original.
- Collapse closed states into strings/dictionaries just to reduce types. Direct does not mean untyped.
- Keep one-to-one interfaces “for consistency” after every independent substitution need is gone.

## Verification
Compare semantic surface before and after:

- can all valid variants still be represented?
- are invalid states no easier to create?
- is required runtime extension/durability preserved?
- is control flow more explicit rather than merely moved?
- are there fewer concepts a reader must learn before understanding the domain law?
- do tests protect behavior/contract rather than the removed pattern choreography?

Invariant:

> Every remaining layer of indirection buys a capability the direct host-language form cannot provide as clearly.

## Done When
The code names the problem before it names the pattern.

A reader can understand the semantic law without mentally decompiling an architecture diagram first.
