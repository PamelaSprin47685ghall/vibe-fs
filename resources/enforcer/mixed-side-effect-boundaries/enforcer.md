# mixed-side-effect-boundaries — Enforcer

Id: enforcement-c06 / Family: C / Ordinal: 26

## ScoreWhen

A single function or module simultaneously owns unrelated effects such as storage, network, process control, UI, Git, and policy decisions.

## Nudge

Unrelated side-effect boundaries are mixed together. Isolate each effect behind a narrow port and keep policy pure.
