# dead-code-delivered — Enforcer

## Definition
Delivered dead code is production code with no reachable role in the current system: unused, superseded, unreferenced, or impossible to execute.

## Governing Principle
Version control is the archive; the working tree is the proposition “this is the system.” Dead code weakens that proposition by preserving alternatives that no longer participate in behavior. Readers must still reason about them, tools still index them, and future maintainers cannot tell whether the path is intentionally dormant or merely forgotten.

## Trigger When
Trigger when production functions, branches, modules, aliases, or paths are unreachable or have no remaining caller after a change.

## Do Not Trigger When
- The path is an intentionally dormant extension point whose activation contract and owner are explicit and tested.
- The code is a supported compatibility surface with a named owner and a documented retirement plan, not an abandoned leftover.
- A feature flag still has a live activation contract, tests, and an owner; the off path is dormant by design, not forgotten.
- Tests, fixtures, or generated snapshots exist only to prove current behavior and are themselves referenced by the suite.

## Distinguish From
`commented-out-code` stores old code in comments. `legacy-cruft-retained` keeps obsolete compatibility surfaces. This rule concerns executable source that no longer belongs to any current behavior. Tie-break: if the source still compiles and could run but has no present caller or contract, this rule owns the case.

## Decision Procedure
Find the caller, activation condition, or contract that gives the code a present role. If none exists, delete it rather than preserve hypothetical utility.

## Examples
- positive: a production helper remains after its last caller was removed; searches still surface it as if it were live.
- near-miss: a plugin hook with an explicit owner, activation test, and documented off-by-default contract.
- counterexample: delete the unreachable path and let version control keep history.

## Nudge
Keep only code with a present proof of life. Delete unreachable production paths; history can recover them if the future ever needs them again.
