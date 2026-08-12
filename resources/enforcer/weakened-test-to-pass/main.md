# weakened-test-to-pass — Main

## What To Do Now
Restore the strongest expectation that the independently owned contract still requires.

Then fix the implementation, not the examiner.

If the contract truly changed, establish that fact first and rewrite the test to the new promise with explicit provenance. The green result should follow the decision; the decision must not be reverse-engineered from what the current implementation happens to pass.

## Why This Matters
A test suite is one of the few places where the code is allowed to be told “no.”

If production code can make its own failing witnesses less demanding, the suite stops governing behavior and becomes a generated autobiography of whatever the implementation currently does. Every regression can be normalized into a new expectation. Green becomes infinitely obtainable and therefore nearly meaningless.

This is particularly acute when the same agent edits implementation and tests. Convenience collapses separation of powers. The remedy is not bureaucratic immobility; it is preserving a source of truth for **why the contract changed** that is independent of the failing code.

## Repair Strategy
Recover the behavioral proposition before editing anything:

- identify the original requirement, protocol, invariant, acceptance criterion, or caller dependency;
- determine whether that proposition still belongs to the current task/product;
- if it does, restore/preserve the test and repair production behavior;
- if it does not, record the authoritative reason and write the new proposition precisely;
- keep regression power: the old defective implementation should still fail for a contract-level reason unless the contract specifically legalized it.

When snapshots are involved, review semantic differences field by field. Accept only intended changes. Prefer targeted assertions for critical fields rather than turning “update snapshot” into a rubber stamp.

When a test was over-coupled to internals, remove the implementation detail and replace it with an observable contract assertion; do not merely delete pressure.

## Decision Branches
- **Requirement unchanged:** restore the expectation and fix implementation.
- **Requirement intentionally changed:** cite/record the new contract and rewrite the test narrowly to it.
- **Old test misunderstood the contract:** prove that from authoritative source, then correct the test; the current implementation's failure is not itself the proof.
- **Assertion constrains private implementation only:** replace it with caller-visible behavior rather than preserve ceremony.
- **Failure is nondeterministic:** fix nondeterminism; do not weaken the behavioral claim.
- **Release pressure is the only reason:** the work is not green. Keep the failure visible and make an explicit risk/waiver decision if such authority exists.

## Common Wrong Fixes
- Replace exact equality with truthiness, broad ranges, substring checks, or “does not throw.”
- Delete edge cases because “users probably won't do that” without a product boundary saying so.
- Mark tests skipped/xfail/flaky and continue counting them as evidence.
- Regenerate snapshots wholesale and rely on visual fatigue to hide unintended changes.
- Change fixtures to easier inputs so the difficult boundary disappears.
- Assert current implementation output as the expected value by importing/reusing the same production logic in the test.
- Add comments explaining why the weaker assertion is “good enough.” Commentary does not create contract authority.

## Verification
Prove that green remains adversarial.

Temporarily reintroduce the defective behavior that motivated the weakening. If the contract did **not** change, the repaired test must go red.

If the contract did change, construct a defect against the **new** promise and prove the rewritten test rejects it. A legitimate contract change changes which behavior is acceptable; it does not abolish the need for rejection power.

Invariant:

> The test suite constrains implementation according to an independently chosen contract; implementation failure cannot unilaterally redefine the contract.

## Done When
You can explain every relaxed expectation by pointing to a changed or corrected contract, not to a red build.

The implementation may disagree with the test. It may not edit the disagreement out of existence.
