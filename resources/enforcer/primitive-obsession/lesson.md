# primitive-obsession — Lesson

## Opinion

### Definition

Primitive obsession appears when a representation that says only **how a value is stored** is asked to carry a distinction the domain depends on for correctness.

The classic symptom is several `string`, `int`, or `decimal` values that are not interchangeable in reality but are interchangeable to the program: `UserId` and `OrderId`, cents and basis points, absolute path and workspace-relative path, trusted capability and untrusted token, digest and arbitrary text.

The defect is not “primitives exist.” Primitives are excellent representations. The defect is that representation has been allowed to erase identity at a boundary where identity matters.

### Governing Principle

A type is a proposition available to every caller after construction.

`string` proves only “this is text.” It does not prove “this text names a session,” “this text is a validated SHA-256 digest,” or “this text came from a capability authority.” When those propositions matter, leaving them in variable names and comments means the compiler cannot reject category errors and every consumer must remember invisible law.

But strong typing can become theater too. Wrapping every string in a one-field type without changing construction, validation, or boundary semantics merely moves punctuation around. A domain type earns its keep when it prevents a real substitution, centralizes a real invariant, or makes a real boundary explicit.

### Trigger When

Trigger when values sharing a primitive representation have different semantic identities and can cross a meaningful boundary in the wrong position without construction/type failure. Typical cases:

- several IDs are plain strings and sibling IDs can be passed to the wrong API;
- money, percentages, durations, byte counts, timestamps, or units share numbers with incompatible semantics;
- raw path strings blur absolute/relative/normalized/workspace-scoped distinctions that affect safety;
- validated and unvalidated versions of the same textual input use the same type;
- capability/security tokens are indistinguishable from arbitrary text after admission;
- hashes/digests/version identifiers are accepted where generic strings are expected and later reparsed repeatedly;
- call sites contain adjacent same-typed primitives whose meaning can only be recovered from parameter order.

### Do Not Trigger When

- The boundary truly treats the value as generic text/number and domain identity is irrelevant there, such as rendering a log label after semantic decisions are complete.
- A primitive stays inside a tiny local expression/helper and cannot be confused across a semantic boundary.
- The value is transport/wire data that is immediately parsed into a stronger domain value before policy code sees it.
- Two concepts share a representation **and are genuinely interchangeable by contract**.
- Introducing a distinct type would add no rejected substitution, validation, unit distinction, or ownership information. Newtype count is not a quality metric.

### Distinguish From

`type-erosion-at-boundary` is about strong/static information being lost because dynamic/unchecked representations leak inward. `primitive-obsession` can occur in fully statically typed code: the static type is simply too weak to express domain identity.

`boolean-blindness` is the particularly damaging case where binary flags erase named choices. `illegal-state-representable` concerns impossible combinations rather than sibling values with the same primitive representation. `misleading-name` is vocabulary drift when the type itself may still be sound.

Tie-break on the violated proposition: if the bug is “a value of the wrong semantic category still type-checks here,” use this rule.

### Decision Procedure

Name the boundary and perform a substitution test:

> Could I pass a different value with the same primitive representation, but a domain meaning reality forbids, and have construction/type checking accept it?

If yes, ask what proposition distinguishes the legal value: identity, unit, validation state, trust level, namespace, coordinate system, lifecycle stage.

That proposition is the candidate type boundary.

Then ask the anti-theater question: will the proposed type actually prevent the substitution or centralize a meaningful invariant? If not, do not manufacture a wrapper merely to look domain-driven.

### Examples

- positive: `loadSession(userId: string)` compiles because both `UserId` and `SessionId` are strings.
- positive: `retryAfter: number` is sometimes milliseconds and sometimes seconds depending on caller; both compile and failures appear as latency anomalies.
- positive: a filesystem deletion API accepts an arbitrary string after validation occurred elsewhere, so a raw path can bypass the validated workspace path concept.
- positive: `ValidatedEmail` is converted back to string immediately and every downstream function accepts string, so validation state is erased at the boundary.
- near-miss: a JSON serializer accepts arbitrary strings because semantic identity has already been decided and serialization is intentionally generic.
- near-miss: `UserId = string` is a type alias used only for documentation in a language where nominal separation is unavailable; it may be weak, but the rule should target an actual dangerous boundary rather than alias syntax itself.
- counterexample: `SessionId`, `UserId`, and `WorkspacePath` have distinct constructing APIs and cannot be substituted across domain calls.

