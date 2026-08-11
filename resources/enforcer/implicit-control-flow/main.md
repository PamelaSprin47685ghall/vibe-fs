# implicit-control-flow — Main

## What To Do Now
Move correctness-critical ordering into explicit structured control flow, with named phases or direct sequencing where the dependency is real. The sequencer that names the required phases is who owns happens-before; registration order, import side effects, and hook folklore are not.

## Why This Matters
Invisible order is an undeclared protocol. It survives only while every participant remembers when hooks run, imports fire, or callbacks were registered. Refactors that preserve all local functions can still break the global behavior because the causal relation lived nowhere explicit.

## Repair Strategy
Write down the required happens-before graph, then encode it using ordinary calls, structured async scopes, explicit state transitions, or a small orchestrator that owns only sequencing—not hidden global registration.

## Decision Branches
- If the order is a real domain protocol, encode it as named phases or direct calls owned by one sequencer.
- If the framework already guards the order mechanically, keep that contract and do not add a second hidden sequence.

## Common Wrong Fixes
- Do not add comments saying “must register before X” while leaving the runtime free to violate it.
- Do not add more phase flags to recover hidden ordering after the fact.
- Do not rely on test-ordering or import-sorting to keep the protocol intact.

## Verification
Tests should permute or isolate components where possible and demonstrate that correctness follows the explicit sequence rather than incidental construction order. The invariant is the happens-before graph: required order is enforced by program structure, not folklore.

## Done When
A reader can see the temporal contract in normal program structure and no critical behavior depends on remembering undocumented hook or registration order.
