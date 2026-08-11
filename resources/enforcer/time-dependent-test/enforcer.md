# time-dependent-test — Enforcer

## Definition
A test is time-dependent when its verdict depends on the real clock, elapsed wall time, time zone, or scheduler timing rather than on explicit temporal facts under test control.

## Governing Principle
Time is an input even when an API hides it. Real clocks make test premises move while the test is running: midnight, DST, machine load, and scheduling alter the scenario without changing source. Deterministic temporal tests freeze the relevant instant or advance a controlled clock, separating domain rules about time from accidents of when the suite happened to execute.

## Trigger When
Trigger when tests call the real current time, wait for wall-clock duration, rely on local timezone defaults, or assert completion within fragile timing windows.

## Do Not Trigger When
Do not trigger for a deliberately narrow real-clock integration smoke whose purpose is to verify clock wiring itself and whose tolerance is stable and non-semantic.

## Distinguish From
time-source-in-logic is production policy reading ambient time. sleep-based-synchronization uses delay as a causal signal. This rule concerns nondeterministic temporal premises in verification.

## Decision Procedure
Name the temporal facts the scenario requires—instant, duration, zone, deadline—and supply them explicitly through a fake/manual clock or fixed values.

## Nudge
Tests should choose time, not discover it. Inject or control the relevant temporal facts so the same scenario means the same thing whenever it runs.
