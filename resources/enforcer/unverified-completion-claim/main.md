# unverified-completion-claim — Main

## What To Do Now
Run the verification that corresponds to the promises changed by the work and report the observed outcomes before declaring completion. Do not announce done until who owns the claim also owns evidence that could have failed.

## Why This Matters
A diff is a hypothesis about the desired system. Tests, builds, reproductions, and canaries are attempts to falsify that hypothesis. Skipping them turns implementation confidence into evidence and makes the completion statement stronger than what is actually known.

## Repair Strategy
Map each acceptance criterion to the lowest faithful check, execute applicable higher boundary checks where needed, and include failures or skipped stages explicitly rather than converting them into optimistic prose.

## Decision Branches
If the work changed behavior, run the checks that can falsify the new promises and report observed results.
If verification could not be run, do not claim completion; report the missing evidence as remaining work.

## Common Wrong Fixes
- Cite code inspection alone when behavior can be executed.
- Report “should pass” as though it meant “passed.”
- Point at an old green pipeline from a different commit as current evidence.

## Verification
Invariant: “complete” summarizes an evidence chain that could have failed. Evidence must be recent, relevant to the changed surface, and capable of failing under a realistic defect in that surface.

## Done When
The word “complete” summarizes an evidence chain already established rather than substituting for one.
