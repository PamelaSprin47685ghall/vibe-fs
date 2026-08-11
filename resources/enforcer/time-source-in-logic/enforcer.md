# time-source-in-logic — Enforcer

## Definition
Time is hidden in logic when domain policy reads the current clock internally instead of receiving the relevant instant or clock as an explicit dependency.

## Governing Principle
The present moment is an input, not a universal constant. A decision that calls `now()` internally changes meaning between two otherwise identical invocations, making replay and causal explanation impossible from visible data alone. Moving the clock to the boundary restores referential clarity: the shell observes time; the policy reasons about a supplied temporal fact.

## Trigger When
Trigger when core rules directly read system time to decide expiry, eligibility, ordering, deadlines, windows, age, or lifecycle outcomes.

## Do Not Trigger When
Do not trigger when clock reads are confined to adapters or orchestration that pass an explicit instant into deterministic domain functions.

## Distinguish From
random-source-in-logic hides entropy. time-dependent-test is the verification symptom. impure-core is the broader principle. This rule specifically treats ambient time as an undeclared policy input.

## Decision Procedure
Rewrite the decision signature conceptually with `now` among its inputs. If doing so makes the real dependency clearer and enables replay, move clock observation outward.

## Nudge
The clock reports a fact; policy interprets it. Read time at the boundary and pass the relevant instant inward so identical inputs produce identical decisions.
