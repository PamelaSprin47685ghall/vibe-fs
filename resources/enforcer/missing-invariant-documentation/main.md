# missing-invariant-documentation — Main

## What To Do Now
Write the hidden correctness rule as a precise invariant at the contract that owns it, then encode a type, test, or gate wherever practical. The owning contract is who owns the invariant: one falsifiable sentence there, not scattered comments near symptoms.

## Why This Matters
Undocumented invariants survive only inside people who have already paid the cost to discover them. When those people leave or context fades, later changes can violate the rule while appearing locally reasonable. The defect is not lack of prose; it is failure to preserve necessary knowledge.

## Repair Strategy
State what must always be true, what boundary owns the rule, and what evidence would falsify it. Keep prose concise, then move enforceable parts into construction, exhaustive matching, architecture checks, or behavioral tests.

## Decision Branches
- If the property can be made unrepresentable by a type or construction rule, encode it there and keep a one-sentence owner statement.
- If it cannot be typed, record the falsifiable sentence at the owner and add the cheapest test or gate that detects violation.

## Common Wrong Fixes
- Scatter the same rule across comments near symptoms instead of one authoritative definition.
- Write vague prose ("be careful with ordering") that cannot be falsified.
- Duplicate the invariant in several modules so later edits diverge.

## Verification
A new contributor should be able to locate the invariant from the owning concept and identify the mechanism that prevents or detects violation.

## Done When
Critical correctness no longer depends on oral tradition: the rule is named once, owned clearly, and mechanically guarded where possible.
