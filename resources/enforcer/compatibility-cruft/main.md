# compatibility-cruft — Main

## What To Do Now
Delete compatibility machinery that has no named external obligation. If a real migration exists, write down its consumer, overlap period, and removal condition. A named external consumer with a bounded overlap is who owns a second path; unspecified fear is not who owns a duplicate interface.

## Why This Matters
Every compatibility path creates a second answer to “what is the system?” The cost is not the adapter’s line count; it is the enlarged semantic universe. Bugs can occur only on one path, tests must cover both, and future changes must decide whether to evolve or preserve each historical form.

## Repair Strategy
Choose the canonical representation and move all owned callers to it. Keep only the minimum bridge required by external consumers you cannot change atomically, with an explicit expiry signal.

## Decision Branches
- If no named consumer exists, delete the second path.
- If a real external overlap exists, record consumer, window, and removal condition, then bound the bridge.
- If owned callers can move now, move them and do not keep an alias for comfort.

## Common Wrong Fixes
- Do not dual-write forever.
- Do not keep aliases because deletion feels risky.
- Do not hide old behavior behind a facade with no expiry.
- Do not add a third format to “cover unknown clients.”

## Verification
Search for remaining producers and consumers of the retired form. Every survivor must have an explicit compatibility owner or be removed. The invariant is one canonical interface unless a concrete bounded migration proves why two must temporarily coexist.

## Done When
There is one canonical interface unless a concrete bounded migration proves why two must temporarily coexist.
