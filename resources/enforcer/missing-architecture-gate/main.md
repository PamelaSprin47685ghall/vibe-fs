# missing-architecture-gate — Main

## What To Do Now
Encode the architecture rule as a deterministic repository check and add it to the standard verification entry point.

## Why This Matters
A boundary enforced only in review survives exactly until a rushed change, unfamiliar contributor, or innocently convenient import crosses it. The violation is usually cheap locally and expensive globally, so relying on memory places the cost signal at the wrong time.

## Repair Strategy
Define the forbidden graph edge or structural condition precisely, implement the smallest static check, and include a known-bad fixture or self-test proving the gate can turn red.

## Wrong Fixes
Do not replace enforcement with more documentation, comments, or reviewer checklists when the property is mechanically decidable. Do not add a gate that scans nothing or fails open.

## Verification
A representative violation must fail the same command CI uses; valid architecture must remain green with low false-positive cost.

## Done When
The repository itself refuses the forbidden dependency or ownership shape, so preserving the architecture no longer depends on everyone remembering the rule.
