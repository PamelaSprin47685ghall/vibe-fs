# impure-core — Lesson

## Opinion

### Definition

A core is impure when business decisions reach outward for time, randomness, storage, network, environment, or mutable global state instead of receiving the facts they need as explicit inputs. The root-cause is that policy fetches undeclared external facts inside the decision, so the signature understates the true inputs and identical calls are not identical results.

### Governing Principle

A policy function is easiest to trust when its result is a mathematical consequence of visible inputs. Hidden effects enlarge the input set without enlarging the signature: the same call can mean different things tomorrow, on another machine, or after another test. Replay, audit, and local reasoning then fail for the same reason—the function has dependencies it refuses to name.

### Trigger When

Trigger when domain logic directly reads clocks, random generators, databases, networks, environment variables, files, or mutable globals while deciding business outcomes.

### Do Not Trigger When

- Do not trigger at adapters/shells whose explicit responsibility is to observe the external world and supply values to the core.
- Do not trigger for logging or metrics that cannot change the decision (observation of an already-computed result).
- Do not trigger when required facts arrive as explicit data inputs and the decision is deterministic given those values.

### Distinguish From

mixed-side-effect-boundaries mixes several effects. time-source-in-logic and random-source-in-logic are specific hidden inputs. This rule is the architectural principle that policy should not own observation of the outside world. Tie-break: if the core fetches any external fact to decide, use this rule; if the issue is only a hidden clock or RNG, the specific sibling may be the tighter fit.

### Decision Procedure

Write the decision as `state × command × observed facts → result`. Any required fact currently fetched inside the function belongs in the input or in a narrow injected port at the shell boundary.

### Examples

- positive: Pricing policy reads `DateTime.UtcNow` and a database row inside the domain function.
- near-miss: The shell loads the clock and row, then calls a pure function with those values.
- counterexample: The core is `state × command × facts → result` with no I/O; the adapter enacts the result.

## Advice

### What To Do Now

Move external observation and side effects to the shell. Pass time, random choices, loaded data, and other required facts explicitly into pure decision functions. The shell/adapter is who owns observation and enactment; the core is who owns policy as a function of explicit inputs.

### Why This Matters

Hidden dependencies make behavior contingent on an environment the signature does not reveal. Tests need mocks, replay changes with the clock, and auditing cannot reconstruct why a decision occurred. Purity is not aesthetic restraint; it is preservation of causal evidence.

### Repair Strategy

Separate “observe the world” from “decide what it means.” Let adapters obtain current facts, let the core compute next state/events or typed failure, then let the shell execute effects.

### Decision Branches

- If the function decides business outcomes, it must take observed facts as inputs and return a value.
- If the function’s job is to observe or enact, keep it in the shell and do not let it own policy.

### Common Wrong Fixes

- Do not hide effects behind a globally injected service locator or mock-heavy interface while policy still decides when to perform them. Dependency inversion without causal separation leaves the core impure.
- Do not pass an I/O port into the core “for testability” and still let policy fetch.
- Do not freeze the clock only in tests while production policy still reads the environment.

### Verification

Call the core twice with identical explicit inputs and require identical outputs without starting infrastructure, touching the clock, or mutating global state. That equality is the purity invariant.

### Done When

Business decisions are replayable values of explicit inputs, and the external world appears only at narrow shell boundaries that gather facts or enact results.

### Nudge

Make policy a consequence, not an observation. Move effects outward and pass the core every fact it needs explicitly so identical inputs imply identical decisions.
