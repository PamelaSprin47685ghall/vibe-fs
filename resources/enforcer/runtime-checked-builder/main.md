# runtime-checked-builder — Main

## What To Do Now
Replace setter-then-validate builders with a single validated constructor or staged builders that make missing fields unrepresentable before `build`.

## Repair Strategy
List required fields and invariants. Collapse into one constructor or typed stages. Move validation to the boundary that creates the value once.

## Decision Branches
If a true multi-step UI wizard is needed, keep a draft DTO separate from the domain type and only promote after full validation.

## Wrong Fixes
Adding more runtime checks at every setter without preventing incomplete `build`. Throwing from `build` while still exposing partial objects. Keeping public mutable setters on the domain type.

## Verification
Attempt incomplete construction; it must be a type or API error, not a late runtime surprise after the object escaped.

## Done When
No incomplete intermediate domain instance can be observed; construction encodes required stages.

## Scope and Authority
Domain and API object construction. Not every optional config bag with documented defaults.
