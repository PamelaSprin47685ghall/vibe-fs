# mixed-side-effect-boundaries — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

## What to do

Unrelated side-effect boundaries are mixed together. Isolate each effect behind a narrow port and keep policy pure.

## Reference

Family C, enforcement-c06, ordinal 26.
