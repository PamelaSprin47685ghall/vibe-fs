# wrong-rule-composition — Main

## What To Do Now
Classify rules by dependence, then use sequential short-circuit composition for prerequisite chains and accumulating composition for independent validations.

## Why This Matters
Error behavior is part of policy. Running a rule after its premise failed manufactures misleading errors; stopping after one independent failure withholds other facts the same input already proves. The right composition preserves the logical structure of the domain rather than imposing one control-flow habit everywhere.

## Repair Strategy
Draw dependencies between rules, separate prerequisite establishment from independent constraints, and encode both semantics in small named combinators. Keep error types rich enough to distinguish prerequisite failure from independently accumulated violations.

## Decision Branches
If rule B requires a fact established by A, sequence them and short-circuit when A fails.
If rules are independent on the same input, accumulate their results instead of stopping at the first failure.

## Common Wrong Fixes
- Choose “fail fast” or “collect all” as a project-wide ideology.
- Keep cascading nonsense errors and filter them in the UI.
- Duplicate each rule’s prerequisite checks instead of expressing dependence in the combinator.

## Verification
Invariant: reported errors are exactly the meaningful ones given logical dependence. Cases with a failed prerequisite plus downstream rules must report only reachable errors; cases with several independent violations must return the complete independent set.

## Done When
The rule engine’s control semantics are derivable from logical dependence, so every reported error is meaningful and no independent truth is hidden by arbitrary evaluation order.
