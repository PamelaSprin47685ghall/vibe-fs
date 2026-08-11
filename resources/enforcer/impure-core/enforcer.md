# impure-core — Enforcer

## Definition
A core is impure when business decisions reach outward for time, randomness, storage, network, environment, or mutable global state instead of receiving the facts they need as explicit inputs.

## Governing Principle
A policy function is easiest to trust when its result is a mathematical consequence of visible inputs. Hidden effects enlarge the input set without enlarging the signature: the same call can mean different things tomorrow, on another machine, or after another test. Replay, audit, and local reasoning then fail for the same reason—the function has dependencies it refuses to name.

## Trigger When
Trigger when domain logic directly reads clocks, random generators, databases, networks, environment variables, files, or mutable globals while deciding business outcomes.

## Do Not Trigger When
Do not trigger at adapters/shells whose explicit responsibility is to observe the external world and supply values to the core.

## Distinguish From
mixed-side-effect-boundaries mixes several effects. time-source-in-logic and random-source-in-logic are specific hidden inputs. This rule is the architectural principle that policy should not own observation of the outside world.

## Decision Procedure
Write the decision as `state × command × observed facts → result`. Any required fact currently fetched inside the function belongs in the input or in a narrow injected port at the shell boundary.

## Nudge
Make policy a consequence, not an observation. Move effects outward and pass the core every fact it needs explicitly so identical inputs imply identical decisions.
