# time-dependent-test — Enforcer

## Definition
A test is time-dependent when its verdict depends on the real clock, elapsed wall time, time zone, or scheduler timing rather than on explicit temporal facts under test control. The root-cause is that the host clock is an undeclared input to the verdict, so premises move while the scenario is supposed to be fixed.

## Governing Principle
Time is an input even when an API hides it. Real clocks make test premises move while the test is running: midnight, DST, machine load, and scheduling alter the scenario without changing source. Deterministic temporal tests freeze the relevant instant or advance a controlled clock, separating domain rules about time from accidents of when the suite happened to execute.

## Trigger When
Trigger when tests call the real current time, wait for wall-clock duration, rely on local timezone defaults, or assert completion within fragile timing windows.

## Do Not Trigger When
- A deliberately narrow real-clock integration smoke whose purpose is to verify clock wiring itself and whose tolerance is stable and non-semantic.
- Tests that inject a frozen instant or manual clock even when production later reads a real clock at the adapter.
- Performance or load benchmarks whose purpose is wall-time measurement, not a functional pass/fail of domain rules.
- Causal waits on an explicit readiness signal (not elapsed milliseconds) used only as synchronization.

## Distinguish From
`time-source-in-logic` is production policy reading ambient time. `sleep-based-synchronization` uses delay as a causal signal. Tie-break: if production core reads `now()` as undeclared policy input, use `time-source-in-logic`; if a test’s verdict depends on the real clock, use this rule.

## Decision Procedure
Name the temporal facts the scenario requires—instant, duration, zone, deadline—and supply them explicitly through a fake/manual clock or fixed values.

## Examples
- positive: a billing test calls `Date.now()` and fails or passes depending on midnight and the runner’s zone.
- near-miss: a one-line smoke that the process can read the system clock, with a generous non-semantic timeout.
- counterexample: domain code calling `now()` internally to decide expiry is `time-source-in-logic`.

## Nudge
Tests should choose time, not discover it. Inject or control the relevant temporal facts so the same scenario means the same thing whenever it runs.