## Advice

### What To Do Now

Move the semantic distinction into the type boundary that already owns the value.

Create a distinct domain type, validated constructor, unit-bearing value, or similarly strong representation that rejects the dangerous sibling substitution. Migrate inward-facing callers to that type and confine primitive conversion to real ingress/egress adapters.

Do not start a newtype census. Repair the boundary where erased identity can cause a real category error.

### Why This Matters

Primitive obsession makes correctness depend on memory.

Humans read `accountId`, `orderId`, and `sessionId` and see three concepts. A type checker that sees three strings sees one concept repeated three times. That gap is exactly where category mistakes survive review: the value looks valid, serialization succeeds, logs look ordinary, and failure appears only when the wrong object is touched later.

Units are even more treacherous because wrong values can remain numerically plausible. Milliseconds passed as seconds do not necessarily crash; they create “mysterious” latency. Cents interpreted as dollars do not violate arithmetic; they violate meaning.

A good domain type makes one expensive distinction cheap forever.

### Repair Strategy

Repair from the boundary inward:

1. identify the dangerous sibling concepts sharing one primitive;
2. define the proposition each concept must carry — identity, unit, validation, trust, namespace, coordinate frame;
3. create one constructing boundary that establishes that proposition;
4. use the strong value through domain/application code;
5. convert to/from raw primitive only where an external protocol/storage/runtime genuinely requires it;
6. remove downstream reparsing/revalidation made obsolete by the stronger type;
7. add compile-time/constructor tests demonstrating the sibling substitution is rejected.

Prefer small types with small APIs. Do not turn a nominal distinction into an object hierarchy unless behavior truly belongs there.

For numeric units, use a unit-of-measure facility when the language supports it cleanly; otherwise distinct value types and explicit conversion can still preserve the law.

### Decision Branches

- **Sibling identifiers can be confused:** give each nominal identity and construct them at ingress.
- **Same number, different unit:** encode unit or use distinct value types; make conversions explicit and named.
- **Validated vs raw input:** return a strong validated type and do not immediately erase it back to the primitive.
- **Trusted/capability-bearing value:** separate admitted capability from untrusted token text; construction should correspond to the authority check.
- **Value is truly generic at this boundary:** leave it primitive. Strong typing should follow semantic distinctions, not fashion.
- **Language cannot enforce nominal distinction cheaply:** use the strongest available constructor/module boundary and tests; do not pretend an alias rejects substitutions when it does not.

### Common Wrong Fixes

- Rename variables while keeping every API primitive-typed. Better names help readers, not substitution safety.
- Create one-field wrappers and immediately expose `.value` everywhere so all domain APIs still consume primitives.
- Add runtime `assert(isUserId(x))` at every caller instead of establishing identity once.
- Build a “universal ID” wrapper carrying a string plus a `kind` field. That often recreates the same category error one level later.
- Wrap every string/number in the repository regardless of risk. Ceremony is not semantic precision.
- Hide implicit unit conversions inside overloaded operators so the type looks strong while meaning can still change silently.

### Verification

Prove the original mistake is now structurally difficult or impossible.

Attempt to:

- pass `OrderId` where `AccountId` is required;
- pass seconds where milliseconds are required;
- pass raw/unvalidated input where validated input is required;
- pass an arbitrary token where an admitted capability is required.

The program should reject the misuse at compile/construction/boundary time, not discover it deep in policy code.

Then ensure adapters can still faithfully serialize/deserialize the external primitive form.

Invariant:

> Domain identity survives every boundary where confusing it would change behavior.

### Done When

The program no longer relies on parameter names, comments, or human caution to distinguish values reality considers different.

And equally important: no new wrapper exists whose only achievement is making the type list longer.

### Nudge

Representation answers “what bits are these?”

Domain type answers “what fact do these bits mean?”

Introduce the second only where the program actually needs to know the difference — and then make the difference impossible to forget.
