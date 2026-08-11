# missing-architecture-gate — Main

## What To Do Now
Encode the architecture rule as a deterministic repository check and add it to the standard verification entry point. The command CI actually runs is who owns the architecture invariant: forbidden edges must be unmergeable, not merely unwelcome in review.

## Why This Matters
A boundary enforced only in review survives exactly until a rushed change, unfamiliar contributor, or innocently convenient import crosses it. The violation is usually cheap locally and expensive globally, so relying on memory places the cost signal at the wrong time.

## Repair Strategy
Define the forbidden graph edge or structural condition precisely, implement the smallest static check, and include a known-bad fixture or self-test proving the gate can turn red.

## Decision Branches
- If a cheap deterministic predicate can recognize the forbidden graph or ownership shape, add it to the standard gate with a known-bad fixture.
- If the rule is semantic and would fail open or flood false positives, record the invariant instead of pretending a gate exists.

## Common Wrong Fixes
- Replace enforcement with more documentation, comments, or reviewer checklists when the property is mechanically decidable.
- Add a gate that scans nothing, matches nothing, or fails open.
- Encode the rule only in a local script that CI never runs.

## Verification
A representative violation must fail the same command CI uses; valid architecture must remain green with low false-positive cost. The invariant is that forbidden edges are unmergeable.

## Done When
The repository itself refuses the forbidden dependency or ownership shape, so preserving the architecture no longer depends on everyone remembering the rule.
