# boolean-blindness — Enforcer

## Definition
Boolean blindness appears when `true`/`false` is used to encode a **named domain choice** rather than a genuinely binary proposition, so the vocabulary disappears exactly where callers must make the choice.

The problem gets worse when several flags jointly encode one conceptual state. Two booleans create four representable combinations; five create thirty-two. If the domain only recognizes three meaningful modes, the remaining twenty-nine are fictional states introduced by the representation.

The defect is not “booleans exist.” `isEmpty`, `hasPermission`, `wasCached`, and many other predicates are naturally binary. The defect is asking bits to carry a vocabulary richer than yes/no.

## Governing Principle
A call site should reveal the choice being made.

`open(true, false)` forces the reader to retrieve meaning from parameter order, editor hints, or memory. `open(ReadWriteExisting)` carries the domain decision in the expression itself.

More importantly, products of flags change the state space. Once illegal combinations are representable, every consumer must either defend against them or quietly assume constructors were disciplined. That is a modeling failure disguised as flexibility.

Boolean blindness is therefore both a readability defect and, frequently, a correctness defect: it erases names while enlarging possibility.

## Trigger When
Trigger when booleans encode modes, phases, policy alternatives, permissions, result kinds, or mutually constrained states whose meanings have domain names. Typical signs:

- callers pass literal `true/false` arguments and need parameter hints/comments to know what they mean;
- several flags must satisfy rules such as “exactly one,” “at most one,” or “if A then not B”;
- a new mode is implemented by adding another boolean rather than another named case;
- the same flag means different things depending on sibling flags;
- serialized records contain clusters such as `isRunning/isDone/isFailed/isCancelled`;
- feature/policy code branches on boolean tuples that correspond to a small named set of modes;
- permissions like read/write/admin are represented as unrelated booleans even though only specific capability combinations are legal;
- APIs expose booleans where an action/mode enum would make call sites self-describing.

## Do Not Trigger When
- The value is a genuine predicate with exactly two semantic outcomes and no hidden third state: `isEmpty`, `contains`, `isAuthorized` as an observation.
- A returned boolean answers a yes/no question whose name remains visible at the call site.
- Independent boolean facts are genuinely independent; their Cartesian product is meaningful by contract.
- A wire/storage representation uses bits but the domain boundary immediately constructs named cases/capabilities.
- A boolean optimizes representation after the domain choice has already been made and cannot leak back into policy vocabulary.

## Distinguish From
`illegal-state-representable` is the broader state-space defect. Use `boolean-blindness` when booleans themselves are the mechanism erasing named alternatives; use the broader rule when arbitrary nullable fields/discriminants create impossible products.

`primitive-obsession` concerns semantic identity shared by primitives generally. `boolean-blindness` is sharper because boolean literals erase vocabulary at call sites and flag products explode possibility particularly fast.

`magic-boolean` style complaints about literal readability alone are too shallow; the rule should fire because a named domain choice or constrained state is being represented as bits.

## Decision Procedure
Write down the actual semantic alternatives without looking at the flags.

For a flag cluster, enumerate the full truth table and label every combination:

- meaningful named state;
- genuinely independent combination;
- impossible/undefined state.

If multiple rows map to domain names or some rows have no legitimate meaning, the boolean product is the wrong model.

For a single boolean parameter, ask: “Could I replace `true` and `false` with two names that communicate a real policy/mode?” If yes, prefer the named choice. If the answer is simply yes/no to the predicate already named by the function, keep the boolean.

## Examples
- positive: `openFile(path, true, false)` means write=true/create=false; a reviewer must inspect the signature to understand the call.
- positive: `{ isRunning, isCompleted, isFailed }` permits `true,true,true` even though lifecycle allows exactly one state.
- positive: `send(message, true)` where `true` means `RequireAcknowledgement`; the domain already has a name for the choice.
- positive: a third mode “dry run” is added by introducing `isDryRun` beside `isWrite`, producing contradictory combinations.
- near-miss: `collection.isEmpty(): bool` returns a direct binary observation; no domain vocabulary was erased.
- near-miss: `{ isEncrypted, isCompressed }` may be two genuinely independent facts if all four combinations are valid.
- counterexample: `FileOpenMode = Read | WriteExisting | WriteCreate` and call sites choose a named case.

## Nudge
A boolean should answer a question.

It should not force the reader to remember a vocabulary the type refused to name — and it should never create imaginary worlds just because bits are cheap.
