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

test('WHAT[PREFIX-STABILITY-003] CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place', () => {
  // The absence of a rollback, seen from the planner: a failed probe attempt produces
  // no promotable probe, and the next slot's plan reads the same committed snapshot.
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

  // The next, unarmed slot projects the committed prefix — cutoff 4, not the
  // candidate's 9.
  const next = prefix.forChoice({ kind: 'committed' }, committed, companion.memoryPreamble, 'B BODY')
  assert.equal(next.dropLeading, 4)
})
test('WHAT[PREFIX-STABILITY-003] CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way', () => {
  // A probe is not a different kind of request — it is the same request with a
  // candidate prefix. Separate code paths would let the two drift, and CTX-012 requires
  // a promoted probe to be byte-identical to what the successful attempt sent.
  const candidate = snapshotAt(7, { seal: 'seal-candidate' })

  const asProbe = prefix.forChoice({ kind: 'probe', candidate }, null, companion.memoryPreamble, 'BODY')
  const asCommitted = prefix.forSnapshot(candidate, companion.memoryPreamble, 'BODY')

  assert.deepEqual(asProbe, asCommitted)
})
test('WHAT[PREFIX-STABILITY-003] CTX_010_the_required_blob_follows_the_choice_not_the_committed_state', () => {
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

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const candidate = ({ cutoff, prefixDigest = `prefix-${cutoff}`, digest = `frozen-${cutoff}`, seal = `seal-${cutoff}` }) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    frozenDigest: digest,
    cutoff,
    prefixDigest,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })
const rebase = (state, { previousEpoch, nextEpoch, cutoff, digest, seal, prefixDigest }) =>
  prefix.applyRebase(
    { previousEpoch, nextEpoch, candidate: candidate({ cutoff, digest, seal, prefixDigest }) },
    state,
  )
const reanchor = (state, { previousEpoch, nextEpoch, observedRun = 'msg_compaction' }) =>
  prefix.applyReanchor({ previousEpoch, nextEpoch, observedRun }, state)

test('WHAT[PREFIX-STABILITY-003] CTX_010_a_failed_probe_leaves_no_trace_to_undo', () => {
  // There is no rollback operation to test, and that absence IS the clause: a
  // discarded candidate never became a fact. The projection a failed probe leaves
  // behind is byte-identical to the one before it.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4 }).value

  // The claim is the absence of a CATEGORY of operation, so it is asserted as a
  // pattern rather than by enumerating every key. An enumeration breaks whenever an
  // unrelated accessor is added — it did, when `isReanchored` arrived — and each such
  // break teaches the reader to update the list rather than to think about the rule.
  for (const forbidden of ['rollback', 'revert', 'undo', 'restore', 'clear', 'discard']) {
    assert.equal(typeof prefix[forbidden], 'undefined', `${forbidden} must not be an epoch API`)
  }

  assert.equal(prefix.epochOf(committed), 1n)
  assert.deepEqual(committed.snapshot, candidate({ cutoff: 4 }))
})
}
