# pattern-sprawl — Enforcer

## Definition
Pattern sprawl occurs when design-pattern machinery becomes a second programming language layered over a host language that already expresses the required distinction more directly.

Factories, visitors, strategies, builders, command classes, registries, interface hierarchies, mediators, and template-method scaffolding are not inherently wrong. The defect is **ceremony without purchased capability**: objects and indirection remain after the original language/platform limitation that justified them no longer exists — or never existed here at all.

## Governing Principle
A pattern is a solution to a constraint, not a collectible architecture shape.

Patterns historically encode real ideas: closed variation, late binding, traversal, construction invariants, effect substitution, protocol dispatch. If the language now offers algebraic data types, pattern matching, first-class functions, modules, records, iterators, closures, traits/interfaces, or ordinary constructors that state the same law directly, reproducing the old object choreography can obscure rather than clarify the design.

The question is never “is this a known pattern?” The question is “what semantic capability does this pattern buy that the direct language form does not?”

## Trigger When
Trigger when pattern machinery simulates a capability already available more directly and its indirection now dominates understanding/change cost. Common forms:

- a closed set of cases is represented as many subclasses + visitor rather than data + exhaustive match;
- stateless one-method strategy classes exist where first-class functions would preserve the same contract;
- factories only choose among constructors already statically known, with no runtime discovery/configuration requirement;
- builders carry dozens of mutable flags for objects that could be constructed as validated immutable data;
- command objects merely wrap function calls and add no persistence/queuing/serialization/undo semantics;
- mediator/event bus routes ordinary synchronous calls solely to avoid direct dependency names;
- interface-per-class architecture creates one implementation for each interface with no independent substitution boundary;
- new “pattern” layers are introduced to make code look enterprise/clean/hexagonal despite no corresponding semantic boundary.

## Do Not Trigger When
- Runtime plugin discovery, open extension, serialization, distributed dispatch, undo/history, or independent substitution genuinely requires the pattern machinery.
- The host language lacks a safer/directer representation for the needed variation.
- A visitor intentionally separates many operations from a stable externally-owned object hierarchy that cannot be modified.
- A builder enforces nontrivial staged construction or validation that the language's ordinary constructor/type system cannot express cleanly.
- A command object is a durable message/event with identity, replay, queueing, auditing, or other first-class semantics beyond “call this function.”
- The pattern creates a real capability/authority boundary, not merely indirection.

## Distinguish From
`framework-tax` comes from a framework's ontology. `pattern-sprawl` may be entirely hand-written and dependency-free.

`premature-unification` invents a common abstraction before semantics justify it. `pattern-sprawl` may also affect a once-justified abstraction that has become obsolete as the language/system evolved.

`implicit-control-flow` can be caused by mediator/event pattern overuse; use it when invisibility of execution order is the sharper defect.

## Decision Procedure
For each pattern layer, state the semantic job without naming the pattern:

- “choose one behavior at runtime”;
- “represent one of these closed states”;
- “construct only valid values”;
- “traverse a structure without modifying its owner”;
- “queue/replay an operation.”

Then implement the same job mentally using the host language's direct constructs. What capability is lost?

If the answer is only “the classes make the pattern explicit,” “this is how OO does it,” or “we may add implementations later,” the machinery has not earned its cost.

## Examples
- positive: twelve AST node subclasses each implement `accept(visitor)` for a hierarchy entirely controlled by the repository; an F# discriminated union + match would be closed, exhaustive, and direct.
- positive: three classes implement `IRetryStrategy.Execute()` with no state; each simply calls one function and is selected by a local match.
- positive: `FooFactoryFactory` constructs the only `FooFactory` implementation so everything “goes through abstractions.”
- near-miss: external third parties register plugins at runtime, so a registry/factory boundary is genuinely open.
- near-miss: command objects are persisted and replayed across process restart; they carry durable identity beyond a function call.
- counterexample: a closed workflow is modeled as algebraic states with exhaustive transitions and ordinary functions.

## Nudge
Patterns are fossils of solved constraints.

Keep the constraint. Throw away the fossil when the language can state the law directly.
