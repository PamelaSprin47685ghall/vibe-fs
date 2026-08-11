# rule-spaghetti — Main

## What To Do Now
Rewrite the policy as named predicates, explicit cases, and small composition rules so a reader can recover the business statement without simulating control flow. Named predicates and their composition are who owns the business rule; nested branches and mutable flags are not who owns the policy statement.

## Why This Matters
Nested imperative code makes policy accidental to execution. The same business sentence becomes scattered across branches, assignments, and returns, so changing one clause requires understanding every path that happens to carry it. Declarative structure shortens the proof: the source states which propositions hold and how they combine.

## Repair Strategy
Extract domain queries first, separate dependent checks from independent checks, then compose them with pattern matching, tables, or small combinators. Keep orchestration outside the rule itself.

## Decision Branches
- If the policy is still only in nested branches, extract named propositions and compose them.
- If propositions are already named but glued with mutable flags, replace flags with combinators or cases.
- If composition exists but uses the wrong failure law, that is wrong-rule-composition—fix the combinator, not the names.

## Common Wrong Fixes
- Extract each `if` into a helper while retaining the same opaque sequence.
- Add comments that narrate the maze instead of naming propositions.
- Collapse everything into one boolean expression so dense it is still unreadable.
- Introduce a generic “rules engine” without stating the domain cases.

## Verification
A domain reviewer should be able to map each clause of the rule to a named expression or case, and tests should cover combinations without depending on temporary implementation state. The invariant is: the source states the policy relation, so readers need not reconstruct it from control flow.

## Done When
The code is a readable statement of policy whose control flow follows from the rule, rather than a maze from which the rule must be reconstructed.
