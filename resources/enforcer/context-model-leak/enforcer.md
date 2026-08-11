# context-model-leak — Enforcer

Id: enforcement-c02 / Family: C / Ordinal: 22

## ScoreWhen

One shared model is reused across authentication, ordering, sessions, persistence, UI, or other contexts that assign it different meanings.

## Nudge

One model is serving incompatible bounded contexts. Give each context its own concept and translate explicitly.
