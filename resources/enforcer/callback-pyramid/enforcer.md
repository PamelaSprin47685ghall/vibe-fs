# callback-pyramid — Enforcer

## Definition
A callback pyramid exists when the lexical nesting of continuations becomes the primary representation of sequencing, so resource lifetime and failure propagation are encoded by indentation rather than structure.

## Governing Principle
Control flow should preserve the operation’s causal order in a form a reader can scan linearly. Deep callback nesting fractures that order into suspended fragments. Every new branch inherits hidden questions—who owns cancellation, which scope releases the resource, where an exception travels, whether later work still runs. The problem is not aesthetic depth; it is loss of a single visible lifetime.

## Trigger When
Trigger when nested callbacks or promise continuations make it difficult to state the operation’s sequence, cleanup, cancellation, or failure path without mentally simulating multiple closures.

## Do Not Trigger When
Do not trigger for shallow callback composition whose lifetime is obvious and whose API is inherently callback-based at the adapter edge.

## Distinguish From
implicit-control-flow hides ordering in frameworks or registration. resource-not-scoped concerns missing lifetime ownership. This rule is lexical continuation nesting that obscures both.

## Decision Procedure
Write the intended operation as a linear causal sequence. If the code cannot be mapped to that sequence without jumping among nested closures, flatten it with structured async control.

## Nudge
Make causality read top to bottom. Use structured async flow so sequence, cancellation, failure, and resource lifetime share one visible scope.
