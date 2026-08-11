# mutable-public-state — Main

## What To Do Now
Remove public write access to invariant-bearing fields and expose domain operations that produce valid next state instead.

## Why This Matters
Public mutation distributes authority without distributing complete knowledge. Each caller can create a state the object itself would never choose, then every downstream consumer must defend against those possibilities. Encapsulation pays the reasoning cost once at the transition boundary.

## Repair Strategy
Keep state immutable or privately owned, define named operations for legitimate transitions, and make each operation return the new value or typed failure. Avoid generic setters when fields have domain meaning.

## Decision Branches
- If callers can assign invariant-bearing fields, hide writes and expose named transitions that enforce the rules.
- If the structure's contract is unrestricted mutation with no higher invariant, leave it public and do not invent false encapsulation.

## Common Wrong Fixes
- Hide fields behind setters that accept any value and perform no domain decision.
- Make fields private but expose a generic `update(patch)` that bypasses transitions.
- Copy validation into every caller instead of concentrating it at the write boundary.

## Verification
Attempt the formerly invalid direct update from a caller. It should be impossible without invoking the operation that owns and verifies the transition. The invariant is that every authoritative write preserves the object's rules.

## Done When
All authoritative state changes pass through a small set of named invariant-preserving operations, and callers cannot bypass the domain's rules by assignment.
