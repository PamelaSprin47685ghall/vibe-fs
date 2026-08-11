# context-model-leak — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

## What to do

One model is serving incompatible bounded contexts. Give each context its own concept and translate explicitly.

## Reference

Family C, enforcement-c02, ordinal 22.
