# blocking-event-loop — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A synchronous wait, blocking process, filesystem call, sleep, or CPU-heavy loop runs on an event-loop or hook thread.

## What to do

Blocking work is running on the event loop. Move it behind an asynchronous boundary or worker.

## Reference

Family F, enforcement-f04, ordinal 54.
