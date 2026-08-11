# callback-pyramid — Enforcer

## Definition
A callback pyramid exists when the lexical nesting of continuations becomes the primary representation of sequencing, so resource lifetime and failure propagation are encoded by indentation rather than structure. The root-cause is that nested continuations make indentation the representation of sequence, so resource lifetime, cancellation, and failure have no single visible scope.

## Governing Principle
Control flow should preserve the operation’s causal order in a form a reader can scan linearly. Deep callback nesting fractures that order into suspended fragments. Every new branch inherits hidden questions—who owns cancellation, which scope releases the resource, where an exception travels, whether later work still runs. The problem is not aesthetic depth; it is loss of a single visible lifetime.

## Trigger When
Trigger when nested callbacks or promise continuations make it difficult to state the operation’s sequence, cleanup, cancellation, or failure path without mentally simulating multiple closures.

## Do Not Trigger When
- The composition is shallow, lifetime is obvious, and the API is inherently callback-based at the adapter edge.
- Structured async (`async`/`await`, tasks with one scope) already owns sequence, and a single callback is only the foreign API edge.
- Event registration that is not the operation’s primary sequencer is not a pyramid by itself.
- Parallel joins expressed as named combinators are structure, not nested continuation soup.

## Distinguish From
`implicit-control-flow` hides ordering in frameworks or registration. `resource-not-scoped` concerns missing lifetime ownership. This rule is lexical continuation nesting that obscures both. Tie-break: if a reader must climb nested closures to recover sequence, cleanup, or failure, this rule owns the case.

## Decision Procedure
Write the intended operation as a linear causal sequence. If the code cannot be mapped to that sequence without jumping among nested closures, flatten it with structured async control.

## Examples
- positive: open → read → parse → write nested four callbacks deep, with cleanup and errors handled in inner closures.
- near-miss: a one-level adapter callback immediately promisified, then a linear `async` function owns the rest.
- counterexample: flatten into structured async with one lexical lifetime for resources, cancellation, and errors.

## Nudge
Make causality read top to bottom. Use structured async flow so sequence, cancellation, failure, and resource lifetime share one visible scope.
