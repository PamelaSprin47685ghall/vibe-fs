# time-dependent-test — Lesson

## Opinion

### Definition

A test is time-dependent when the host clock contributes an undeclared premise to the verdict.

The source may be `Date.now()`, `UtcNow`, local timezone, DST rules, elapsed wall time, scheduler delay, current date, or a real deadline. The common defect is that the scenario is supposed to be fixed, but part of its input keeps moving because the test discovers time from the machine instead of choosing time explicitly.

This creates failures that are not really "flaky timing" in one generic sense. They may depend on:

- crossing midnight during the test;
- month/year boundary;
- daylight-saving transition;
- CI running in a different zone/locale;
- leap-day/calendar edge;
- scheduler pause moving an operation past a deadline;
- slow machine changing whether "within N ms" passes;
- test order changing how much real time elapsed before the assertion.

### Governing Principle

The repair is to make temporal facts ordinary data: fixed instant, explicit zone, duration, deadline, monotonic tick, manually advanced clock. The test should own these values.

Real time may still appear at one thin adapter boundary. Test that adapter separately with a tolerant smoke if needed, then feed deterministic values inward.

Do not replace one ambient source with another global monkeypatch and call it solved. A process-wide mocked clock can itself create order dependence if tests mutate it without scope.

### Trigger When

Fire this rule when functional/domain verdicts depend on ambient clock or wall-time windows that are not themselves the feature under test.

### Do Not Trigger When

Do not fire for a deliberately narrow clock-adapter smoke whose purpose is to prove that production can read the system clock. Do not fire for performance/load benchmarks whose output is explicitly wall time. Do not confuse causal synchronization with time dependence: awaiting a real completion signal under a timeout can be deterministic in meaning if timeout only bounds failure.

### Distinguish From

Also distinguish from `time-source-in-logic`: that rule attacks production policy that reads ambient time instead of receiving temporal facts explicitly. This rule attacks **test premises**. They often point to the same design seam, but either can exist alone.

### Decision Procedure

A decisive question is:

> If this exact test started one hour later, in another time zone, on a slower machine, would it still represent the same scenario?

If not, time is an undeclared input.

### Examples

Crossing midnight during the test. Month/year boundary. Daylight-saving transition. CI running in a different zone/locale. Leap-day/calendar edge. Scheduler pause moving an operation past a deadline.

## Advice

### What To Do Now

Make time an explicit test input.

Identify the temporal facts the scenario actually needs — instant, date, zone, duration, deadline, monotonic elapsed value — and provide them directly or through a scoped/manual clock controlled by the fixture.

For expiration/deadline logic, prefer:

```text
clock = ManualClock(t0)
run scenario
clock.advance(delta)
assert result
```

rather than sleeping until real time crosses the threshold.

### Why This Matters

For calendar logic, use explicit zones and named edge instants: DST start/end, month boundary, leap day, midnight. The test should communicate *which temporal law is being examined* instead of hoping CI eventually runs near the interesting moment.

For timeout/cancellation logic, separate two concerns:

- domain/policy deadline → controlled clock or explicit deadline input;
- synchronization safety timeout in the test runner → real timeout may remain as a guard against hangs, but it must not define success semantics.

### Repair Strategy

If production code itself reaches ambient `now()` deep inside policy, the test difficulty may be evidence for `time-source-in-logic`. Fixing the production seam can make both product design and test determinism better.

### Decision Branches

Then vary the **controlled** time inputs deliberately. The test should change only when the domain law says it should: just before/at/after expiry, DST transition, boundary instant, etc.

Keep one narrow integration smoke for the real clock adapter if needed. Its claim should be modest: "the adapter reads a plausible system time," not "the billing/expiry domain is correct."

### Common Wrong Fixes

Common fake repairs:

- widen `within 100ms` to `within 5s`;
- sleep longer so a deadline is "surely crossed";
- set CI timezone globally and assume local machines match;
- globally monkeypatch `Date.now` without restoring/scoping it, creating cross-test leakage;
- use frozen time for one layer while another dependency still reads the real clock;
- assert formatted date strings without fixing locale/zone;
- replace wall time with scheduler ordering and still assume elapsed duration.

### Verification

Verification should intentionally move the real environment. Run under different host time zones, arbitrary wall-clock times, and slower scheduling where practical. The functional verdict must remain identical because all semantic temporal facts come from the fixture.

### Done When

You are done when the same scenario has the same meaning regardless of when/where CI happens to execute, and temporal behavior changes only when the test explicitly changes temporal data.

> A deterministic test does not remove time. It makes time part of the story instead of part of the weather.

### Nudge

> Tests should choose the time of the story. They should not ask the machine what time it happened to be when the story was read.
