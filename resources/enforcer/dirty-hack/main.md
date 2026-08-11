# dirty-hack — Main

## What To Do Now
Remove the workaround and repair the model, ownership, or boundary whose mismatch made the workaround appear necessary.

## Why This Matters
A hack is expensive not because it is ugly but because it creates an undocumented exception to the system’s explanatory model. Future code must remember both the rule and the place where the rule is false. Enough such exceptions turn architecture into folklore.

## Repair Strategy
Trace the symptom to the first violated invariant. Change the representation or owner there, migrate callers, and delete the bypass rather than preserving it as fallback insurance.

## Decision Branches
- If the special case has no domain fact, repair the broken invariant/ownership and delete the bypass.
- If reality itself contains the exception, name it, own it, and test it as domain language rather than a secret `if`.
- If a facade is hiding many such patches, repair structure first; concealment is a different rule.

## Common Wrong Fixes
- Do not add another flag, adapter, catch, retry, or duplicate path around the workaround.
- Do not keep the hack as “fallback insurance” after the model is “mostly” fixed.
- Do not rename the bypass to sound domain-like without changing the model.
- Do not layer a facade over the special case so callers stop seeing the lie.

## Verification
The original failing behavior must be explained and covered by a regression test at the correct boundary. Removing the workaround alone should no longer reintroduce the defect. The invariant is that one coherent model explains the behavior without “except here.”

## Done When
There is one coherent model of the behavior, no special path exists solely to compensate for a known structural mistake, and the fix can be explained without “except here.”
