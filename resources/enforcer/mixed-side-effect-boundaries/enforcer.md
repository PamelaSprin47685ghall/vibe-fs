# mixed-side-effect-boundaries — Enforcer

## Definition
Side-effect boundaries are mixed when one function or module simultaneously owns unrelated effects—storage, network, process control, UI, Git, filesystem—and business policy decides among them in the same imperative body.

## Governing Principle
Effects differ not merely by API but by failure model, lifetime, retry semantics, and authority. Mixing them collapses these distinct contracts into one control-flow surface, so tests need to simulate the whole world and policy becomes inseparable from orchestration accidents. Isolation restores a crucial asymmetry: policy may decide what should happen without knowing how each external world performs it.

## Trigger When
Trigger when one unit directly coordinates multiple unrelated effect systems while also containing domain decisions or mutable shared state.

## Do Not Trigger When
Do not trigger for a thin application shell whose explicit role is to execute already-decided commands across effects and which contains no hidden policy.

## Distinguish From
god-module concerns multiple responsibilities broadly. impure-core concerns effects inside business decisions. This rule focuses on unrelated external contracts sharing one owner.

## Decision Procedure
List effects and their distinct failure/lifetime semantics. Give each a narrow port/adapter, move policy to pure code, and let orchestration compose typed outcomes at one explicit shell.

## Nudge
Different external worlds have different failure laws. Isolate each effect behind its own boundary and keep policy from becoming the place where those laws are entangled.
