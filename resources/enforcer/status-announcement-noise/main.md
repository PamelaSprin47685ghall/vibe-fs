# status-announcement-noise — Main

## What To Do Now
Delete routine "starting/done/working on" chatter. Emit signals only when a decision is made, a phase completes with a result, or action is required.

## Repair Strategy
Audit log and message sites. Keep error and decision lines. Collapse multi-line progress into one summary at boundaries. Prefer structured events over prose spam.

## Decision Branches
If operators need heartbeats, use a single metric or sparse heartbeat with timestamps—not paragraph status. If debugging, gate verbose traces behind a flag.

## Wrong Fixes
Logging every loop iteration as INFO. Agent turns that only narrate tool intent without results. Comments that restate the next line as "now we process".

## Verification
Output under normal load is scannable; each remaining line carries decision, result, failure, or action.

## Done When
Routine status spam is gone; remaining messages are actionable or decision-bearing.

## Scope and Authority
Logs, agent messages, and user-visible operational output. Not required audit trails.
