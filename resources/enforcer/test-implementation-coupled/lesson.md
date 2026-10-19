# test-implementation-coupled — Lesson

## Opinion

### Definition

A test is implementation-coupled when it punishes a correct refactor because it froze **how the code currently works** instead of **what the supported contract requires**.

### Governing Principle

Common coupling targets include:

- exact private helper call counts;
- internal method names or object layout;
- intermediate field values never exposed as contract;
- incidental sequence of pure computations;
- mocks asserting which helper called which helper;
- snapshots of internal JSON/state whose exact shape is not public;
- tests reaching private members through reflection/test-only exports;
- algorithm-specific steps where several equivalent algorithms are valid.

Why this is costly: such tests make the old implementation an unofficial second specification. A simpler algorithm, changed decomposition, removed helper, batched call, or equivalent data structure causes red even though users would observe no regression. Teams then stop refactoring because the suite charges a tax for changing details nobody promised.

Worse, implementation-coupled tests can still miss real bugs. Code may reproduce the expected helper choreography while returning the wrong public result. The suite has frozen motion, not meaning.

### Trigger When

A useful thought experiment is a semantics-preserving rewrite: replace a helper chain with one pure function, change a list to a map, batch two internal calls, inline/remove a private method, reorder independent calculations. If the test fails and nobody can name a contract violation, it is coupled.

### Do Not Trigger When

Do not overcorrect. Some interactions **are** contractual. Exactly-once publication, "zero external calls on rejection," transaction boundaries, durable ordering, idempotency-key reuse, provider call sequence required by a real protocol — these can legitimately be observed. The criterion is whether a conforming implementation is allowed to vary the detail.

### Distinguish From

This differs from `weakened-test-to-pass`: that rule deletes or loosens a valid behavioral expectation because production fails it. Here the expectation was never a legitimate promise in the first place. Removing private choreography can strengthen the suite if the replacement assertion moves outward to the real contract.

`behavioral-boundary-untested` often coexists with this smell: the suite has hundreds of internal assertions yet no proof through the supported entrance.

### Decision Procedure

The central diagnostic is substitutability:

> Could a different implementation satisfy every real caller-visible promise and still fail this test?

If yes, the test may be protecting private choreography rather than behavior.

### Examples

Exact private helper call counts. Internal method names or object layout. Intermediate field values never exposed as contract. Mocks asserting which helper called which helper. Snapshots of internal JSON/state whose exact shape is not public.

## Advice

### What To Do Now

Move assertions outward until they land on a promise.

For every private/internal assertion, ask what real behavior it was trying to protect. Rewrite the test in terms of supported input, observable result, durable state, or truly contractual external interaction.

Examples:

```text
"helper X called twice"
    ↓
"one logical publication occurs exactly once"

"private field status = ready"
    ↓
"supported API now admits the operation"

"method A runs before B"
    ↓
"durable commit precedes provider-visible success"
```

The replacement may still observe interactions, but only where interaction itself is the contract: no network call under rejection, exactly-once effect, ordering required for durability, stable idempotency identity, protocol handshake, etc.

### Why This Matters

A good suite becomes **less sensitive to irrelevant implementation change and more sensitive to meaningful behavioral change**. This two-sided check prevents the easy mistake of merely weakening tests.

### Repair Strategy

Keep diagnostic unit tests where they protect real local laws. A pure parser function can have direct tests because that function's result is itself its supported contract. The smell is not proximity to implementation; it is asserting details no conforming consumer is entitled to rely on.

### Decision Branches


### Common Wrong Fixes

Common fake repairs:

- delete all white-box tests without adding behavioral evidence;
- replace private field assertions with giant snapshots of equally private object graphs;
- expose internals permanently because old tests are inconvenient to rewrite;
- mock fewer helpers but still assert call choreography that callers never see;
- keep exact call counts where batching/caching would be a legal optimization;
- claim "this sequence is important" without identifying which public/durable invariant makes it important.

### Verification

Verification has two sides.

First, perform a semantics-preserving refactor: rename/inlining helpers, change internal data structure, batch independent calls, reorder pure calculations. Tests guarding real promises should stay green.

Second, break the promise while keeping much of the old internal choreography intact: return wrong identity, publish twice, skip authorization, expose stale state. The rewritten tests must turn red.

### Done When

You are done when the suite allows multiple correct implementations of the same promise and rejects incorrect ones, instead of demanding imitation of yesterday's decomposition.

> The test should be loyal to the contract, not to the shape of the code that happened to satisfy it first.

### Nudge

> Tests should make wrong behavior expensive and correct refactoring cheap. If the reverse is true, the suite is guarding implementation nostalgia.
