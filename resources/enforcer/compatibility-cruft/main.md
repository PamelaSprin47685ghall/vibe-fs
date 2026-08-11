# compatibility-cruft — Main

## What To Do Now
Delete compatibility machinery that has no named external obligation. If a real migration exists, write down its consumer, overlap period, and removal condition.

## Why This Matters
Every compatibility path creates a second answer to “what is the system?” The cost is not the adapter’s line count; it is the enlarged semantic universe. Bugs can occur only on one path, tests must cover both, and future changes must decide whether to evolve or preserve each historical form.

## Repair Strategy
Choose the canonical representation and move all owned callers to it. Keep only the minimum bridge required by external consumers you cannot change atomically, with an explicit expiry signal.

## Wrong Fixes
Do not dual-write forever, keep aliases because deletion feels risky, or hide old behavior behind a facade. Unspecified compatibility has no finish line and therefore becomes permanent.

## Verification
Search for remaining producers and consumers of the retired form. Every survivor must have an explicit compatibility owner or be removed.

## Done When
There is one canonical interface unless a concrete bounded migration proves why two must temporarily coexist.
