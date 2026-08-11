# time-dependent-test — Main

## What To Do Now
Inject a fake clock or fixed instants. Drive time explicitly in the arrange phase. Remove wall-clock sleeps from assertions.

## Repair Strategy
Replace Date.now/new Date with a port. In tests, advance time manually to trigger timeouts and expirations.

## Decision Branches
If timezone behavior matters, fix a zone and instant rather than using the machine local zone.

## Wrong Fixes
asserting `Date.now()` within a fuzzy window as the main proof. sleep then assert. Tests that fail around midnight or DST.

## Verification
Tests pass with the system clock set arbitrarily; time-sensitive branches flip by advancing the fake clock only.

## Done When
Time in tests is injected and deterministic; wall-clock luck is gone.

## Scope and Authority
Automated tests. Production time injection is time-source-in-logic.
