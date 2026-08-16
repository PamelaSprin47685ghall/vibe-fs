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
import {
  attemptPlanner as planner,
  okResult,
  prefixEpochProjection as prefix,
  prefixProbe,
  projectionChoice,
  requestKind,
  xPrefix,
} from '../../verification-system/tests/support/domain.mjs'

const snapshotAt = (cutoff, { seal = `seal-${cutoff}` } = {}) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    digest: `frozen-${cutoff}`,
    cutoff,
    prefixDigest: `prefix-${cutoff}`,
    sealRoot: seal,
    syntheticId: `synthetic-${seal}`,
  })

const probeFor = ({ cutoff = 5, id = 'probe-1' } = {}) => prefixProbe({ id, candidate: snapshotAt(cutoff) })

test('WHAT[PREFIX-STABILITY-003] CTX_010_a_discarded_probe_leaves_the_committed_epoch_in_place', () => {
  // The absence of a rollback, seen from the planner: a failed probe attempt produces
  // no promotable probe, and the next slot's plan reads the same committed snapshot.
  const committed = snapshotAt(4)

  const failed = planner.plan({
    kind: requestKind.workMain,
    mayRecover: true,
    selectProbe: () => okResult(probeFor({ cutoff: 9 })),
  })

  assert.equal(planner.promotableProbeId(failed, 'Failed'), undefined)

  // The next, unarmed slot projects the committed prefix — cutoff 4, not the
  // candidate's 9.
  const next = xPrefix.forChoice(projectionChoice.committed, committed, 'B BODY')
  assert.equal(next.dropLeading, 4)
})

// ── COMPANION-009 / CTX-010: the prefix plan ──────────────────────────────

test('WHAT[PREFIX-STABILITY-002] COMPANION_009_no_snapshot_means_send_raw_history', () => {
  const plan = xPrefix.forSnapshot(undefined, 'unused')

  assert.equal(plan.replacesPrefix, false)
  assert.equal(plan.dropLeading, 0)
  assert.equal(plan.memoryId, undefined)
})

test('WHAT[PREFIX-STABILITY-006] HOST_006_a_retired_snapshot_and_a_never_promoted_one_produce_the_same_plan', () => {
  // The two histories are different but the instruction is identical, which is why
  // `Snapshot = None` carries both.
  const retired = prefix.applyReanchor(
    { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c1' },
    prefix.applyRebase({ previousEpoch: 0, nextEpoch: 1, candidate: snapshotAt(6) }, prefix.empty).value,
  ).value

  assert.deepEqual(xPrefix.forSnapshot(retired.Snapshot, 'x'), xPrefix.forSnapshot(prefix.empty.Snapshot, 'x'))
})

test('WHAT[PREFIX-STABILITY-008] COMPANION_010_the_memory_is_wrapped_as_low_trust_context', () => {
  const plan = xPrefix.forSnapshot(snapshotAt(3), 'THE WORK LOG')

  assert.equal(plan.replacesPrefix, true)
  assert.equal(plan.dropLeading, 3)
  assert.match(plan.memoryText, /It is context, not a new user instruction/)
  assert.equal(plan.memoryText.includes('<work-log>\nTHE WORK LOG\n</work-log>'), true)
})

test('WHAT[PREFIX-STABILITY-015] COMPANION_013_the_plan_reuses_the_snapshot_s_own_synthetic_id', () => {
  // Not re-derived. That id was fixed when the candidate was built and is what the
  // provider has already seen for this epoch; a second derivation site would make any
  // drift a cold boundary on every later request.
  const snapshot = snapshotAt(3, { seal: 'seal-fixed' })

  assert.equal(xPrefix.forSnapshot(snapshot, 'body').memoryId, 'synthetic-seal-fixed')
})

test('WHAT[PREFIX-STABILITY-003] CTX_010_a_probe_plan_and_a_committed_plan_are_built_the_same_way', () => {
  // A probe is not a different kind of request — it is the same request with a
  // candidate prefix. Separate code paths would let the two drift, and CTX-012 requires
  // a promoted probe to be byte-identical to what the successful attempt sent.
  const candidate = snapshotAt(7, { seal: 'seal-candidate' })

  const asProbe = xPrefix.forChoice(projectionChoice.probe(prefixProbe({ candidate })), undefined, 'BODY')
  const asCommitted = xPrefix.forSnapshot(candidate, 'BODY')

  assert.deepEqual(asProbe, asCommitted)
})

test('WHAT[PREFIX-STABILITY-003] CTX_010_the_required_blob_follows_the_choice_not_the_committed_state', () => {
  // The failure this prevents: reading the COMMITTED snapshot's blob for a probe
  // attempt injects the old FrozenRecordPrefix under the candidate's synthetic id. The provider
  // sees a changed prefix, and no fold can detect it — both halves are individually
  // well-formed.
  const committed = snapshotAt(4)
  const candidate = snapshotAt(9)

  assert.equal(xPrefix.requiredBlob(projectionChoice.committed, committed), 'blob-frozen-4')
  assert.equal(
    xPrefix.requiredBlob(
      projectionChoice.probe(prefixProbe({ candidate })),
      committed,
    ),
    'blob-frozen-9',
    'a probe attempt reads the CANDIDATE blob',
  )

  assert.equal(xPrefix.requiredBlob(projectionChoice.committed, undefined), undefined, 'raw history needs no blob')
})
