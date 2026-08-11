# type-erosion-at-boundary — Main

## What To Do Now
Keep dynamic decoding inside the adapter. Validate once and expose a typed domain contract. Ban `any`/unchecked casts in domain modules.

## Repair Strategy
Find casts and dynamic access inward of adapters. Move parsing out. Introduce DTOs and mappers that fail at the edge.

## Decision Branches
If a language boundary forces reflection, wrap it in one module with a typed façade and tests. If legacy forces gradual typing, stop the bleed with a firewall package.

## Wrong Fixes
Passing `obj` through domain services. Catch-and-cast everywhere. Using reflection to reach private domain state from plugins without a protocol.

## Verification
Domain projects compile/lint without `any` escapes; adapter tests cover invalid payloads failing before domain entry.

## Done When
Dynamic typing stops at adapters; domain code consumes validated types only.

## Scope and Authority
Trust and type boundaries between adapters and domain.
