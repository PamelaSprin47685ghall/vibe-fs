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

// ── the initial state and the retired state are one state ───────────────────

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

// ── reanchor: retire, do not replace ───────────────────────────────────────

const reanchor = (state, { previousEpoch, nextEpoch, observedRun = 'msg_compaction' }) =>
  prefix.applyReanchor({ previousEpoch, nextEpoch, observedRun }, state)
