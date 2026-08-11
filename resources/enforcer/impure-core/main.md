# impure-core — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

Core business decisions directly read clocks, random sources, databases, networks, environment state, or mutable globals.

## What to do

Business policy is entangled with effects. Move effects to the shell and pass explicit values into a pure core.

## Reference

Family D, enforcement-d04, ordinal 34.
