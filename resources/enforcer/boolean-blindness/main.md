# boolean-blindness — Main

## What To Do Now
Replace booleans that carry named choices with a type whose cases are the actual choices.

For flag clusters, model the **legal state space** directly instead of preserving a Cartesian product and teaching every consumer which combinations are fictional.

Leave genuine predicates as booleans. The goal is semantic precision, not boolean prohibition.

## Why This Matters
Boolean-heavy APIs make meaning cheap to encode and expensive to recover.

The writer knows what `true` means because the signature is fresh in mind. The reader encounters `configure(false, true, false)` months later and must reconstruct the vocabulary from somewhere else. Tooling can display parameter names, but the program itself still accepts swapped or contradictory literals.

Flag clusters are worse. Every added boolean doubles the representable state space whether the domain doubled or not. Invalid combinations then leak into persistence, tests, branching, migration, and incident diagnosis. The system starts carrying defensive checks for worlds no user could ever legitimately create.

A named sum/enum/capability set makes the valid alternatives visible and gives future changes an explicit place to land.

## Repair Strategy
Start from the domain alternatives, not from existing flags:

1. enumerate meaningful modes/states/actions;
2. decide whether they are mutually exclusive, independent, or hierarchical;
3. use a sum/enum for mutually exclusive alternatives;
4. attach state-specific data to the case that owns it;
5. use a capability/set model only when combinations are genuinely independent and meaningful;
6. translate wire/storage booleans at the boundary if an external format requires them;
7. remove old boolean overloads so callers cannot bypass the named model;
8. use exhaustive matching so future alternatives create visible obligations.

For a single parameter, replace `bool` with a named two-case type when the choice is policy-bearing and call-site meaning matters. Two cases are still worthwhile when the names carry real domain vocabulary.

## Decision Branches
- **Mutually exclusive lifecycle states:** use one closed state type, not many `isX` flags.
- **Policy option with two named alternatives:** use a two-case enum/union if `true/false` would hide intent.
- **Independent capabilities:** a set/bitset may be appropriate if every allowed combination is meaningful; give the capabilities names.
- **Simple observation/predicate:** keep `bool`; `isEmpty` does not need `Empty | NonEmpty` unless the distinction later carries data/behavior.
- **External protocol is boolean:** decode to named domain choice at ingress and encode back at egress.
- **Existing flags have historical invalid combinations in storage:** define a migration/validation policy; do not silently reinterpret contradictory records.

## Common Wrong Fixes
- Rename `flag` to `isSpecial` and keep literal calls everywhere.
- Add comments documenting which combinations are illegal while the type continues accepting them.
- Add another boolean for every new mode, multiplying contradictions.
- Replace booleans with free-form strings such as `"read" | "write"` without a closed type. That trades boolean blindness for stringly typing.
- Keep legacy boolean overloads “for convenience,” guaranteeing new code can still bypass the repair.
- Replace truly independent binary facts with one giant enum containing every combination. That can make the model worse; use named capabilities when independence is real.

## Verification
Prove both readability and state-space closure:

- search call sites for unexplained boolean literals in policy/mode positions;
- attempt to construct formerly contradictory flag combinations;
- ensure exhaustive matches force handling when a new named case is added;
- verify wire/storage adapters preserve external compatibility without leaking booleans inward;
- confirm genuine predicates remain simple rather than being wrapped ceremonially.

Invariant:

> The set of representable policy/state choices matches the set of choices the domain actually names.

## Done When
Call sites say what they mean without editor hints, and the type no longer admits combinations whose only semantics are “this should never happen.”
