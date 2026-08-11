# canary-skipped — Enforcer

## Definition
A canary is skipped when correctness depends on behavior owned by a real Host, provider, runtime, or deployment environment, yet verification stops at mocks or documentation. The root-cause is that an empirical Host/provider premise is treated as proven by mocks or comments, so release can rest on behavior only the real environment can settle.

## Governing Principle
Undocumented external behavior is an empirical premise. No amount of internal proof can derive it, because the proposition is not owned by the code under test. When correctness depends on ordering, framing, identity, timing, or lifecycle supplied by another system, the final proof obligation must cross that system’s actual boundary.

## Trigger When
Trigger when a change relies on Host/provider behavior that is undocumented, weakly specified, or historically surprising and no narrow real-environment canary proves the assumption.

## Do Not Trigger When
- The dependency is fully specified by a stable contract already exercised by an equivalent contract test.
- The change cannot reach that Host or provider boundary.
- The assumption is owned entirely by this codebase and does not depend on undocumented external behavior.
- A recent equivalent real-boundary canary already falsifies the same empirical premise for this change.

## Distinguish From
`contract-test-missing` concerns a declared boundary contract. `release-ladder-skipped` concerns verification order generally. This rule targets an empirical Host assumption that only the real system can settle. Tie-break: if the missing proof is “what the Host actually does” rather than “what we declared,” this rule owns the case.

## Decision Procedure
State the external assumption as a falsifiable sentence. Build the smallest real interaction that would disprove it. Treat the observation, not the mock, as authority.

## Examples
- positive: shipping a framing change based on mock Host replies and a comment about last quarter’s manual check.
- near-miss: the Host’s behavior is a versioned contract already proven by a contract test that would fail on incompatibility.
- counterexample: a narrow real canary against the Host that fails if the empirical assumption is false.

## Nudge
When the premise belongs to the Host, ask the Host. Add a narrow real canary that can falsify the assumption before release.
