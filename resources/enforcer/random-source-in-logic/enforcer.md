# random-source-in-logic — Enforcer

## Definition
Randomness is hidden in logic when a domain decision draws entropy internally, making the decision depend on an input absent from its signature and difficult to replay.

## Governing Principle
Randomness is still input. A function that samples it internally only conceals the input channel, so identical visible state can lead to different events with no reproducible explanation. For simulations, allocation, games, sampling, and tie-breaking, replayability requires preserving either the generated choice or the seed/source from which the choice is deterministically derived.

## Trigger When
Trigger when core policy directly calls random/UUID/entropy APIs to decide domain outcomes without receiving the random choice/source explicitly.

## Do Not Trigger When
Do not trigger for cryptographic nonce/key generation or UI-only jitter confined to an adapter where replay of business policy is irrelevant.

## Distinguish From
time-source-in-logic hides the clock. impure-core is the broader architecture smell. This rule specifically hides entropy as an undeclared decision input.

## Decision Procedure
Ask whether replaying the same command/state should reproduce the same domain decision. If yes, inject a deterministic random source/seed or record the sampled value as part of the event/input.

## Nudge
Entropy is an input, not magic. Make it explicit or record its result so a past decision can be reproduced and explained.
