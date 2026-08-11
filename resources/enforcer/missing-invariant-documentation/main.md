# missing-invariant-documentation — Main

## What To Do Now
Write the hidden correctness rule as a precise invariant at the contract that owns it, then encode a type, test, or gate wherever practical.

## Why This Matters
Undocumented invariants survive only inside people who have already paid the cost to discover them. When those people leave or context fades, later changes can violate the rule while appearing locally reasonable. The defect is not lack of prose; it is failure to preserve necessary knowledge.

## Repair Strategy
State what must always be true, what boundary owns the rule, and what evidence would falsify it. Keep prose concise, then move enforceable parts into construction, exhaustive matching, architecture checks, or behavioral tests.

## Wrong Fixes
Do not scatter the same rule across comments near symptoms. One invariant needs one authoritative definition, with other sites linking or enforcing rather than redefining it.

## Verification
A new contributor should be able to locate the invariant from the owning concept and identify the mechanism that prevents or detects violation.

## Done When
Critical correctness no longer depends on oral tradition: the rule is named once, owned clearly, and mechanically guarded where possible.
