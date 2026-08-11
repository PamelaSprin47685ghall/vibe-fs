# implicit-convention-magic — Main

## What To Do Now
Replace correctness-critical discovery conventions with explicit typed registration or a build-time contract that makes participation visible and checkable. The typed registration or mechanical completeness gate is who owns participation; filename, path, annotation, and discovery order are not.

## Why This Matters
Convention saves syntax by spending memory. The more correctness depends on hidden names, paths, and annotations, the more the codebase requires folklore to assemble. Failures become omissions rather than type errors: nothing happens because some invisible ritual was missed.

## Repair Strategy
Identify the relationship the convention encodes, represent it as data or a typed declaration, and have one owner validate completeness. Keep convention only as optional sugar that compiles down to the explicit model.

## Decision Branches
- If discovery currently carries correctness, replace it with explicit registration or a mechanical completeness gate.
- If convention is only navigation sugar over an already-checked model, leave the sugar and keep the model authoritative.

## Common Wrong Fixes
- Do not merely document more magic. Documentation can teach a convention but cannot make violating it impossible or even visible.
- Do not add linter comments that remind people of the filename rule while runtime still discovers by name.
- Do not scan more directories to “make the convention easier.” Wider discovery enlarges the invisible API.

## Verification
Rename, move, or omit a participant in a controlled fixture. The build/startup gate should fail with a precise contract error rather than silently changing runtime behavior. The invariant is: participation is visible and checked, not implied by path.

## Done When
Critical relationships are discoverable from code and checked mechanically, while directory shape and naming conventions no longer carry hidden semantic authority.
