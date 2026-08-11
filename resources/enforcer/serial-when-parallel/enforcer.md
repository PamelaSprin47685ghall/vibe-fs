# serial-when-parallel — Enforcer

## Definition
Work is serial when it could be parallel if independent operations are forced into one temporal chain without a dependency, critical section, or protocol ordering that requires it.

## Governing Principle
Concurrency should follow independence. If A and B share no premise or mutable owner, making B wait for A invents causality the problem does not contain. The opposite error is unbounded fan-out; the correct model is a dependency graph executed with finite capacity.

## Trigger When
Trigger when independent tool calls, validations, reads, computations, or I/O operations run sequentially solely by implementation habit.

## Do Not Trigger When
Do not trigger when later work consumes earlier results, shared mutable state requires serialization, or an external protocol defines order as part of correctness.

## Distinguish From
serial-investigation is the evidence-gathering specialization. unbounded-fanout is the opposite failure: respecting independence without respecting finite capacity.

## Decision Procedure
Draw dependency edges. Operations with no path between them may run concurrently, subject to an explicit bound and deterministic collection of results.

## Nudge
Let independence become concurrency, but never infinity. Execute the real dependency graph with a finite bound instead of inventing a serial chain.
