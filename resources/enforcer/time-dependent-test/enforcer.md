# time-dependent-test — Enforcer

A test is time-dependent when the host clock contributes an undeclared premise to the verdict.

The source may be `Date.now()`, `UtcNow`, local timezone, DST rules, elapsed wall time, scheduler delay, current date, or a real deadline. The common defect is that the scenario is supposed to be fixed, but part of its input keeps moving because the test discovers time from the machine instead of choosing time explicitly.

This creates failures that are not really “flaky timing” in one generic sense. They may depend on:

- crossing midnight during the test;
- month/year boundary;
- daylight-saving transition;
- CI running in a different zone/locale;
- leap-day/calendar edge;
- scheduler pause moving an operation past a deadline;
- slow machine changing whether “within N ms” passes;
- test order changing how much real time elapsed before the assertion.

Fire this rule when functional/domain verdicts depend on ambient clock or wall-time windows that are not themselves the feature under test.

Do not fire for a deliberately narrow clock-adapter smoke whose purpose is to prove that production can read the system clock. Do not fire for performance/load benchmarks whose output is explicitly wall time. Do not confuse causal synchronization with time dependence: awaiting a real completion signal under a timeout can be deterministic in meaning if timeout only bounds failure.

Also distinguish from `time-source-in-logic`: that rule attacks production policy that reads ambient time instead of receiving temporal facts explicitly. This rule attacks **test premises**. They often point to the same design seam, but either can exist alone.

A decisive question is:

> If this exact test started one hour later, in another time zone, on a slower machine, would it still represent the same scenario?

If not, time is an undeclared input.

The repair is to make temporal facts ordinary data: fixed instant, explicit zone, duration, deadline, monotonic tick, manually advanced clock. The test should own these values.

Real time may still appear at one thin adapter boundary. Test that adapter separately with a tolerant smoke if needed, then feed deterministic values inward.

Do not replace one ambient source with another global monkeypatch and call it solved. A process-wide mocked clock can itself create order dependence if tests mutate it without scope.

> Tests should choose the time of the story. They should not ask the machine what time it happened to be when the story was read.