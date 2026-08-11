# callback-pyramid — Main

## What To Do Now
Flatten the continuation tree into structured asynchronous control with one explicit lifetime for resources, cancellation, and errors.

## Why This Matters
Nested callbacks turn time into topology. A reader must infer execution order from lexical depth, then separately reconstruct which closure owns each failure and cleanup action. That representation scales poorly because every extra branch multiplies paths without improving the domain model.

## Repair Strategy
Promisify or adapt callback APIs at the edge, then express the operation as a linear async sequence. Scope resources with language constructs that guarantee disposal, propagate cancellation explicitly, and gather parallel branches at named join points.

## Wrong Fixes
Do not merely extract nested callbacks into separately named functions if the hidden lifetime remains. Moving indentation between files does not restore structured causality.

## Verification
Trace success, failure, and cancellation from one top-level operation. Each path should have an obvious owner and deterministic cleanup.

## Done When
The code reads in causal order, every resource has one lexical lifetime, and no reader must follow a pyramid of closures to discover what happens next.
