# missing-regression-test — Enforcer

## Definition
A regression test is missing when a defect is corrected without preserving an executable example that fails under the old behavior and passes under the repaired behavior.

## Governing Principle
A bug report is new knowledge about the system’s reachable state space. Fixing the implementation removes the symptom; a regression test preserves the knowledge that this region of state space is dangerous. Without it, the team pays for the discovery once and then permits future refactors to forget it completely.

## Trigger When
Trigger when a concrete defect is fixed and no test reproduces the original failure through the relevant behavioral boundary.

## Do Not Trigger When
Do not trigger when an existing test already fails on the buggy behavior and was the evidence that drove the repair.

## Distinguish From
ignored-tdd concerns red-first order for all behavior changes. failure-path-untested concerns failure branches. This rule is the durable memory obligation created by a known defect.

## Decision Procedure
Reproduce the bug in the smallest behavioral test before or alongside the fix. Confirm the old implementation fails for the reported reason, then require the corrected implementation to pass.

## Nudge
A bug that taught the project nothing can return unchanged. Preserve the failure as a regression test so the repository remembers what humans had to discover.
