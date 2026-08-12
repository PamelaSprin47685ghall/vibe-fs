# non-exhaustive-transition — Main

## What To Do Now
Make the finite transition relation explicit. Every reachable `state × event` pair must produce a named successor, an intentional idempotent/no-op result, a typed rejection, or be mechanically impossible.

The transition function owns those cells. A wildcard/default does not own semantics; it merely hides cells that nobody decided.

## Why This Matters
The most expensive state-machine bugs often do not come from a wrong branch. They come from a branch that was never designed.

A default such as “ignore everything else” feels defensive until a new event is added. Then the program compiles, tests may stay green, and the new event silently acquires old fallback behavior. A product/domain change has entered production without creating a policy review point.

Exhaustive matching converts ontology growth into work you can see. That is the point: when the world gains a new case, every finite policy depending on the old case set should be forced to answer whether the new case matters.

## Repair Strategy
1. Enumerate reachable states and event/input cases.
2. Build the state × event table.
3. Mark each cell as successor, no-op, rejection, or unreachable.
4. Encode that table with exhaustive matching or an equally total declarative relation.
5. Use typed `IllegalTransition`/`NoOp` rather than conflating rejection with silent retention of current state.
6. Add table/property tests that cover every finite pair.
7. Remove wildcard/default behavior that exists only to make the compiler quiet.

## Decision Branches
- Closed domain: require exhaustive policy.
- Intentionally extensible protocol: define the unknown-case law explicitly and keep that open-world adapter separate from closed domain transitions.
- Several pairs truly share semantics: group them explicitly; do not let a wildcard decide membership accidentally.
- A pair is impossible by construction: prove it with types/constructor rather than a comment saying “cannot happen.”

## Common Wrong Fixes
- Replace `_ -> state` with `_ -> Illegal` while still allowing future cases to fall there without review.
- Add logging to the default and call it explicit.
- Maintain a partial transition map and treat missing key as one universal meaning.
- Create a generated exhaustive table that nobody can inspect or relate to domain vocabulary; mechanical totality without readable policy is still poor design.
- Write comments listing ignored cases beside a wildcard instead of letting the case set enforce them.

## Verification
Enumerate all finite pairs in a property/table test and assert the exact successor/no-op/rejection category.

Then temporarily add a new event case. Build/test must fail or present an explicit unclassified cell until a semantic decision is made. If the new event silently receives an old fallback, the repair is incomplete.

Invariant: **every cell of a closed transition relation is an explicit domain decision.**

## Done When
The transition function reads as a complete policy, ontology growth creates visible review obligations, and no reachable pair derives meaning merely from falling through control flow.