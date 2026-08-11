# failure-path-untested — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

New error handling, cancellation, rollback, retry, malformed input, or recovery behavior has no direct test.

## What to do

A newly introduced failure path is untested. Add a test that exercises the actual failure and its observable result.

## Reference

Family G, enforcement-g09, ordinal 69.
