# runtime-checked-builder — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A complex object is built through setters or fluent mutation and only validated after construction, allowing incomplete intermediate states.

## What to do

Construction correctness is deferred to runtime. Encode the required construction stages or use one validated constructor.

## Reference

Family A, enforcement-a10, ordinal 10.
