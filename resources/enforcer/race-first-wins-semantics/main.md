# race-first-wins-semantics — Main

## What To Do Now
Remove scheduler order from the business decision. Gather the information the rule actually needs and apply a deterministic merge, or explicitly define first-writer semantics if timing truly belongs to the domain.

## Why This Matters
“Whichever finishes first” makes load and latency part of the product model without admitting it. Replays, tests, and retries can then produce different answers from the same logical inputs because execution timing—not data—decides truth.

## Repair Strategy
Define stable identities and the domain merge law. Use concurrency only to obtain inputs faster; join before deciding unless the protocol intentionally elects a winner by time/order.

## Wrong Fixes
Do not add tiny delays to make one branch usually win, or depend on current task scheduling behavior. Those turn nondeterminism into a fragile bias rather than a rule.

## Verification
Permute completion order across the same logical inputs. Results must remain identical unless the documented domain semantics explicitly say otherwise.

## Done When
Business outcomes depend on declared facts and merge rules, not on incidental scheduler timing.
