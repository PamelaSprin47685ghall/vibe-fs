# weakened-test-to-pass — Lesson

## Opinion

### Definition

A test is weakened to pass when the implementation loses an argument with the specification, and the response is to make the witness less demanding.

The crucial question is not “did the test change?” Tests should change when contracts change. The defect is **direction of causality**: the expectation is relaxed *because the implementation cannot satisfy it*, without an independently authorized reason that the promise itself became weaker.

### Governing Principle

A meaningful test exists to make some implementations unacceptable.

When red appears, there are two legitimate possibilities:

1. the implementation violates the intended contract;
2. the intended contract has genuinely changed.

There is no third legitimate resolution called “make the assertion vague enough that current code passes.” That does not settle the disagreement. It deletes the witness.

This pathology is common in fast-moving and AI-assisted codebases because editing tests is mechanically as easy as editing production code. A system that can rewrite both the answer and the examiner can manufacture green on demand unless authority for changing the contract remains explicit.

### Trigger When

Trigger when a meaningful behavioral expectation is relaxed primarily in response to implementation failure, without an independently established contract change. Common forms:

- exact expected value becomes truthy/non-null/contains-something;
- an edge or failure case is deleted because new code mishandles it;
- a precise error type/status becomes “throws anything” or “returns some error”;
- ordering/idempotence/identity assertions are removed after a race or duplication bug appears;
- snapshot output is wholesale regenerated so an unintended change becomes the new expected state;
- assertions are commented out, skipped, marked flaky/xfail, or moved behind an environment condition to restore green;
- fixture inputs are simplified until the implementation no longer encounters the failing boundary;
- a test is rewritten to mirror current implementation rather than the externally owned promise.

### Do Not Trigger When

- Product/protocol/domain authority explicitly changed the contract, and the new test encodes that new promise for that reason.
- An assertion is removed because it froze private choreography that was never contractual, while caller-visible behavior remains protected; that is often a repair for `test-implementation-coupled`.
- The old test is discovered to be factually wrong about the contract, with authoritative evidence independent of the current failing implementation.
- A flaky mechanism is repaired by controlling time/order/state while preserving the same behavioral proposition.
- A test is strengthened, split, or rewritten to make the same contract easier to understand and harder to bypass.

### Distinguish From

`coverage-theater` starts with assertions too weak to carry the claim. `weakened-test-to-pass` has a meaningful witness and then disarms it under pressure.

`test-implementation-coupled` may justify deleting assertions, but only when those assertions constrain implementation accidents rather than the real contract. `scope-creep` is unrelated: a test may legitimately be removed because the owning requirement disappeared from scope, but that disappearance must come from the task/contract, not from inconvenience.

### Decision Procedure

Before accepting any relaxation, ask:

> What independently owned fact changed that makes the old expectation no longer required?

Require provenance: product decision, protocol specification, domain invariant revision, compatibility decision, or evidence that the old test misunderstood reality.

If the only answer is “the new implementation fails it,” the test is being weakened to pass.

Then check whether the new assertion still distinguishes the defect the old assertion caught. If not, the evidence has been degraded.

### Examples

- positive: `assert.equal(status, 409)` becomes `assert.ok(status >= 400)` because the implementation now returns 500.
- positive: a duplicate-request case is removed after a refactor loses idempotence.
- positive: a 300-line snapshot changes; the entire file is regenerated and committed with no account of which semantic differences are intended.
- positive: a failing test is marked skipped “temporarily” so release can proceed, with no independent requirement change.
- near-miss: the API contract intentionally changes from 409 to 422; the decision is recorded and tests are updated to 422.
- counterexample: a spy asserting a private helper call is deleted while a public output assertion remains; the contract never promised that helper.

## Advice

### What To Do Now

Restore the strongest expectation that the independently owned contract still requires.

Then fix the implementation, not the examiner.

If the contract truly changed, establish that fact first and rewrite the test to the new promise with explicit provenance. The green result should follow the decision; the decision must not be reverse-engineered from what the current implementation happens to pass.

### Why This Matters

A test suite is one of the few places where the code is allowed to be told “no.”

If production code can make its own failing witnesses less demanding, the suite stops governing behavior and becomes a generated autobiography of whatever the implementation currently does. Every regression can be normalized into a new expectation. Green becomes infinitely obtainable and therefore nearly meaningless.

This is particularly acute when the same agent edits implementation and tests. Convenience collapses separation of powers. The remedy is not bureaucratic immobility; it is preserving a source of truth for **why the contract changed** that is independent of the failing code.

### Repair Strategy

Recover the behavioral proposition before editing anything:

- identify the original requirement, protocol, invariant, acceptance criterion, or caller dependency;
- determine whether that proposition still belongs to the current task/product;
- if it does, restore/preserve the test and repair production behavior;
- if it does not, record the authoritative reason and write the new proposition precisely;
- keep regression power: the old defective implementation should still fail for a contract-level reason unless the contract specifically legalized it.

When snapshots are involved, review semantic differences field by field. Accept only intended changes. Prefer targeted assertions for critical fields rather than turning “update snapshot” into a rubber stamp.

When a test was over-coupled to internals, remove the implementation detail and replace it with an observable contract assertion; do not merely delete pressure.

### Decision Branches

- **Requirement unchanged:** restore the expectation and fix implementation.
- **Requirement intentionally changed:** cite/record the new contract and rewrite the test narrowly to it.
- **Old test misunderstood the contract:** prove that from authoritative source, then correct the test; the current implementation's failure is not itself the proof.
- **Assertion constrains private implementation only:** replace it with caller-visible behavior rather than preserve ceremony.
- **Failure is nondeterministic:** fix nondeterminism; do not weaken the behavioral claim.
- **Release pressure is the only reason:** the work is not green. Keep the failure visible and make an explicit risk/waiver decision if such authority exists.

### Common Wrong Fixes

- Replace exact equality with truthiness, broad ranges, substring checks, or “does not throw.”
- Delete edge cases because “users probably won't do that” without a product boundary saying so.
- Mark tests skipped/xfail/flaky and continue counting them as evidence.
- Regenerate snapshots wholesale and rely on visual fatigue to hide unintended changes.
- Change fixtures to easier inputs so the difficult boundary disappears.
- Assert current implementation output as the expected value by importing/reusing the same production logic in the test.
- Add comments explaining why the weaker assertion is “good enough.” Commentary does not create contract authority.

### Verification

Prove that green remains adversarial.

Temporarily reintroduce the defective behavior that motivated the weakening. If the contract did **not** change, the repaired test must go red.

If the contract did change, construct a defect against the **new** promise and prove the rewritten test rejects it. A legitimate contract change changes which behavior is acceptable; it does not abolish the need for rejection power.

Invariant:

> The test suite constrains implementation according to an independently chosen contract; implementation failure cannot unilaterally redefine the contract.

### Done When

You can explain every relaxed expectation by pointing to a changed or corrected contract, not to a red build.

The implementation may disagree with the test. It may not edit the disagreement out of existence.

### Nudge

Do not end a disagreement by making the witness forget what it saw.

Change the test because the contract changed — never because the implementation wants an easier examiner.
