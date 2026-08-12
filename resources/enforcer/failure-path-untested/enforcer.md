# failure-path-untested — Enforcer

A failure path is untested when the code contains policy for what happens under failure, cancellation, rollback, malformed input, conflict, timeout, retry, or recovery — but no test has ever forced the condition that gives that policy meaning.

The dangerous illusion is readability. Failure code often *looks* obviously correct:

```text
catch
  release permit
  rollback reservation
  return error
```

But these branches are where ownership, partial effects, cleanup ordering, idempotency, stale state, and secondary failure interact. They are often the least exercised code in production until the moment correctness matters most.

Fire this rule when a change adds or materially changes failure semantics and no test deliberately produces the real precondition. Examples:

- new rollback branch never runs under test;
- cancellation cleanup is asserted only by code inspection;
- retry logic is tested by calling the retry helper directly, not by causing the owning operation to fail;
- malformed provider/wire input has a decoder branch but no malformed fixture reaches it;
- resource cleanup after partial initialization is never exercised;
- conflict/CAS rejection path exists but all tests serialize writers;
- recovery branch is “covered” only by manually constructing post-failure internal state;
- error mapping is tested without the external/inner failure that production maps.

Do not fire when an existing test already induces the exact failure through the same owning boundary and observes the same externally relevant semantics. Do not demand a bespoke test for unreachable dead code; delete the dead branch. Property/exhaustive tests may already provide sufficient failure evidence if they genuinely generate the condition and assert cleanup/state, not merely lines executed.

This differs from `missing-regression-test`: that rule starts from a **known defect that already escaped or was observed** and asks whether the repository preserved executable memory of it. `failure-path-untested` applies even before any incident: newly important failure policy has never been exercised.

It also differs from `coverage-theater`. A failure line can show as covered because a broad test passed through it, yet the test may never assert the guarantee that matters. The question is not “did the branch execute?” but:

> Did a test deliberately create this failure and prove the required result, cleanup, state preservation, and forbidden side effects?

A useful failure-test specification has four parts:

```text
induce: what exactly fails?
observe: what result/state must follow?
cleanup: what owned resources/effects must be discharged?
forbid: what must not happen despite the failure?
```

The “forbid” column is often where the real contract lives: no duplicate charge, no stale publish, no leaked permit, no state advance, no second retry, no swallowed error.

> Failure handling is executable policy. Code that has never been forced to fail is not yet evidence that failure is handled.