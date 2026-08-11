# boolean-blindness — Main

## What To Do Now
Replace boolean parameters and flag clusters that stand for named domain choices with explicit cases or distinct types.

## Why This Matters
A call such as `open(true, false)` is information-poor even when the implementation is correct. The meaning lives outside the expression, in memory or comments. Worse, flag combinations create states that may have no counterpart in reality. Every downstream branch then pays to rediscover the distinction the type erased.

## Repair Strategy
Enumerate the legitimate alternatives first. Model exactly those alternatives, attach state-specific data to the cases that need it, and make callers construct the domain choice by name. Let exhaustive matching expose future additions.

## Wrong Fixes
Do not merely rename `flag` to `isSpecial`. Do not keep old boolean overloads “for convenience.” A named boolean still has only two anonymous values and compatibility overloads preserve the ambiguity.

## Verification
Search call sites for unexplained boolean literals and tests for impossible combinations. The new type should make invalid combinations fail to compile or construct.

## Done When
The valid state space is visible in the type, call sites read in domain language, and no comment is needed to explain what `true` means.
