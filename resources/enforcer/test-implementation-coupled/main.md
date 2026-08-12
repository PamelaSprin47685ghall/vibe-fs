# test-implementation-coupled — Main

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

Common fake repairs:

- delete all white-box tests without adding behavioral evidence;
- replace private field assertions with giant snapshots of equally private object graphs;
- expose internals permanently because old tests are inconvenient to rewrite;
- mock fewer helpers but still assert call choreography that callers never see;
- keep exact call counts where batching/caching would be a legal optimization;
- claim “this sequence is important” without identifying which public/durable invariant makes it important.

Verification has two sides.

First, perform a semantics-preserving refactor: rename/inlining helpers, change internal data structure, batch independent calls, reorder pure calculations. Tests guarding real promises should stay green.

Second, break the promise while keeping much of the old internal choreography intact: return wrong identity, publish twice, skip authorization, expose stale state. The rewritten tests must turn red.

This two-sided check prevents the easy mistake of merely weakening tests. A good suite becomes **less sensitive to irrelevant implementation change and more sensitive to meaningful behavioral change**.

Keep diagnostic unit tests where they protect real local laws. A pure parser function can have direct tests because that function's result is itself its supported contract. The smell is not proximity to implementation; it is asserting details no conforming consumer is entitled to rely on.

You are done when the suite allows multiple correct implementations of the same promise and rejects incorrect ones, instead of demanding imitation of yesterday's decomposition.

> The test should be loyal to the contract, not to the shape of the code that happened to satisfy it first.