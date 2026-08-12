# primitive-obsession — Enforcer

## Definition
Primitive obsession appears when a representation that says only **how a value is stored** is asked to carry a distinction the domain depends on for correctness.

The classic symptom is several `string`, `int`, or `decimal` values that are not interchangeable in reality but are interchangeable to the program: `UserId` and `OrderId`, cents and basis points, absolute path and workspace-relative path, trusted capability and untrusted token, digest and arbitrary text.

The defect is not “primitives exist.” Primitives are excellent representations. The defect is that representation has been allowed to erase identity at a boundary where identity matters.

## Governing Principle
A type is a proposition available to every caller after construction.

`string` proves only “this is text.” It does not prove “this text names a session,” “this text is a validated SHA-256 digest,” or “this text came from a capability authority.” When those propositions matter, leaving them in variable names and comments means the compiler cannot reject category errors and every consumer must remember invisible law.

But strong typing can become theater too. Wrapping every string in a one-field type without changing construction, validation, or boundary semantics merely moves punctuation around. A domain type earns its keep when it prevents a real substitution, centralizes a real invariant, or makes a real boundary explicit.

## Trigger When
Trigger when values sharing a primitive representation have different semantic identities and can cross a meaningful boundary in the wrong position without construction/type failure. Typical cases:

- several IDs are plain strings and sibling IDs can be passed to the wrong API;
- money, percentages, durations, byte counts, timestamps, or units share numbers with incompatible semantics;
- raw path strings blur absolute/relative/normalized/workspace-scoped distinctions that affect safety;
- validated and unvalidated versions of the same textual input use the same type;
- capability/security tokens are indistinguishable from arbitrary text after admission;
- hashes/digests/version identifiers are accepted where generic strings are expected and later reparsed repeatedly;
- call sites contain adjacent same-typed primitives whose meaning can only be recovered from parameter order.

## Do Not Trigger When
- The boundary truly treats the value as generic text/number and domain identity is irrelevant there, such as rendering a log label after semantic decisions are complete.
- A primitive stays inside a tiny local expression/helper and cannot be confused across a semantic boundary.
- The value is transport/wire data that is immediately parsed into a stronger domain value before policy code sees it.
- Two concepts share a representation **and are genuinely interchangeable by contract**.
- Introducing a distinct type would add no rejected substitution, validation, unit distinction, or ownership information. Newtype count is not a quality metric.

## Distinguish From
`type-erosion-at-boundary` is about strong/static information being lost because dynamic/unchecked representations leak inward. `primitive-obsession` can occur in fully statically typed code: the static type is simply too weak to express domain identity.

`boolean-blindness` is the particularly damaging case where binary flags erase named choices. `illegal-state-representable` concerns impossible combinations rather than sibling values with the same primitive representation. `misleading-name` is vocabulary drift when the type itself may still be sound.

Tie-break on the violated proposition: if the bug is “a value of the wrong semantic category still type-checks here,” use this rule.

## Decision Procedure
Name the boundary and perform a substitution test:

> Could I pass a different value with the same primitive representation, but a domain meaning reality forbids, and have construction/type checking accept it?

If yes, ask what proposition distinguishes the legal value: identity, unit, validation state, trust level, namespace, coordinate system, lifecycle stage.

That proposition is the candidate type boundary.

Then ask the anti-theater question: will the proposed type actually prevent the substitution or centralize a meaningful invariant? If not, do not manufacture a wrapper merely to look domain-driven.

## Examples
- positive: `loadSession(userId: string)` compiles because both `UserId` and `SessionId` are strings.
- positive: `retryAfter: number` is sometimes milliseconds and sometimes seconds depending on caller; both compile and failures appear as latency anomalies.
- positive: a filesystem deletion API accepts an arbitrary string after validation occurred elsewhere, so a raw path can bypass the validated workspace path concept.
- positive: `ValidatedEmail` is converted back to string immediately and every downstream function accepts string, so validation state is erased at the boundary.
- near-miss: a JSON serializer accepts arbitrary strings because semantic identity has already been decided and serialization is intentionally generic.
- near-miss: `UserId = string` is a type alias used only for documentation in a language where nominal separation is unavailable; it may be weak, but the rule should target an actual dangerous boundary rather than alias syntax itself.
- counterexample: `SessionId`, `UserId`, and `WorkspacePath` have distinct constructing APIs and cannot be substituted across domain calls.

## Nudge
Representation answers “what bits are these?”

Domain type answers “what fact do these bits mean?”

Introduce the second only where the program actually needs to know the difference — and then make the difference impossible to forget.
