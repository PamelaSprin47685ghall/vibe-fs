# false-gate — Enforcer

## Definition
A gate is false when its green state does not logically imply that the property it claims to guard was actually checked.

## Governing Principle
A quality gate has value only if there exists a reachable bad state that forces it red. If the scanner points at the wrong path, matches nothing, ignores exit status, or asserts a tautology, the gate is ceremonial: its success is independent of the property. A lock that cannot lock is worse than no lock because it manufactures confidence.

## Trigger When
Trigger when a test/check/gate can pass after inspecting zero relevant subjects, swallowing failures, scanning the wrong surface, or evaluating a condition that cannot become false.

## Do Not Trigger When
- The gate has a self-test or fixture proving known violations turn it red and the changed scope remains covered.
- A test is weak but still can fail for the property it claims; that is `coverage-theater`, not a structurally unfalsifiable gate.
- A tool error is raised and ignored by a caller; that is `tool-error-ignored` once the detector itself can still go red.
- The check is intentionally advisory and is not presented as a passing quality gate.

## Distinguish From
`coverage-theater` mistakes execution for proof. `tool-error-ignored` bypasses a raised error. This rule concerns a guard whose structure cannot reliably signal violation at all. Tie-break: if a known violation in scope still yields green, this rule owns the case.

## Decision Procedure
Construct the smallest known violation. If the gate still passes—or there is no way to create a counterexample inside its intended scope—the gate is not enforcing the property.

## Examples
- positive: a lint gate greps a path that contains no sources, exits 0, and CI treats that as “lint passed.”
- near-miss: the same gate includes a fixture that must fail, and CI fails when that fixture is present.
- counterexample: add a known-bad fixture proving the gate turns red for the claimed property.

## Nudge
Prove the guard can fail. Seed a known violation or fixture and require the gate to turn red before trusting any green result.
