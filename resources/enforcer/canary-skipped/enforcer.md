# canary-skipped — Enforcer

## Definition
A canary is skipped when correctness depends on behavior owned by a real Host, provider, runtime, or deployment environment, yet verification stops at mocks or documentation.

## Governing Principle
Undocumented external behavior is an empirical premise. No amount of internal proof can derive it, because the proposition is not owned by the code under test. When correctness depends on ordering, framing, identity, timing, or lifecycle supplied by another system, the final proof obligation must cross that system’s actual boundary.

## Trigger When
Trigger when a change relies on Host/provider behavior that is undocumented, weakly specified, or historically surprising and no narrow real-environment canary proves the assumption.

## Do Not Trigger When
Do not trigger when the dependency is fully specified by a stable contract already exercised by an equivalent contract test, or when the change cannot reach that boundary.

## Distinguish From
contract-test-missing concerns a declared boundary contract. release-ladder-skipped concerns verification order generally. This rule targets an empirical Host assumption that only the real system can settle.

## Decision Procedure
State the external assumption as a falsifiable sentence. Build the smallest real interaction that would disprove it. Treat the observation, not the mock, as authority.

## Nudge
When the premise belongs to the Host, ask the Host. Add a narrow real canary that can falsify the assumption before release.
