# behavioral-boundary-untested — Lesson

## Opinion

### Definition

A behavioral boundary is untested when the suite proves the pieces behind a supported entry point but never proves the behavior **through the entry point callers actually use**.

### Governing Principle

The trap is seductive because helper tests often look stronger: they are fast, precise, easy to arrange, and produce excellent coverage. But a public behavior is more than the sum of its helpers. The boundary contains wiring, defaults, identity, authorization, normalization, serialization, effect ordering, error mapping, and dependency composition — exactly the places where individually correct pieces can become collectively wrong.

Think of internal tests as lemmas and the supported boundary as the theorem. Ten correct lemmas do not prove a theorem whose composition is wrong.

### Trigger When

Fire this rule when:

- tests call private/internal helpers directly while the real public method/route/tool/hook remains unexercised;
- fixtures mutate internal state into the desired setup instead of entering through supported behavior;
- a test-only export bypasses the production adapter, decoder, permission gate, or workflow owner;
- helper coverage is cited as evidence that public defaults/wiring work;
- public identity or failure semantics changed but tests still stop one layer below them;
- integration wiring can be broken while every unit test stays green.

### Do Not Trigger When

Do not fire when the public behavior already has a strong boundary test and helper tests merely localize failures. Do not demand a huge end-to-end environment for every pure helper change. The question is whether the **caller-visible promise has at least one failing-capable proof at its owning entrance**.

### Distinguish From

This differs from `contract-test-missing`: that rule is for a boundary between independent systems/runtimes whose agreement can drift. This rule also applies inside one product whenever callers depend on a supported behavioral surface. `test-implementation-coupled` is the opposite failure: tests cross too far inward and freeze private choreography.

### Decision Procedure

The decisive mutation is simple. Keep every helper correct, but break the boundary wiring: wrong default, swapped field, missing permission, wrong serializer, forgotten adapter, stale route, different ID. Would any test turn red? If not, the public theorem is unproved.

A good boundary test does not need to be broad. It should be the **narrowest test that still enters where the real caller enters** and observes what the caller is entitled to rely on.

### Examples


## Advice

### What To Do Now

Add one test through the supported entrance that owns the promise.

### Why This Matters

Keep lower-level tests when they buy diagnosis or exhaust a pure law. They are useful once the boundary theorem exists. The repair is not “delete unit tests”; it is “stop asking unit tests to certify integration they never exercise.”

### Repair Strategy

Start from the caller's sentence, not from the helper you want to cover:

```text
When caller supplies X through supported boundary B,
caller may observe Y and must not observe Z.
```

Then build the smallest fixture that crosses B using production decoding/wiring/permission/default logic relevant to that promise. Assert the caller-visible result, durable state, or external effect — not private helper choreography.

### Decision Branches

Do not overgeneralize the rule into “everything needs end-to-end.” A private arithmetic function whose public owner already has boundary proof may need only focused unit/property tests for new arithmetic cases. Evidence should be placed where the claim lives.

### Common Wrong Fixes

Common fake repairs:

- exporting a private helper solely so the test can call it;
- reproducing the production wiring inside the test fixture, thereby allowing both fixture and production to be wrong differently;
- constructing post-decoder objects directly when the bug could live in decoding/defaults;
- mocking the permission/identity layer away when that layer is part of the public behavior;
- adding a giant full-stack E2E when a narrow real boundary test would be faster and more precise;
- asserting only “request did not throw,” while wrong result/default/identity would still pass.

### Verification

Verification should prove the test owns a real boundary risk. Deliberately introduce one plausible composition defect — swap a field, remove a default, bypass an adapter, miswire a dependency, change an ID — while leaving internal helpers correct. The test must fail.

Also perform a semantics-preserving internal refactor. The boundary test should remain green. If it breaks because helper names/call order changed, it has drifted into `test-implementation-coupled` territory.

### Done When

You are done when a caller-visible regression cannot hide behind green helper tests, and internal decomposition can still evolve without rewriting the boundary proof.

> A supported entrance is where implementation becomes a promise. Put at least one witness there.

### Nudge

> Prove behavior at the place where the promise becomes real, not only at the places where implementation happens to be convenient to test.
