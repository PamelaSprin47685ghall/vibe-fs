# dead-code-delivered — Enforcer

## Definition
Delivered dead code is production code with no reachable role in the current system: unused, superseded, unreferenced, or impossible to execute.

## Governing Principle
Version control is the archive; the working tree is the proposition “this is the system.” Dead code weakens that proposition by preserving alternatives that no longer participate in behavior. Readers must still reason about them, tools still index them, and future maintainers cannot tell whether the path is intentionally dormant or merely forgotten.

## Trigger When
Trigger when production functions, branches, modules, aliases, or paths are unreachable or have no remaining caller after a change.

## Do Not Trigger When
Do not trigger for intentionally dormant extension points or feature paths whose activation contract and owner are explicit and tested.

## Distinguish From
commented-out-code stores old code in comments. legacy-cruft-retained keeps obsolete compatibility surfaces. This rule concerns executable source that no longer belongs to any current behavior.

## Decision Procedure
Find the caller, activation condition, or contract that gives the code a present role. If none exists, delete it rather than preserve hypothetical utility.

## Nudge
Keep only code with a present proof of life. Delete unreachable production paths; history can recover them if the future ever needs them again.
