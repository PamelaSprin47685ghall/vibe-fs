# god-module — Enforcer

## Definition
A god module owns several unrelated policies, effects, resources, or domains because colocating them is convenient, not because one invariant requires their joint ownership.

## Governing Principle
Size is a symptom; mixed reasons to change are the disease. A module becomes “god-like” when it sits above multiple independent truths and therefore must know how they interact. Every new responsibility increases the Cartesian product of contexts visible inside it, until local changes require understanding storage, network, policy, lifecycle, and presentation at once.

## Trigger When
Trigger when one module controls several side-effect boundaries or domain policies whose lifecycles and invariants can vary independently.

## Do Not Trigger When
Do not trigger merely because a cohesive module is large. A large implementation with one governing invariant may still have one owner.

## Distinguish From
mixed-side-effect-boundaries focuses on combining effects in one function/module. generic-helper-bucket lacks ownership entirely. This rule concerns one owner accumulating several distinct sovereignties.

## Decision Procedure
List the module’s reasons to change. If several can change independently and have different invariants, split by those reasons—not by arbitrary line count.

## Nudge
Do not split by size; split by sovereignty. Give each independent invariant and side-effect boundary an owner small enough to reason about without importing the others.
