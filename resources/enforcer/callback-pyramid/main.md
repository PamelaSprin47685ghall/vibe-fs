# callback-pyramid — Main

## What To Do Now
Flatten the continuation tree into structured asynchronous control with one explicit lifetime for resources, cancellation, and errors.

## Why This Matters
Nested callbacks turn time into topology. A reader must infer execution order from lexical depth, then separately reconstruct which closure owns each failure and cleanup action. That representation scales poorly because every extra branch multiplies paths without improving the domain model.

## Repair Strategy
Promisify or adapt callback APIs at the edge, then express the operation as a linear async sequence. Scope resources with language constructs that guarantee disposal, propagate cancellation explicitly, and gather parallel branches at named join points.

## Decision Branches
- If nesting encodes the operation’s sequence, flatten it into structured async with one visible lifetime.
- If a foreign API is callback-only, adapt at the edge and keep the pyramid from spreading inward.
- If branches are independent, join them with named combinators rather than deeper nesting.

## Common Wrong Fixes
- Do not merely extract nested callbacks into separately named functions if the hidden lifetime remains.
- Do not add more `.then` layers that preserve the same continuation tree.
- Do not swallow errors in inner closures to “simplify” the pyramid.
- Do not leave cancellation unthreaded because the flattened syntax looks linear.

## Verification
Trace success, failure, and cancellation from one top-level operation. Each path should have an obvious owner and deterministic cleanup. The invariant is that causal order, resource lifetime, and failure propagation are visible in one structured scope rather than in indentation.

## Done When
The code reads in causal order, every resource has one lexical lifetime, and no reader must follow a pyramid of closures to discover what happens next.
