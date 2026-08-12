# weakened-test-to-pass — Enforcer

## Definition
A test is weakened to pass when the implementation loses an argument with the specification, and the response is to make the witness less demanding.

The crucial question is not “did the test change?” Tests should change when contracts change. The defect is **direction of causality**: the expectation is relaxed *because the implementation cannot satisfy it*, without an independently authorized reason that the promise itself became weaker.

## Governing Principle
A meaningful test exists to make some implementations unacceptable.

When red appears, there are two legitimate possibilities:

1. the implementation violates the intended contract;
2. the intended contract has genuinely changed.

There is no third legitimate resolution called “make the assertion vague enough that current code passes.” That does not settle the disagreement. It deletes the witness.

This pathology is common in fast-moving and AI-assisted codebases because editing tests is mechanically as easy as editing production code. A system that can rewrite both the answer and the examiner can manufacture green on demand unless authority for changing the contract remains explicit.

## Trigger When
Trigger when a meaningful behavioral expectation is relaxed primarily in response to implementation failure, without an independently established contract change. Common forms:

- exact expected value becomes truthy/non-null/contains-something;
- an edge or failure case is deleted because new code mishandles it;
- a precise error type/status becomes “throws anything” or “returns some error”;
- ordering/idempotence/identity assertions are removed after a race or duplication bug appears;
- snapshot output is wholesale regenerated so an unintended change becomes the new expected state;
- assertions are commented out, skipped, marked flaky/xfail, or moved behind an environment condition to restore green;
- fixture inputs are simplified until the implementation no longer encounters the failing boundary;
- a test is rewritten to mirror current implementation rather than the externally owned promise.

## Do Not Trigger When
- Product/protocol/domain authority explicitly changed the contract, and the new test encodes that new promise for that reason.
- An assertion is removed because it froze private choreography that was never contractual, while caller-visible behavior remains protected; that is often a repair for `test-implementation-coupled`.
- The old test is discovered to be factually wrong about the contract, with authoritative evidence independent of the current failing implementation.
- A flaky mechanism is repaired by controlling time/order/state while preserving the same behavioral proposition.
- A test is strengthened, split, or rewritten to make the same contract easier to understand and harder to bypass.

## Distinguish From
`coverage-theater` starts with assertions too weak to carry the claim. `weakened-test-to-pass` has a meaningful witness and then disarms it under pressure.

`test-implementation-coupled` may justify deleting assertions, but only when those assertions constrain implementation accidents rather than the real contract. `scope-creep` is unrelated: a test may legitimately be removed because the owning requirement disappeared from scope, but that disappearance must come from the task/contract, not from inconvenience.

## Decision Procedure
Before accepting any relaxation, ask:

> What independently owned fact changed that makes the old expectation no longer required?

Require provenance: product decision, protocol specification, domain invariant revision, compatibility decision, or evidence that the old test misunderstood reality.

If the only answer is “the new implementation fails it,” the test is being weakened to pass.

Then check whether the new assertion still distinguishes the defect the old assertion caught. If not, the evidence has been degraded.

## Examples
- positive: `assert.equal(status, 409)` becomes `assert.ok(status >= 400)` because the implementation now returns 500.
- positive: a duplicate-request case is removed after a refactor loses idempotence.
- positive: a 300-line snapshot changes; the entire file is regenerated and committed with no account of which semantic differences are intended.
- positive: a failing test is marked skipped “temporarily” so release can proceed, with no independent requirement change.
- near-miss: the API contract intentionally changes from 409 to 422; the decision is recorded and tests are updated to 422.
- counterexample: a spy asserting a private helper call is deleted while a public output assertion remains; the contract never promised that helper.

## Nudge
Do not end a disagreement by making the witness forget what it saw.

Change the test because the contract changed — never because the implementation wants an easier examiner.
