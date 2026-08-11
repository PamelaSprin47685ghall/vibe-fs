# release-ladder-skipped — Main

## What To Do Now
Run the applicable ladder in order: pure unit/property → contract/boundary → replay/integration → canary/limited release → full promote. Do not claim higher rungs while lower ones are red or unrun.

## Repair Strategy
List required gates for this change class. Execute missing lower gates first. Only then re-run the higher signal that originally looked attractive.

## Decision Branches
If a gate is inapplicable, record why and skip explicitly. If a lower gate is slow, still run a focused subset that covers the changed contract before promotion.

## Wrong Fixes
Shipping because the UI smoke passed while domain tests failed. Inflating canary scope to replace unit proof. Declaring ladder complete from CI green on unrelated packages.

## Verification
Evidence exists for each required rung in order; higher rungs were not the first or only signal.

## Done When
Every applicable lower gate passed before promotion; skip reasons are explicit where a rung does not apply.

## Scope and Authority
Behavioral and release changes. Content-only edits may use a shorter documented path.
