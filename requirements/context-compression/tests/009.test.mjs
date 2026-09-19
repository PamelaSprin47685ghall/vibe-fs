import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");
const xwire = await import("../../../dist/Context/Prefix/XWireSurface.js");
const { budget } = await import("../../../dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js");

const requestKind = prefix.requestKind
const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => ({
  probeId: id,
  basedOnEpoch: 0,
  candidate: snapshotAt(cutoff),
})

test('WHAT[context-compression-009] CTX_011_a_refused_candidate_falls_back_to_the_committed_epoch_with_a_reason', () => {
  // The ordinary outcome when an allowed attempt has nothing to work with. The request still
  // goes out; only the reason is recorded, for diagnostics.
  const plan = planner.attemptPlan({
    kind: requestKind.workMain,
    mayRecover: true,
    noCandidateReason: 'NoCoverage',
  })

  assert.equal(plan.choice, 'UseCommittedEpoch')
  assert.equal(plan.probeId, null)
  assert.equal(plan.noProbeReason, 'NoCoverage')
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const compression = await import("../../../dist/Context/Companion/CompressionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const selection = prefix
const agreeing = (digest) => () => digest
const committedAt = (cutoff, { digest = `prefix-${cutoff}`, frozen = `frozen-${cutoff}`, seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: frozen,
    cutoff,
    prefixDigest: digest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

test('WHAT[context-compression-009] CTX_010_the_probe_records_the_epoch_it_was_built_from', () => {
  // `BasedOnEpochId` is what the promotion is validated against: a probe built while
  // epoch 3 was in force may only promote to 4. A probe that raced a concurrent
  // reanchor is then refused rather than applied to the wrong base.
  const result = selection.select({
    committedEpoch: 3,
    committedSnapshot: committedAt(2),
    coverableCutoff: 7,
    coveredDigest: 'p7',
    requestStartCutoff: 20,
    recomputeDigest: agreeing('p7'),
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.basedOnEpoch, 3n)
})
}
