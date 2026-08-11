# implicit-convention-magic — Enforcer

## Definition
Convention becomes magic when correctness depends on names, paths, annotations, reflection, discovery order, or placement that the call site cannot see and the type system does not check. The root-cause is that participation and configuration are encoded as ambient names, paths, or annotations, so the type system never sees the contract and omission becomes a silent runtime change.

## Governing Principle
A hidden convention is an API whose syntax is absence. The programmer “calls” it by putting a file in the right folder or choosing the right string, and failure appears only at runtime when the invisible agreement is broken. Such mechanisms trade explicit local information for global ambient rules; each new convention enlarges the amount of architecture that must live in memory rather than code.

## Trigger When
Trigger when file naming, directory placement, reflection, annotation scanning, registration order, or framework discovery determines behavior without an explicit typed registration or contract.

## Do Not Trigger When
- Do not trigger when convention is merely ergonomic sugar over a checked explicit model and violations fail mechanically at build/startup with clear ownership.
- Do not trigger for directory layout used only for human navigation when behavior is registered explicitly.
- Do not trigger for optional naming sugar that compiles down to a typed registration the compiler checks.

## Distinguish From
implicit-control-flow concerns hidden temporal order. missing-architecture-gate concerns unenforced boundaries. This rule concerns correctness encoded in ambient discovery conventions. Tie-break: if the missing fact is who participates, use this rule; if it is when they run, use implicit-control-flow.

## Decision Procedure
Ask how a new contributor discovers the rule and how the compiler/build detects violation. If the answer is “know the convention and notice runtime absence,” make the relationship explicit.

## Examples
- positive: Handlers are discovered by filename suffix; omitting the suffix silently drops a route.
- near-miss: Files live in a conventional folder, but each handler is also listed in an explicit typed registry checked at startup.
- counterexample: Participants are registered in code; moving a file without updating the registry is a compile or startup error.

## Nudge
Invisible configuration is still configuration. Replace ambient discovery rules with explicit typed registration or a mechanically checked contract.
