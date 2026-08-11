# half-finished-refactor — Enforcer

## Definition
A refactor is half-finished when old and new ownership models coexist after the change, forcing adapters, duplicate paths, or conventions to decide which world a caller belongs to.

## Governing Principle
A refactor changes the representation of knowledge without intending to change behavior. During migration two representations may temporarily coexist, but that state is not architecture—it is scaffolding. If delivered without a bounded migration contract, the temporary duality becomes permanent and future changes must maintain both stories.

## Trigger When
Trigger when old and new APIs, models, modules, names, or data paths remain active after a refactor with no explicit external migration need or removal boundary.

## Do Not Trigger When
Do not trigger during a deliberate staged migration whose consumers, compatibility period, and completion criteria are explicit and actively bounded.

## Distinguish From
compatibility-cruft lacks a justified compatibility obligation. legacy-cruft-retained violates an explicit clean break. This rule concerns an ownership transfer that stopped before one side became authoritative.

## Decision Procedure
Name the intended post-refactor owner. Trace every remaining old-path caller and adapter. If the repository itself controls them, migrate them and remove the superseded surface in the same change.

## Nudge
A migration state is not a destination. Finish the ownership transfer, move every owned caller, and delete the path whose only purpose was to bridge old and new worlds.
