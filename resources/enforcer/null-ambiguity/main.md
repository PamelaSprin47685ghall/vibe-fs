# null-ambiguity — Main

## What To Do Now
Replace ambiguous null/optional results with explicit alternatives for every absence reason that changes caller behavior.

## Why This Matters
Once distinct outcomes are collapsed into "no value," information is irreversibly lost. Downstream layers compensate with flags, status inspection, retries, or prose parsing, creating a web of heuristics around a distinction the producer already knew and failed to preserve.

## Repair Strategy
Name the domain outcomes, return them as a closed result type, and let adapters translate them to transport/UI representations. Keep a plain option only where one notion of absence is truly sufficient.

## Decision Branches
- If callers must act differently for different absences, return a closed result that names each reason.
- If there is one notion of optionality, keep `Option` and do not invent extra cases.

## Common Wrong Fixes
- Add another boolean such as `wasUnauthorized` beside a nullable value, recreating contradictory product states.
- Encode the reason in an error string that callers must parse.
- Map every absence to a generic exception and lose the distinction again.

## Verification
Every caller should choose behavior by matching a named outcome, not by combining null checks with contextual clues. The invariant is that every semantically distinct absence remains distinguishable at the boundary.

## Done When
The return type preserves all semantically relevant absence information at the point where it is still known, and no downstream code has to infer why the value is missing.
