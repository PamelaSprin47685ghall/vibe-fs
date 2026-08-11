# wrong-rule-composition — Main

## What To Do Now
Classify rules by dependence, then use sequential short-circuit composition for prerequisite chains and accumulating composition for independent validations.

## Why This Matters
Error behavior is part of policy. Running a rule after its premise failed manufactures misleading errors; stopping after one independent failure withholds other facts the same input already proves. The right composition preserves the logical structure of the domain rather than imposing one control-flow habit everywhere.

## Repair Strategy
Draw dependencies between rules, separate prerequisite establishment from independent constraints, and encode both semantics in small named combinators. Keep error types rich enough to distinguish prerequisite failure from independently accumulated violations.

## Wrong Fixes
Do not choose “fail fast” or “collect all” as a project-wide ideology. Neither is universally correct; each follows from a different logical relation between propositions.

## Verification
Create cases with a failed prerequisite plus downstream rules, and cases with several independent violations. The former should report only meaningful reachable errors; the latter should return the complete independent set.

## Done When
The rule engine’s control semantics are derivable from logical dependence, so every reported error is meaningful and no independent truth is hidden by arbitrary evaluation order.
