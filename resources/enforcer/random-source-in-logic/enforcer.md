# random-source-in-logic — Enforcer

## Definition
Randomness is hidden in logic when a domain decision draws entropy internally, making the decision depend on an input absent from its signature and difficult to replay. The root-cause is that entropy is sampled inside policy instead of being an explicit input, so replay cannot reconstruct the decision from visible data.

## Governing Principle
Randomness is still input. A function that samples it internally only conceals the input channel, so identical visible state can lead to different events with no reproducible explanation. For simulations, allocation, games, sampling, and tie-breaking, replayability requires preserving either the generated choice or the seed/source from which the choice is deterministically derived.

## Trigger When
Trigger when core policy directly calls random/UUID/entropy APIs to decide domain outcomes without receiving the random choice/source explicitly.

## Do Not Trigger When
- Cryptographic nonce or key generation lives in a security adapter where domain replay does not own the entropy.
- UI-only jitter is confined to an adapter and does not decide business events.
- The domain function already receives a seed, RNG, or sampled value as an explicit argument and records it when replay matters.
- The call is a test helper that injects a controlled source into production’s explicit entropy port.

## Distinguish From
time-source-in-logic hides the clock. impure-core is the broader architecture smell. This rule specifically hides entropy as an undeclared decision input. Tie-break: fire here when the hidden input is randomness; fire time-source-in-logic when it is the clock; fire impure-core when several ambient effects are mixed, not only entropy.

## Decision Procedure
Ask whether replaying the same command/state should reproduce the same domain decision. If yes, inject a deterministic random source/seed or record the sampled value as part of the event/input.

## Examples
- positive: a billing policy calls `uuid4()` inside the fold to choose a winner among tied invoices, so the same command cannot be replayed.
- near-miss: a TLS adapter generates a nonce for a handshake and never feeds that nonce into domain events.
- counterexample: the shell samples a dice roll and passes the integer into a pure `apply(command, roll)` function that records `roll` on the event.

## Nudge
Entropy is an input, not magic. Make it explicit or record its result so a past decision can be reproduced and explained.
