# boolean-blindness — Enforcer

## Definition
Boolean blindness appears when `true` and `false` are asked to carry domain distinctions whose names disappear at the call site or whose combinations include impossible worlds. The root-cause is that true/false (or a product of flags) is asked to carry named domain modes, erasing vocabulary at the call site and making illegal combinations constructable.

## Governing Principle
A type is a set of states the program permits. Replacing a domain distinction with a boolean enlarges or obscures that set while erasing vocabulary. Two independent flags already admit four combinations; three admit eight. If the domain has only three meaningful states, the remaining five are not flexibility—they are fabricated universes the rest of the program must defensively reject.

## Trigger When
Trigger when booleans encode modes, permissions, lifecycle states, policy choices, or multiple independent meanings, especially when callers pass literals or combinations require comments to explain.

## Do Not Trigger When
- The value is a genuinely binary predicate whose two meanings are obvious from the type and call site, such as `isEmpty` as a returned observation.
- A single flag is a true yes/no fact with no third mode and no pairing with sibling flags that create illegal products.
- The boolean is a wire or storage bit already constrained by a named enum at the domain boundary.
- Test assertions comparing a boolean observation to an expected named outcome do not themselves encode a mode.

## Distinguish From
`illegal-state-representable` is the general state-space defect. `primitive-obsession` concerns erased domain identity at boundaries. This rule focuses on booleans as a particularly lossy representation of choice. Tie-break: if the lost vocabulary is specifically true/false (or a product of flags), this rule owns the case.

## Decision Procedure
Name every semantic value currently represented by the flag product. If those values have names in the domain, represent those names directly and make impossible combinations unconstructable.

## Examples
- positive: `open(true, false)` where the literals mean “write, not create,” and a third illegal pair remains constructable.
- near-miss: `isEmpty` returned from a collection, a binary observation whose call site names the predicate.
- counterexample: replace the flags with a named mode or distinct type whose cases are exactly the legal domain choices.

## Nudge
Do not encode a vocabulary as bits. Replace boolean products with named cases whose state space is exactly the domain’s state space.
