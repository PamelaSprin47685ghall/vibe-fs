# boolean-blindness — Main

## What To Do Now
Replace boolean parameters and flag clusters that stand for named domain choices with explicit cases or distinct types. The type that models the domain choice is who owns the state-space invariant that representable cases equal the legal named alternatives.

## Why This Matters
A call such as `open(true, false)` is information-poor even when the implementation is correct. The meaning lives outside the expression, in memory or comments. Worse, flag combinations create states that may have no counterpart in reality. Every downstream branch then pays to rediscover the distinction the type erased.

## Repair Strategy
Enumerate the legitimate alternatives first. Model exactly those alternatives, attach state-specific data to the cases that need it, and make callers construct the domain choice by name. Let exhaustive matching expose future additions.

## Decision Branches
- If several flags jointly encode named modes, replace the product with those named cases.
- If a single boolean hides a domain noun, introduce a type or enum whose names are the domain vocabulary.
- If the predicate is truly binary and obvious at the call site, leave it; do not invent an enum for `isEmpty`.

## Common Wrong Fixes
- Do not merely rename `flag` to `isSpecial`.
- Do not keep old boolean overloads “for convenience.”
- Do not document the meaning of `true` in a comment while leaving the type as bits.
- Do not add a third boolean to encode a mode the first two could not name.

## Verification
Search call sites for unexplained boolean literals and tests for impossible combinations. The new type should make invalid combinations fail to compile or construct. The invariant is that the representable state space equals the domain’s named choices.

## Done When
The valid state space is visible in the type, call sites read in domain language, and no comment is needed to explain what `true` means.
