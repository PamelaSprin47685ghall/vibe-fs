# failure-path-untested — Lesson

## Opinion

### Definition

A failure path is untested when the code contains policy for what happens under failure, cancellation, rollback, malformed input, conflict, timeout, retry, or recovery — but no test has ever forced the condition that gives that policy meaning.

### Governing Principle

The dangerous illusion is readability. Failure code often *looks* obviously correct:

```text
catch
  release permit
  rollback reservation
  return error
```

But these branches are where ownership, partial effects, cleanup ordering, idempotency, stale state, and secondary failure interact. They are often the least exercised code in production until the moment correctness matters most.

### Trigger When

Fire this rule when a change adds or materially changes failure semantics and no test deliberately produces the real precondition. Examples:

- new rollback branch never runs under test;
- cancellation cleanup is asserted only by code inspection;
- retry logic is tested by calling the retry helper directly, not by causing the owning operation to fail;
- malformed provider/wire input has a decoder branch but no malformed fixture reaches it;
- resource cleanup after partial initialization is never exercised;
- conflict/CAS rejection path exists but all tests serialize writers;
- recovery branch is “covered” only by manually constructing post-failure internal state;
- error mapping is tested without the external/inner failure that production maps.

### Do Not Trigger When

Do not fire when an existing test already induces the exact failure through the same owning boundary and observes the same externally relevant semantics. Do not demand a bespoke test for unreachable dead code; delete the dead branch. Property/exhaustive tests may already provide sufficient failure evidence if they genuinely generate the condition and assert cleanup/state, not merely lines executed.

### Distinguish From

This differs from `missing-regression-test`: that rule starts from a **known defect that already escaped or was observed** and asks whether the repository preserved executable memory of it. `failure-path-untested` applies even before any incident: newly important failure policy has never been exercised.

It also differs from `coverage-theater`. A failure line can show as covered because a broad test passed through it, yet the test may never assert the guarantee that matters. The question is not “did the branch execute?” but:

> Did a test deliberately create this failure and prove the required result, cleanup, state preservation, and forbidden side effects?

### Decision Procedure

A useful failure-test specification has four parts:

```text
induce: what exactly fails?
observe: what result/state must follow?
cleanup: what owned resources/effects must be discharged?
forbid: what must not happen despite the failure?
```

The “forbid” column is often where the real contract lives: no duplicate charge, no stale publish, no leaked permit, no state advance, no second retry, no swallowed error.

### Examples


## Advice

### What To Do Now

Force the failure at the boundary that owns it.

Do not unit-test the catch block in isolation unless that catch block itself is the supported boundary. Arrange the smallest deterministic condition that makes production choose the failure path, then assert the externally meaningful contract.

### Why This Matters

For each important failure, write the test around four questions:

```text
What failed?
What result/state must now be true?
What cleanup/compensation must happen?
What side effects are forbidden?
```

### Repair Strategy

Use deterministic fault injection where possible: a fake/store port that fails exactly on operation N, a controllable cancellation point, a provider double that returns a specific malformed payload, a barrier that forces conflict. Avoid timing luck as the mechanism that “sometimes makes failure happen.”

### Decision Branches

Test secondary failure where it matters. Cleanup itself can fail; compensation may be unavailable; cancellation can race completion. You do not need combinatorial catastrophe theater, but if the system has a defined policy for such cases, force the meaningful ones deliberately.

If a failure branch is genuinely impossible by construction, delete it or encode that impossibility more strongly instead of maintaining an untestable defensive myth.

### Common Wrong Fixes

Common fake repairs:

- call an internal recovery helper directly without proving production routes the real failure there;
- assert only that an exception/error was returned while cleanup/state invariants remain unobserved;
- mock the failing dependency so heavily that ownership/transaction boundaries are bypassed;
- assert a private `rollbackCalled` flag instead of the public/durable consequence that rollback protects;
- use coverage numbers as proof the failure semantics were tested;
- only test failure before any partial work, leaving the dangerous mid-operation failure unexamined;
- make every dependency fail at once, producing an unrealistic scenario that cannot localize which guarantee broke.

### Verification

Examples:

- storage commit fails → state stays old, no publication, resource released;
- provider returns malformed response → typed rejection, no retry under wrong identity;
- cancellation wins → child effect stops, permit returns, no later mutation;
- CAS conflict → stale writer rejected, accepted update preserved;
- second acquisition fails → first resource is still released;
- retry exhausts → final error surfaces, no extra hidden attempt.

Mutation verification is especially valuable. Reintroduce a plausible failure bug: skip cleanup, advance state despite commit failure, swallow error, perform one extra retry, publish before rollback. The test should turn red for the reason the production guarantee would be violated.

### Done When

You are done when important failure semantics are not merely readable but executable: the suite has witnessed the system fail and proved what it does — and refuses to do — next.

> The happy path proves the system can work. Failure tests prove it knows how to stop being lucky.

### Nudge

> Failure handling is executable policy. Code that has never been forced to fail is not yet evidence that failure is handled.
