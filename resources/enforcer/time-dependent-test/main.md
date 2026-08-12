# time-dependent-test — Main

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

For calendar logic, use explicit zones and named edge instants: DST start/end, month boundary, leap day, midnight. The test should communicate *which temporal law is being examined* instead of hoping CI eventually runs near the interesting moment.

For timeout/cancellation logic, separate two concerns:

- domain/policy deadline → controlled clock or explicit deadline input;
- synchronization safety timeout in the test runner → real timeout may remain as a guard against hangs, but it must not define success semantics.

Common fake repairs:

- widen `within 100ms` to `within 5s`;
- sleep longer so a deadline is “surely crossed”;
- set CI timezone globally and assume local machines match;
- globally monkeypatch `Date.now` without restoring/scoping it, creating cross-test leakage;
- use frozen time for one layer while another dependency still reads the real clock;
- assert formatted date strings without fixing locale/zone;
- replace wall time with scheduler ordering and still assume elapsed duration.

Verification should intentionally move the real environment. Run under different host time zones, arbitrary wall-clock times, and slower scheduling where practical. The functional verdict must remain identical because all semantic temporal facts come from the fixture.

Then vary the **controlled** time inputs deliberately. The test should change only when the domain law says it should: just before/at/after expiry, DST transition, boundary instant, etc.

If production code itself reaches ambient `now()` deep inside policy, the test difficulty may be evidence for `time-source-in-logic`. Fixing the production seam can make both product design and test determinism better.

Keep one narrow integration smoke for the real clock adapter if needed. Its claim should be modest: “the adapter reads a plausible system time,” not “the billing/expiry domain is correct.”

You are done when the same scenario has the same meaning regardless of when/where CI happens to execute, and temporal behavior changes only when the test explicitly changes temporal data.

> A deterministic test does not remove time. It makes time part of the story instead of part of the weather.