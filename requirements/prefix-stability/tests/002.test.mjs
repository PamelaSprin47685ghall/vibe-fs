import assert from 'node:assert/strict'
import test from 'node:test'
import * as planner from '../../../dist/Context/Companion/CompressionSurface.js'
import * as companion from '../../../dist/Context/Companion/ProjectionSurface.js'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

// Split from tests/unit/context/attempt-plan.test.mjs (cutover Wave 2a); owner: prefix-stability.
//
// CTX-010 / COMPANION-009/010/013 / HOST-006 epoch-related prefix-plan assertions:
// a discarded probe leaves the committed epoch in place, the probe plan and the
// committed plan are built the same way, Snapshot=None means raw history, a
// retired snapshot and a never-promoted one produce the same plan, the memory is
// wrapped as low-trust context, the plan reuses the snapshot's own synthetic id,
// and the required blob follows the choice not the committed state.



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

// tests/unit/Context/prefix-epoch.test.mjs — COMPANION-009 / CTX-012 / HOST-006.
//
// Which X prefix generation is in force, and the two facts that may change it.
//
// One asymmetry runs through this file and is the point of it: a stale frame
// epoch is fatal (see blog-projection.test.mjs) while a stale PREFIX epoch is
// absorbed. That is not an inconsistency. CTX-012's crash recovery deliberately
// re-attempts both a rebase and a reanchor after a restart, so a replayed line
// carrying an epoch the projection has left behind means "already applied".
// Idempotency falls out of the epoch check instead of needing a second dedupe
// mechanism that could disagree with it.


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

// ── the initial state and the retired state are one state ───────────────────

test('WHAT[PREFIX-STABILITY-002] COMPANION_009_no_snapshot_means_send_raw_history', () => {
  const plan = prefix.forSnapshot(null, companion.memoryPreamble, 'unused')

  assert.equal(plan.replacesPrefix, false)
  assert.equal(plan.dropLeading, 0)
  assert.equal(plan.memoryId, null)
})

test('WHAT[PREFIX-STABILITY-002] COMPANION_009_initial_epoch_has_no_snapshot', () => {
  assert.equal(prefix.epochOf(prefix.empty), 0n)
  assert.equal(prefix.hasSnapshot(prefix.empty), false)
})

// ── rebase: promote a probe's candidate ─────────────────────────────────────

test('WHAT[PREFIX-STABILITY-002] CTX_012_successful_probe_promotes_its_candidate_verbatim', () => {
  const result = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' })

  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(prefix.epochOf(result.value), 1n)
  assert.equal(prefix.hasSnapshot(result.value), true)

  // COMPANION-013: the promoted SealRoot must be the one the successful request
  // used. Regenerating it would put a cold boundary between the request that
  // worked and the next one, which is the whole reason the candidate is passed
  // whole rather than field by field.
  assert.deepEqual(result.value.snapshot, candidate({ cutoff: 4, seal: 'seal-P1' }))
})

test('WHAT[PREFIX-STABILITY-002] CTX_012_probe_capability_returns_after_a_reanchor', () => {
  // The reanchor is not a permanent shutdown. Once the Companion rebuilds
  // coverage in the new numbering, a probe promotes normally — from cutoff 1,
  // because the retired snapshot no longer imposes a floor.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 20 }).value
  const retired = reanchor(committed, { previousEpoch: 1, nextEpoch: 2 }).value

  const rebuilt = rebase(retired, { previousEpoch: 2, nextEpoch: 3, cutoff: 1, seal: 'seal-new' })

  assert.equal(rebuilt.ok, true, rebuilt.ok ? '' : rebuilt.error)
  assert.equal(prefix.epochOf(rebuilt.value), 3n)
  assert.deepEqual(rebuilt.value.snapshot, candidate({ cutoff: 1, seal: 'seal-new' }))

  // A rebase does not disturb the recorded compactions.
  assert.deepEqual(prefix.reanchoredRuns(rebuilt.value), ['msg_compaction'])
})
