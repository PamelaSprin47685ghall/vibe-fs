# missing-regression-test — Enforcer

## Definition
A regression test is missing when a defect is corrected without preserving an executable example that fails under the old behavior and passes under the repaired behavior.

## Governing Principle
A bug report is new knowledge about the system's reachable state space. Fixing the implementation removes the symptom; a regression test preserves the knowledge that this region of state space is dangerous. Without it, the team pays for the discovery once and then permits future refactors to forget it completely.

## Trigger When
Trigger when a concrete defect is fixed and no test reproduces the original failure through the relevant behavioral boundary.

## Do Not Trigger When
- An existing test already fails on the buggy behavior and was the evidence that drove the repair.
- The change is a pure refactor with no observed defect and no new reachable failure.
- The incident was an operational misconfiguration outside the product's behavioral contract.

## Distinguish From
ignored-tdd concerns going red first for all behavior changes. failure-path-untested concerns failure branches never exercised. Tie-break: if a known defect was fixed without an executable memory of that defect, this rule; if new behavior was written without going red first, ignored-tdd; if a failure branch has never been covered, failure-path-untested.

## Decision Procedure
Reproduce the bug in the smallest behavioral test before or alongside the fix. Confirm the old implementation fails for the reported reason, then require the corrected implementation to pass.

## Examples
- positive: A timezone bug is patched in parsing, but no test encodes the reported input that used to fail.
- near-miss: The failing property test already reproduced the bug and stayed in the suite after the fix.
- counterexample: Adding a feature with a new unit test is ordinary TDD, not a missing regression for a known defect.

## Nudge
A bug that taught the project nothing can return unchanged. Preserve the failure as a regression test so the repository remembers what humans had to discover.
