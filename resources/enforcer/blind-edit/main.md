# blind-edit — Main

## What To Do Now
Stop changing the visible symptom. Read the owner, the contract around it, and the caller/data path until you can state a causal explanation for the defect. The contract at the first violated invariant is who owns the fix; downstream renderers and guards are not.

## Why This Matters
The cheapest edit is not the fewest changed lines; it is the edit that restores the violated invariant without creating a second story elsewhere. Without ownership, every patch is a wager that the chosen file happens to be the source rather than merely a witness.

## Repair Strategy
Work backward from the observable failure to the governing contract, then forward through the implementation. Preserve known-correct structure and modify the smallest point that owns the wrong fact or transition.

## Decision Branches
- If the owner and causal path are unknown, stop editing and map them first.
- If the owner is known and the first violated invariant is identified, edit there and nowhere downstream.
- If a downstream guard would hide the symptom, reject it; repair the source of the wrong fact.

## Common Wrong Fixes
- Do not add downstream guards, adapters, or fallbacks because they make the symptom disappear.
- Do not infer an API or lifecycle from naming alone.
- Do not spray similar patches across every file that mentions the symptom.
- Do not treat a green local tweak as proof of ownership without a causal account.

## Verification
The regression test should fail at the original behavior, pass after the owner-level repair, and make sense from the contract without knowledge of the patch. The invariant is that the edit restores the owning contract rather than silencing a witness of the violation.

## Done When
You can explain why the defect occurred, why this boundary owns the correction, and why the same mechanism cannot silently reappear downstream.
