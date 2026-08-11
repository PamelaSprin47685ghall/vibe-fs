# implicit-control-flow — Enforcer

## Definition
Control flow is implicit when essential ordering is determined by registration, hidden callbacks, lifecycle hooks, global initialization, or framework convention rather than visible program structure.

## Governing Principle
Correctness that depends on order should make order a first-class fact. Hidden sequencing turns causality into ambient knowledge: readers see components but not the temporal relation that makes them correct. The system then behaves like a distributed protocol without an explicit protocol—events may be locally valid yet globally wrong because nobody owns the happens-before relation.

## Trigger When
Trigger when correctness depends on callback registration order, hook phase, import side effect, global startup order, or another lifecycle convention not evident at the call site.

## Do Not Trigger When
- Do not trigger when the runtime order is explicitly modeled, documented as a stable contract, and mechanically guarded where misuse is possible.
- Do not trigger for ordinary higher-order callbacks whose call site still names who runs after whom.
- Do not trigger for documented framework lifecycle that fails at startup if the required order is violated.

## Distinguish From
implicit-convention-magic concerns hidden discovery/configuration generally. program-counter-state reifies control as mutable fields. This rule concerns invisible temporal causality. Tie-break: if the hidden fact is happens-before, use this rule; if it is ambient discovery of participants, use implicit-convention-magic.

## Decision Procedure
State the required happens-before relations. Locate where each is enforced. If enforcement exists only as convention or incidental registration order, replace it with explicit structured sequencing.

## Examples
- positive: Correctness depends on plugin registration order and import side effects; the call site shows only `start()`.
- near-miss: An orchestrator names phases in code and startup fails if a phase is missing.
- counterexample: Direct sequential calls make the happens-before relation visible in ordinary control flow.

## Nudge
If order is part of correctness, make order part of the program. Express the causal sequence explicitly instead of trusting lifecycle folklore.
