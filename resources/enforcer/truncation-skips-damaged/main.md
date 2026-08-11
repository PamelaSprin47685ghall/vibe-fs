# truncation-skips-damaged — Main

## What To Do Now
Fail closed on interior corruption. Allow truncation only of a trailing incomplete frame. Alert and require repair before applying later facts.

## Repair Strategy
Detect checksum/parse failures with offsets. If not at the tail, stop replay and surface repair tooling. Never jump the gap.

## Decision Branches
If a known bad segment has an authorized repair event, apply the repair procedure—do not silently skip. If dual logs exist, reconcile explicitly.

## Wrong Fixes
catch-and-continue on decode errors mid-log. Dropping "bad" events to keep uptime. Rebuilding state from a suffix of the log.

## Verification
Inject mid-log corruption; recovery halts. Inject truncated tail only; recovery truncates cleanly and continues after append.

## Done When
Interior corruption fails closed; only trailing incomplete records may be truncated.

## Scope and Authority
Durable log/event recovery paths.
