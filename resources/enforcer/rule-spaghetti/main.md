# rule-spaghetti — Main

## What To Do Now
Rewrite the policy as named predicates, explicit cases, and small composition rules so a reader can recover the business statement without simulating control flow.

## Why This Matters
Nested imperative code makes policy accidental to execution. The same business sentence becomes scattered across branches, assignments, and returns, so changing one clause requires understanding every path that happens to carry it. Declarative structure shortens the proof: the source states which propositions hold and how they combine.

## Repair Strategy
Extract domain queries first, separate dependent checks from independent checks, then compose them with pattern matching, tables, or small combinators. Keep orchestration outside the rule itself.

## Wrong Fixes
Do not merely extract each `if` into a helper while retaining the same opaque sequence. Function names help only if the composition itself exposes the policy.

## Verification
A domain reviewer should be able to map each clause of the rule to a named expression or case, and tests should cover combinations without depending on temporary implementation state.

## Done When
The code is a readable statement of policy whose control flow follows from the rule, rather than a maze from which the rule must be reconstructed.
