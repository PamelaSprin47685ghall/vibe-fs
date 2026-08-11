# rule-spaghetti — Main

## What To Do Now
Extract the rule into named predicates, decision tables, pattern matches, or combinators. Eliminate temporary boolean piles and mutation that exist only to thread control.

## Repair Strategy
Write the rule in one paragraph of prose, then mirror that structure in code. Prefer exhaustive matches and early domain results over flag accumulation.

## Decision Branches
If performance forces a special path, keep the declarative form as source of truth and derive the optimized path with tests proving equivalence.

## Wrong Fixes
Adding more flags to an already opaque chain. Commenting the intended rule above unreformed spaghetti. Extracting methods that still mutate shared control state.

## Verification
A reader can state the rule from the code structure without stepping through. Table-driven cases match the prose rule.

## Done When
The rule is directly readable; simulation of nested control is no longer required to understand outcomes.

## Scope and Authority
Business and validation rules. Not every low-level loop.
