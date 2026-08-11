# unverified-completion-claim — Main

## What To Do Now
Run the verification that corresponds to the promises changed by the work and report the observed outcomes before declaring completion.

## Why This Matters
A diff is a hypothesis about the desired system. Tests, builds, reproductions, and canaries are attempts to falsify that hypothesis. Skipping them turns implementation confidence into evidence and makes the completion statement stronger than what is actually known.

## Repair Strategy
Map each acceptance criterion to the lowest faithful check, execute applicable higher boundary checks where needed, and include failures or skipped stages explicitly rather than converting them into optimistic prose.

## Wrong Fixes
Do not cite code inspection alone when behavior can be executed, and do not report “should pass” as though it meant “passed.” Modal verbs are not test results.

## Verification
The evidence should be recent, relevant to the changed surface, and capable of failing under a realistic defect in that surface.

## Done When
The word “complete” summarizes an evidence chain already established rather than substituting for one.
