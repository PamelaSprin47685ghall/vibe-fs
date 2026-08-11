# mixed-side-effect-boundaries — Enforcer

## Definition
Side-effect boundaries are mixed when one function or module simultaneously owns unrelated effects—storage, network, process control, UI, Git, filesystem—and business policy decides among them in the same imperative body. The root-cause is that unrelated effect contracts (failure, lifetime, retry, authority) share one imperative owner with domain policy, so each law is no longer isolable.

## Governing Principle
Effects differ not merely by API but by failure model, lifetime, retry semantics, and authority. Mixing them collapses these distinct contracts into one control-flow surface, so tests need to simulate the whole world and policy becomes inseparable from orchestration accidents. Isolation restores a crucial asymmetry: policy may decide what should happen without knowing how each external world performs it.

## Trigger When
Trigger when one unit directly coordinates multiple unrelated effect systems while also containing domain decisions or mutable shared state.

## Do Not Trigger When
- The unit is a thin application shell whose explicit role is to execute already-decided commands across effects and which contains no hidden policy.
- The function talks to one effect system only.
- Related operations share one effect contract (for example several queries on the same store port).
- The function is a generated adapter whose mixed calls are a mechanical translation of one already-decided command.

## Distinguish From
`god-module` concerns multiple responsibilities broadly. `impure-core` concerns effects inside business decisions. Tie-break: if unrelated external contracts share one owner, this rule; if the module is large for many reasons, `god-module`; if domain logic itself performs effects, `impure-core`.

## Decision Procedure
List effects and their distinct failure/lifetime semantics. Give each a narrow port/adapter, move policy to pure code, and let orchestration compose typed outcomes at one explicit shell.

## Examples
- positive: One service method writes the database, shells out to Git, and posts HTTP while deciding retry policy inline.
- near-miss: `main` sequences already-built commands across adapters and contains no domain branching.
- counterexample: A repository adapter performs only persistence; policy lives in a pure function that returns commands.

## Nudge
Different external worlds have different failure laws. Isolate each effect behind its own boundary and keep policy from becoming the place where those laws are entangled.
