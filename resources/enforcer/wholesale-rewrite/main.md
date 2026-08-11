# wholesale-rewrite — Main

## What To Do Now
Prefer the smallest edit that fixes the defect or lands the feature. Preserve known-good structure, names, and adjacent behavior.

## Repair Strategy
Revert sprawling rewrite hunks. Re-apply a minimal patch. Use structure-aware edits over regenerate-everything flows.

## Decision Branches
If local structure blocks a safe fix, extract a seam first—still smaller than a full rewrite. If rewrite is justified, get explicit scope and characterization tests first.

## Wrong Fixes
Regenerating whole files for a one-line fix. Delete-and-recreate to avoid reading code. Formatting-only churn mixed with behavior changes at file scale.

## Verification
Diff is reviewable and tied to acceptance; unrelated behavior remains intact under tests.

## Done When
The change is the smallest structurally correct patch; no unjustified wholesale rewrite remains.

## Scope and Authority
Edits to existing known-good codebases.
