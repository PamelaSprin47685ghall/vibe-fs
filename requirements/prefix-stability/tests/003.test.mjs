import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const planner = await import("../../../dist/Context/Companion/CompressionSurface.js");
const companion = await import("../../../dist/Context/Companion/ProjectionSurface.js");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

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

test('WHAT[prefix-stability-003] absent candidate selects the supplied committed snapshot', () => {
  const committed = snapshotAt(4)

  const failed = planner.attemptPlan({
    role: 'Engineer',
    tier: 'Fast',
    kind: 'WorkMain',
    mayRecover: true,
    noCandidateReason: 'NoCoverage',
  })

  assert.equal(failed.choice, 'UseCommittedEpoch')
  assert.equal(failed.probeId, null)
  assert.equal(failed.noProbeReason, 'NoCoverage')

  const next = prefix.forChoice({ kind: 'committed' }, committed, companion.memoryPreamble, 'B BODY')
  assert.equal(next.dropLeading, 4)
})
test('WHAT[prefix-stability-003] CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way', () => {
  // A probe is not a different kind of request — it is the same request with a
  // candidate prefix. Separate code paths would let the two drift, and CTX-012 requires
  // a promoted probe to be byte-identical to what the successful attempt sent.
  const candidate = snapshotAt(7, { seal: 'seal-candidate' })

  const asProbe = prefix.forChoice({ kind: 'probe', candidate }, null, companion.memoryPreamble, 'BODY')
  const asCommitted = prefix.forSnapshot(candidate, companion.memoryPreamble, 'BODY')

  assert.deepEqual(asProbe, asCommitted)
})
test('WHAT[prefix-stability-003] CTX_010_the_required_blob_follows_the_choice_not_the_committed_state', () => {
  // The failure this prevents: reading the COMMITTED snapshot's blob for a probe
  // attempt injects the old FrozenRecordPrefix under the candidate's synthetic id. The provider
  // sees a changed prefix, and no fold can detect it — both halves are individually
  // well-formed.
  const committed = snapshotAt(4)
  const candidate = snapshotAt(9)

  assert.equal(prefix.requiredBlob({ kind: 'committed' }, committed), 'blob-frozen-4')
  assert.equal(
    prefix.requiredBlob(
      { kind: 'probe', candidate: probeFor({ cutoff: 9 }).candidate },
      committed,
    ),
    'blob-frozen-9',
    'a probe attempt reads the CANDIDATE blob',
  )

  assert.equal(prefix.requiredBlob({ kind: 'committed' }, null), null, 'raw history needs no blob')
})
}

test.todo('WHAT[prefix-stability-003] actual failed probe leaves no prefix commit or rollback in durable history; missing export names do not prove this; GAP-106')
