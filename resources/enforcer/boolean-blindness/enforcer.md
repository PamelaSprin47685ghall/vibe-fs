# boolean-blindness — Enforcer

## Definition
Boolean blindness appears when `true` and `false` are asked to carry domain distinctions whose names disappear at the call site or whose combinations include impossible worlds.

## Governing Principle
A type is a set of states the program permits. Replacing a domain distinction with a boolean enlarges or obscures that set while erasing vocabulary. Two independent flags already admit four combinations; three admit eight. If the domain has only three meaningful states, the remaining five are not flexibility—they are fabricated universes the rest of the program must defensively reject.

## Trigger When
Trigger when booleans encode modes, permissions, lifecycle states, policy choices, or multiple independent meanings, especially when callers pass literals or combinations require comments to explain.

## Do Not Trigger When
Do not trigger for a genuinely binary predicate whose two meanings are obvious from the type and call site, such as `isEmpty` as a returned observation.

## Distinguish From
illegal-state-representable is the general state-space defect. primitive-obsession concerns erased domain identity at boundaries. This rule focuses on booleans as a particularly lossy representation of choice.

## Decision Procedure
Name every semantic value currently represented by the flag product. If those values have names in the domain, represent those names directly and make impossible combinations unconstructable.

## Nudge
Do not encode a vocabulary as bits. Replace boolean products with named cases whose state space is exactly the domain’s state space.
