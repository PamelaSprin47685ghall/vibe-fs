# false-gate — Enforcer

## Definition
A gate is false when its green state does not logically imply that the property it claims to guard was actually checked.

## Governing Principle
A quality gate has value only if there exists a reachable bad state that forces it red. If the scanner points at the wrong path, matches nothing, ignores exit status, or asserts a tautology, the gate is ceremonial: its success is independent of the property. A lock that cannot lock is worse than no lock because it manufactures confidence.

## Trigger When
Trigger when a test/check/gate can pass after inspecting zero relevant subjects, swallowing failures, scanning the wrong surface, or evaluating a condition that cannot become false.

## Do Not Trigger When
Do not trigger when the gate has a self-test or fixture proving known violations turn it red and the changed scope remains covered.

## Distinguish From
coverage-theater mistakes execution for proof. tool-error-ignored bypasses a raised error. This rule concerns a guard whose structure cannot reliably signal violation at all.

## Decision Procedure
Construct the smallest known violation. If the gate still passes—or there is no way to create a counterexample inside its intended scope—the gate is not enforcing the property.

## Nudge
Prove the guard can fail. Seed a known violation or fixture and require the gate to turn red before trusting any green result.
