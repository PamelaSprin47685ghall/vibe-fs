# type-erosion-at-boundary — Main

Tip already selected by Enforcer. Next step: apply the nudge.

## Why

`any`, unchecked casts, reflection, dynamic property access, or unboxing escape the designated adapter boundary and enter domain logic.

## What to do

Type information is being discarded beyond the adapter boundary. Contain dynamic decoding and expose a typed contract.

## Reference

Family A, enforcement-a09, ordinal 9.
