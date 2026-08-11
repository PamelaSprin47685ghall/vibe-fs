# leftover-scaffolding — Enforcer

## Definition
Scaffolding is leftover when temporary files, flags, probes, fixtures, scripts, migration aids, or experimental branches remain after the task that justified their temporary existence has ended.

## Governing Principle
Temporary structure has a different contract from maintained structure: it is optimized for a transition, not for indefinite ownership. Once the transition ends, leaving scaffolding in place silently promotes it without assigning the maintenance duties promotion requires. The repository then accumulates artifacts whose intended lifetime is unknown and whose deletion risk grows simply because they have survived.

## Trigger When
Trigger when temporary support created for debugging, migration, rollout, experimentation, or one-time generation remains in the delivered result without a permanent role and owner.

## Do Not Trigger When
- Do not trigger when the artifact has been deliberately promoted into a maintained tool with stable purpose, tests, documentation, and ownership.
- Do not trigger for fixtures that remain the maintained regression for the shipped behavior.
- Do not trigger for feature flags still covering an active, bounded rollout with an owner and removal date.

## Distinguish From
spike-not-cleaned ships experimental design itself. half-finished-refactor leaves dual architecture. This rule concerns ancillary temporary artifacts that outlive the transition they served. Tie-break: if the leftover is dual ownership architecture, use half-finished-refactor; if it is a probe/script/flag that should have vanished, use this rule.

## Decision Procedure
For each temporary artifact, state its ongoing user, maintenance contract, and trigger for execution. If those do not exist, the root-cause is an artifact that outlived its transition: remove it now rather than let age create false legitimacy. Prefer this over dual-architecture leftovers when the residue is a probe, flag, or script that should have vanished.

## Examples
- positive: A debug dump script and `TEMP_SKIP_AUTH` flag remain after the investigation that created them.
- near-miss: A generator is promoted into a maintained command with tests, docs, and an owner.
- counterexample: Temporary probes are deleted when the task ends; only maintained tools remain.

## Nudge
Temporary means “must disappear unless promoted deliberately.” Remove scaffolding after the transition, or give it the full contract of a maintained tool.
