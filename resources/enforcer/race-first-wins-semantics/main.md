# race-first-wins-semantics — Main

## What To Do Now
Remove scheduler order from the business decision. Gather the information the rule actually needs and apply a deterministic merge, or explicitly define first-writer semantics if timing truly belongs to the domain. The documented merge law, or an explicit first-writer protocol, is who owns the outcome; the scheduler is not who owns business truth.

## Why This Matters
“Whichever finishes first” makes load and latency part of the product model without admitting it. Replays, tests, and retries can then produce different answers from the same logical inputs because execution timing—not data—decides truth.

## Repair Strategy
Define stable identities and the domain merge law. Use concurrency only to obtain inputs faster; join before deciding unless the protocol intentionally elects a winner by time/order.

## Decision Branches
- If the domain merge is independent of arrival, collect all required results and apply the merge after the join.
- If first-writer or election-by-time is the real protocol, document identity, quorum, and tie-break so timing is an explicit rule, not an accident.
- If only one owner should decide, serialize through that owner and stop racing writers.

## Common Wrong Fixes
- Add tiny delays to make one branch usually win.
- Depend on current task scheduling or thread priority as a substitute for a merge law.
- Keep first-completion as the result while adding retries that sample a different winner.
- Treat flaky tests as noise instead of evidence that timing is still choosing truth.

## Verification
Permute completion order across the same logical inputs. Results must remain identical unless the documented domain semantics explicitly say otherwise. The invariant is: logical inputs plus declared merge/identity determine the outcome, not scheduler order.

## Done When
Business outcomes depend on declared facts and merge rules, not on incidental scheduler timing.
