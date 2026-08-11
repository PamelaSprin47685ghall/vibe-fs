# unbounded-fanout — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Tasks, requests, subprocesses, agents, or file operations are spawned without a finite concurrency bound.

## What to do

Concurrency is unbounded. Use a bounded map or semaphore and define cancellation behavior.

## Reference

Family F, enforcement-f02, ordinal 52.
