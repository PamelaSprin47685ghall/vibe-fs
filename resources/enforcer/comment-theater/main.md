# comment-theater — Main

## What To Do Now
Remove comments that merely translate unclear code and rewrite the code so its names, types, and structure carry the intended meaning directly.

## Why This Matters
A compensating comment creates two representations of one fact, but only one is mechanically checked. As code changes, the prose can remain plausible while becoming false. Worse, readers learn to depend on the explanation rather than demand a structure that makes the rule obvious.

## Repair Strategy
For each noisy comment, identify the knowledge it tries to supply. If it is “what,” encode it in naming and types. If it is “how,” simplify the control flow. If it is a durable “why” imposed from outside the code, keep the shortest precise explanation and name the source of the constraint.

## Decision Branches
- If the comment restates syntax or translates a poor name, delete it and repair the structure.
- If the comment explains tangled control flow, untangle the flow instead of polishing the prose.
- If the knowledge cannot live in types, names, or tests, keep the shortest durable why.

## Common Wrong Fixes
- Do not rewrite obvious comments in more polished prose.
- Do not add comments around every branch.
- Do not leave the unclear code and add a longer apology.
- Do not move theater into a README while the structure stays opaque.

## Verification
Read the implementation with routine comments hidden. The governing behavior should remain understandable; remaining comments should add information unavailable from the code itself. The invariant is that executable meaning lives in structure; prose carries only knowledge the compiler cannot enforce.

## Done When
Prose no longer carries executable meaning the structure could own, and every surviving comment protects a genuine piece of non-obvious knowledge.
