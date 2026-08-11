# leftover-scaffolding — Main

## What To Do Now
Delete temporary artifacts whose transition is complete. If one now serves an enduring workflow, explicitly promote it into a maintained tool.

## Why This Matters
Scaffolding becomes dangerous by aging: future readers cannot distinguish “still required” from “forgotten,” so they preserve it defensively. The longer it survives, the more apparent authority it acquires without ever earning a stable contract.

## Repair Strategy
Review flags, scripts, fixtures, probes, temporary files, and migration helpers introduced by the work. Remove those with no ongoing consumer. For promoted artifacts, give them a clear name, normal test path, and owner.

## Decision Branches
- If the transition is complete and no ongoing user exists, delete the artifact.
- If the artifact now has a durable workflow, promote it with tests, docs, and an owner.

## Common Wrong Fixes
- Do not move temporary files into a `tools` or `legacy` directory merely to tidy the tree. A new folder does not create a permanent purpose.
- Do not comment out the probe “in case we need it.”
- Do not keep a flag whose only remaining value is that nobody remembers whether it is safe to remove.

## Verification
Search for references and documented workflows before removal; afterward, standard tests/builds should pass without relying on the transitional artifact. The invariant: every delivered artifact has a maintained purpose or is gone.

## Done When
Every delivered artifact either participates in a maintained workflow or is gone; nothing survives solely because nobody was certain it was safe to delete.
