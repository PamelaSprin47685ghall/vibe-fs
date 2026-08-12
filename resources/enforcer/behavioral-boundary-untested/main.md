# behavioral-boundary-untested — Main

Add one test through the supported entrance that owns the promise.

Start from the caller's sentence, not from the helper you want to cover:

```text
When caller supplies X through supported boundary B,
caller may observe Y and must not observe Z.
```

Then build the smallest fixture that crosses B using production decoding/wiring/permission/default logic relevant to that promise. Assert the caller-visible result, durable state, or external effect — not private helper choreography.

Keep lower-level tests when they buy diagnosis or exhaust a pure law. They are useful once the boundary theorem exists. The repair is not “delete unit tests”; it is “stop asking unit tests to certify integration they never exercise.”

Common fake repairs:

- exporting a private helper solely so the test can call it;
- reproducing the production wiring inside the test fixture, thereby allowing both fixture and production to be wrong differently;
- constructing post-decoder objects directly when the bug could live in decoding/defaults;
- mocking the permission/identity layer away when that layer is part of the public behavior;
- adding a giant full-stack E2E when a narrow real boundary test would be faster and more precise;
- asserting only “request did not throw,” while wrong result/default/identity would still pass.

Verification should prove the test owns a real boundary risk. Deliberately introduce one plausible composition defect — swap a field, remove a default, bypass an adapter, miswire a dependency, change an ID — while leaving internal helpers correct. The test must fail.

Also perform a semantics-preserving internal refactor. The boundary test should remain green. If it breaks because helper names/call order changed, it has drifted into `test-implementation-coupled` territory.

Do not overgeneralize the rule into “everything needs end-to-end.” A private arithmetic function whose public owner already has boundary proof may need only focused unit/property tests for new arithmetic cases. Evidence should be placed where the claim lives.

You are done when a caller-visible regression cannot hide behind green helper tests, and internal decomposition can still evolve without rewriting the boundary proof.

> A supported entrance is where implementation becomes a promise. Put at least one witness there.