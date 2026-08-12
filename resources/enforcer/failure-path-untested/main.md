# failure-path-untested — Main

Force the failure at the boundary that owns it.

Do not unit-test the catch block in isolation unless that catch block itself is the supported boundary. Arrange the smallest deterministic condition that makes production choose the failure path, then assert the externally meaningful contract.

For each important failure, write the test around four questions:

```text
What failed?
What result/state must now be true?
What cleanup/compensation must happen?
What side effects are forbidden?
```

Examples:

- storage commit fails → state stays old, no publication, resource released;
- provider returns malformed response → typed rejection, no retry under wrong identity;
- cancellation wins → child effect stops, permit returns, no later mutation;
- CAS conflict → stale writer rejected, accepted update preserved;
- second acquisition fails → first resource is still released;
- retry exhausts → final error surfaces, no extra hidden attempt.

Use deterministic fault injection where possible: a fake/store port that fails exactly on operation N, a controllable cancellation point, a provider double that returns a specific malformed payload, a barrier that forces conflict. Avoid timing luck as the mechanism that “sometimes makes failure happen.”

Common fake repairs:

- call an internal recovery helper directly without proving production routes the real failure there;
- assert only that an exception/error was returned while cleanup/state invariants remain unobserved;
- mock the failing dependency so heavily that ownership/transaction boundaries are bypassed;
- assert a private `rollbackCalled` flag instead of the public/durable consequence that rollback protects;
- use coverage numbers as proof the failure semantics were tested;
- only test failure before any partial work, leaving the dangerous mid-operation failure unexamined;
- make every dependency fail at once, producing an unrealistic scenario that cannot localize which guarantee broke.

Test secondary failure where it matters. Cleanup itself can fail; compensation may be unavailable; cancellation can race completion. You do not need combinatorial catastrophe theater, but if the system has a defined policy for such cases, force the meaningful ones deliberately.

Mutation verification is especially valuable. Reintroduce a plausible failure bug: skip cleanup, advance state despite commit failure, swallow error, perform one extra retry, publish before rollback. The test should turn red for the reason the production guarantee would be violated.

If a failure branch is genuinely impossible by construction, delete it or encode that impossibility more strongly instead of maintaining an untestable defensive myth.

You are done when important failure semantics are not merely readable but executable: the suite has witnessed the system fail and proved what it does — and refuses to do — next.

> The happy path proves the system can work. Failure tests prove it knows how to stop being lucky.