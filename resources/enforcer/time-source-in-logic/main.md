# time-source-in-logic — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Domain logic reads the current clock internally instead of receiving an explicit time value or clock port.

## What to do

Time is an implicit dependency. Inject the relevant instant or clock so behavior is deterministic and testable.

## Reference

Family D, enforcement-d05, ordinal 35.
