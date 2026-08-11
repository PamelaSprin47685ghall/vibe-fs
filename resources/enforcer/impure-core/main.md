# impure-core — Main

## What To Do Now
Move external observation and side effects to the shell. Pass time, random choices, loaded data, and other required facts explicitly into pure decision functions.

## Why This Matters
Hidden dependencies make behavior contingent on an environment the signature does not reveal. Tests need mocks, replay changes with the clock, and auditing cannot reconstruct why a decision occurred. Purity is not aesthetic restraint; it is preservation of causal evidence.

## Repair Strategy
Separate “observe the world” from “decide what it means.” Let adapters obtain current facts, let the core compute next state/events or typed failure, then let the shell execute effects.

## Decision Branches
- If the function decides business outcomes, it must take observed facts as inputs and return a value.
- If the function’s job is to observe or enact, keep it in the shell and do not let it own policy.

## Common Wrong Fixes
- Do not hide effects behind a globally injected service locator or mock-heavy interface while policy still decides when to perform them. Dependency inversion without causal separation leaves the core impure.
- Do not pass an I/O port into the core “for testability” and still let policy fetch.
- Do not freeze the clock only in tests while production policy still reads the environment.

## Verification
Call the core twice with identical explicit inputs and require identical outputs without starting infrastructure, touching the clock, or mutating global state. That equality is the purity invariant.

## Done When
Business decisions are replayable values of explicit inputs, and the external world appears only at narrow shell boundaries that gather facts or enact results.
