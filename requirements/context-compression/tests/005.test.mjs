import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const policy = await import("../../../dist/Host/Contract/CompactionPolicySurface.js");

const settings = policy.requiredSettings()
const nextReanchor = (observed, reanchored) => {
  const handled = new Set(reanchored)
  return policy.nextReanchor(observed, (runId) => handled.has(runId))
}

test('WHAT[CONTEXT-COMPRESSION-005] CTX_005_containment_does_not_discriminate_by_source', () => {
  // A user's /compact and an unexpected Host compaction get identical handling, so
  // there is no "which kind" parameter and no branch for it. This asserts the shape of
  // the signature: a single-argument predicate with no source input.
  assert.equal(policy.isContainableCompaction.length, 1)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const failureOwner = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const budget = failureOwner.budget

test('WHAT[CONTEXT-COMPRESSION-005] every recorded failure consumes exactly one budget unit', () => {
  // The budget API takes no reason or error text: failures are never classified.
  assert.equal(budget.recordFailure(budget.initial).failures, 1)
  assert.equal(budget.isValidRecord(0, 1), true)
  assert.equal(budget.isValidRecord(0, 2), false)
  assert.equal(budget.isValidRecord(3, 4), true)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");

const terminalValidity = {
  check: compression.terminalValidityCheck,
  isValid: compression.terminalValidityIsValid,
  describe: compression.terminalValidityDescription,
}

test('WHAT[CONTEXT-COMPRESSION-005] CTX_005_validity_does_not_depend_on_failure_cause', () => {
  // The predicate must not treat provider error prose as a signal. A completed
  // response that happens to discuss an overflow is still a valid result, and a
  // failed attempt is not this function's business at all.
  const discussesOverflow =
    'The request failed with context_overflow earlier; I have summarised the findings instead.'

  assert.equal(terminalValidity.isValid(discussesOverflow), true)
})
}
