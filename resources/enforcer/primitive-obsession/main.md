# primitive-obsession — Main

## What To Do Now
Move the semantic distinction into the type boundary that already owns the value.

Create a distinct domain type, validated constructor, unit-bearing value, or similarly strong representation that rejects the dangerous sibling substitution. Migrate inward-facing callers to that type and confine primitive conversion to real ingress/egress adapters.

Do not start a newtype census. Repair the boundary where erased identity can cause a real category error.

## Why This Matters
Primitive obsession makes correctness depend on memory.

Humans read `accountId`, `orderId`, and `sessionId` and see three concepts. A type checker that sees three strings sees one concept repeated three times. That gap is exactly where category mistakes survive review: the value looks valid, serialization succeeds, logs look ordinary, and failure appears only when the wrong object is touched later.

Units are even more treacherous because wrong values can remain numerically plausible. Milliseconds passed as seconds do not necessarily crash; they create “mysterious” latency. Cents interpreted as dollars do not violate arithmetic; they violate meaning.

A good domain type makes one expensive distinction cheap forever.

## Repair Strategy
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

## Decision Branches
- **Sibling identifiers can be confused:** give each nominal identity and construct them at ingress.
- **Same number, different unit:** encode unit or use distinct value types; make conversions explicit and named.
- **Validated vs raw input:** return a strong validated type and do not immediately erase it back to the primitive.
- **Trusted/capability-bearing value:** separate admitted capability from untrusted token text; construction should correspond to the authority check.
- **Value is truly generic at this boundary:** leave it primitive. Strong typing should follow semantic distinctions, not fashion.
- **Language cannot enforce nominal distinction cheaply:** use the strongest available constructor/module boundary and tests; do not pretend an alias rejects substitutions when it does not.

## Common Wrong Fixes
- Rename variables while keeping every API primitive-typed. Better names help readers, not substitution safety.
- Create one-field wrappers and immediately expose `.value` everywhere so all domain APIs still consume primitives.
- Add runtime `assert(isUserId(x))` at every caller instead of establishing identity once.
- Build a “universal ID” wrapper carrying a string plus a `kind` field. That often recreates the same category error one level later.
- Wrap every string/number in the repository regardless of risk. Ceremony is not semantic precision.
- Hide implicit unit conversions inside overloaded operators so the type looks strong while meaning can still change silently.

## Verification
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

## Done When
The program no longer relies on parameter names, comments, or human caution to distinguish values reality considers different.

And equally important: no new wrapper exists whose only achievement is making the type list longer.
