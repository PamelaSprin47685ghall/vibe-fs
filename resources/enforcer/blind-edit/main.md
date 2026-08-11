# blind-edit — Main

## What To Do Now
Stop changing the visible symptom. Read the owner, the contract around it, and the caller/data path until you can state a causal explanation for the defect.

## Why This Matters
The cheapest edit is not the fewest changed lines; it is the edit that restores the violated invariant without creating a second story elsewhere. Without ownership, every patch is a wager that the chosen file happens to be the source rather than merely a witness.

## Repair Strategy
Work backward from the observable failure to the governing contract, then forward through the implementation. Preserve known-correct structure and modify the smallest point that owns the wrong fact or transition.

## Wrong Fixes
Do not add downstream guards, adapters, or fallbacks because they make the symptom disappear. Do not infer an API or lifecycle from naming alone. A patch without a causal account is deferred debugging.

## Verification
The regression test should fail at the original behavior, pass after the owner-level repair, and make sense from the contract without knowledge of the patch.

## Done When
You can explain why the defect occurred, why this boundary owns the correction, and why the same mechanism cannot silently reappear downstream.
