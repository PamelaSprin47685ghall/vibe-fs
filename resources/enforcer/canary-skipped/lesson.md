# canary-skipped — Lesson

## Opinion

### Definition

A canary is skipped when correctness depends on behavior owned by a real Host, provider, runtime, or deployment environment, yet verification stops at mocks or documentation. The root-cause is that an empirical Host/provider premise is treated as proven by mocks or comments, so release can rest on behavior only the real environment can settle.

### Governing Principle

Undocumented external behavior is an empirical premise. No amount of internal proof can derive it, because the proposition is not owned by the code under test. When correctness depends on ordering, framing, identity, timing, or lifecycle supplied by another system, the final proof obligation must cross that system’s actual boundary.

### Trigger When

Trigger when a change relies on Host/provider behavior that is undocumented, weakly specified, or historically surprising and no narrow real-environment canary proves the assumption.

### Do Not Trigger When

- The dependency is fully specified by a stable contract already exercised by an equivalent contract test.
- The change cannot reach that Host or provider boundary.
- The assumption is owned entirely by this codebase and does not depend on undocumented external behavior.
- A recent equivalent real-boundary canary already falsifies the same empirical premise for this change.

### Distinguish From

`contract-test-missing` concerns a declared boundary contract. `release-ladder-skipped` concerns verification order generally. This rule targets an empirical Host assumption that only the real system can settle. Tie-break: if the missing proof is “what the Host actually does” rather than “what we declared,” this rule owns the case.

### Decision Procedure

State the external assumption as a falsifiable sentence. Build the smallest real interaction that would disprove it. Treat the observation, not the mock, as authority.

### Examples

- positive: shipping a framing change based on mock Host replies and a comment about last quarter’s manual check.
- near-miss: the Host’s behavior is a versioned contract already proven by a contract test that would fail on incompatibility.
- counterexample: a narrow real canary against the Host that fails if the empirical assumption is false.

## Advice

### What To Do Now

Write the external assumption down precisely and test it against the real Host or provider with the smallest safe canary that can prove it false. The real Host or provider is who owns the undocumented empirical behavior; a narrow live canary is who owns the proof that the assumption still holds.

### Why This Matters

Mocks prove what we told them to do. A canary proves what the other system actually does. If the behavior is undocumented, the gap between those two statements is the entire risk. Internal tests can be perfectly green while resting on a false premise about framing, ordering, identity, or lifecycle.

### Repair Strategy

Keep pure and contract tests for fast reasoning, then add one narrow real-boundary check for the irreducible empirical assumption. Make the canary stable, cheap, and explicit about what observation constitutes failure.

### Decision Branches

- If the premise is undocumented Host behavior, add a narrow real canary before release.
- If a stable declared contract already has an equivalent failing-capable test, do not duplicate a live canary for that same declared fact.
- If the change never reaches the Host boundary, skip the canary; the empirical premise is not in play.

### Common Wrong Fixes

- Do not widen mocks until they mimic the behavior you hope exists.
- Do not cite comments or historical observations as current proof when the environment is the authority.
- Do not run a broad end-to-end suite that never asserts the specific empirical fact.
- Do not treat a passing staging deploy with no targeted observation as a canary.

### Verification

The canary must fail if the external assumption changes while the internal implementation remains untouched. The invariant is that no release depends on an untested empirical premise owned only by the real Host.

### Done When

The release no longer depends on an untested empirical premise: lower layers prove internal logic, and the real boundary proves the behavior only it can own.

### Nudge

When the premise belongs to the Host, ask the Host. Add a narrow real canary that can falsify the assumption before release.
