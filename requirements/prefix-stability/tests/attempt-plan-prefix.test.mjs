// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX-010 / COMPANION-009/010/013 / HOST-006 epoch-related prefix-plan assertions:
// a discarded probe leaves the committed epoch in place, the probe plan and the
// committed plan are built the same way, Snapshot=None means raw history, a
// retired snapshot and a never-promoted one produce the same plan, the memory is
// wrapped as low-trust context, the plan reuses the snapshot's own synthetic id,
// and the required blob follows the choice not the committed state.

import assert from 'node:assert/strict'
import test from 'node:test'

import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
import * as companion from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

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
    role: 'Coder',
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

// ── COMPANION-009 / CTX-010: the prefix plan ──────────────────────────────

test('WHAT[PREFIX-STABILITY-002] COMPANION_009_no_snapshot_means_send_raw_history', () => {
  const plan = prefix.forSnapshot(null, companion.memoryPreamble, 'unused')

  assert.equal(plan.replacesPrefix, false)
  assert.equal(plan.dropLeading, 0)
  assert.equal(plan.memoryId, null)
})

test('WHAT[PREFIX-STABILITY-006] HOST_006_a_retired_snapshot_and_a_never_promoted_one_produce_the_same_plan', () => {
  // The two histories are different but the instruction is identical, which is why
  // `Snapshot = None` carries both.
  const rebased = prefix.applyRebase(
    { previousEpoch: 0, nextEpoch: 1, candidate: snapshotAt(6) },
    prefix.empty,
  ).value
  const retired = prefix.applyReanchor(
    { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c1' },
    rebased,
  ).value

  assert.deepEqual(
    prefix.forSnapshot(retired.snapshot, companion.memoryPreamble, 'x'),
    prefix.forSnapshot(null, companion.memoryPreamble, 'x'),
  )
})

test('WHAT[PREFIX-STABILITY-008] COMPANION_010_the_memory_returns_same_session_responsibility_as_instruction', () => {
  const plan = prefix.forSnapshot(snapshotAt(3), companion.memoryPreamble, 'THE WORK LOG')

  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 3)
  assert.match(plan.memoryText, /prior responsibility/)
  assert.match(plan.memoryText, /^# THE WORK LOG$/m)
  assert.doesNotMatch(plan.memoryText, /<work-log>|not a new user instruction/)
})

test('WHAT[PREFIX-STABILITY-015] COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id', () => {
  // Not re-derived. That id was fixed when the candidate was built and is what the
  // provider has already seen for this epoch; a second derivation site would make any
  // drift a cold boundary on every later request.
  const snapshot = snapshotAt(3, { seal: 'seal-fixed' })

  assert.equal(prefix.forSnapshot(snapshot, companion.memoryPreamble, 'body').memoryId, 'synthetic-seal-fixed')
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
