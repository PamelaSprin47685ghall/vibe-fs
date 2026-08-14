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

import assert from 'node:assert/strict'
import test from 'node:test'
import { prefixEpochProjection as prefix } from '../../verification-system/tests/support/domain.mjs'

const candidate = ({ cutoff, prefixDigest = `prefix-${cutoff}`, digest = `frozen-${cutoff}`, seal = `seal-${cutoff}` }) =>
  prefix.snapshot({
    ref: `blob-frozen-${cutoff}`,
    digest,
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

test('COMPANION_009_initial_epoch_has_no_snapshot', () => {
  assert.equal(prefix.epochOf(prefix.empty), 0n)
  assert.equal(prefix.hasSnapshot(prefix.empty), false)
})

// ── rebase: promote a probe's candidate ─────────────────────────────────────

test('CTX_012_successful_probe_promotes_its_candidate_verbatim', () => {
  const result = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' })

  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(prefix.epochOf(result.value), 1n)
  assert.equal(prefix.hasSnapshot(result.value), true)

  // COMPANION-013: the promoted SealRoot must be the one the successful request
  // used. Regenerating it would put a cold boundary between the request that
  // worked and the next one, which is the whole reason the candidate is passed
  // whole rather than field by field.
  assert.deepEqual(result.value.Snapshot, candidate({ cutoff: 4, seal: 'seal-P1' }))
})

test('CTX_011_promoted_cutoff_may_not_retreat', () => {
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 6 }).value

  const backwards = rebase(committed, { previousEpoch: 1, nextEpoch: 2, cutoff: 3 })
  assert.deepEqual(backwards, { ok: false, error: 'CutoffRetreated' })

  const forwards = rebase(committed, { previousEpoch: 1, nextEpoch: 2, cutoff: 9 })
  assert.equal(forwards.ok, true, forwards.ok ? '' : forwards.error)
})

test('CTX_011_same_cutoff_with_a_tighter_B_is_a_new_candidate', () => {
  // A Y squash makes B more compact without covering more X turns. Equal cutoff
  // plus a different FrozenRecordPrefix digest is therefore a legitimate promotion — this is
  // the case a naive "cutoff must increase" rule would wrongly reject.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 5, digest: 'frozen-wide' }).value

  const tighter = rebase(committed, {
    previousEpoch: 1,
    nextEpoch: 2,
    cutoff: 5,
    digest: 'frozen-squashed',
    prefixDigest: 'prefix-5',
  })

  assert.equal(tighter.ok, true, tighter.ok ? '' : tighter.error)
  assert.equal(prefix.epochOf(tighter.value), 2n)
})

test('CTX_011_an_identical_candidate_is_reported_as_not_new', () => {
  // Identity is (cutoff, prefix digest, FrozenRecordPrefix digest). CTX-011 already refuses
  // to BUILD such a probe, so a line carrying one is a replay. The projection
  // reports it rather than silently applying: promoting would spend an epoch and
  // a cold boundary for no change in what the model sees.
  //
  // Whether that report is fatal is Fold's decision, not this layer's — see
  // fold-context-recovery.test.mjs, which proves the fold absorbs it.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 5 }).value

  const identical = rebase(committed, { previousEpoch: 1, nextEpoch: 2, cutoff: 5 })
  assert.deepEqual(identical, { ok: false, error: 'CandidateNotNew' })

  // The projection is untouched by the refusal.
  assert.equal(prefix.epochOf(committed), 1n)
})

test('PERSIST_010_rebase_epoch_must_be_the_successor', () => {
  for (const nextEpoch of [0, 2, 5]) {
    assert.deepEqual(
      rebase(prefix.empty, { previousEpoch: 0, nextEpoch, cutoff: 3 }),
      { ok: false, error: 'NonSequentialPrefixEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})

test('CTX_012_a_replayed_rebase_is_reported_as_stale', () => {
  // CTX-012's recovery path re-attempts the commit after a restart: it cannot
  // know whether the append landed before the crash. The second attempt carries
  // the epoch it expected, which the projection has left.
  //
  // The projection says so; Fold turns that into an absorbed no-op. Keeping the
  // two layers distinct is what lets the frame projection treat the SAME shape of
  // refusal as fatal — see blog-projection.test.mjs.
  const once = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' }).value

  const replay = rebase(once, { previousEpoch: 0, nextEpoch: 1, cutoff: 4, seal: 'seal-P1' })
  assert.deepEqual(replay, { ok: false, error: 'StalePrefixEpoch' })

  assert.equal(prefix.epochOf(once), 1n)
  assert.deepEqual(once.Snapshot, candidate({ cutoff: 4, seal: 'seal-P1' }))
})

test('CTX_010_a_failed_probe_leaves_no_trace_to_undo', () => {
  // There is no rollback operation to test, and that absence IS the clause: a
  // discarded candidate never became a fact. The projection a failed probe leaves
  // behind is byte-identical to the one before it.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 4 }).value

  // The claim is the absence of a CATEGORY of operation, so it is asserted as a
  // pattern rather than by enumerating every key. An enumeration breaks whenever an
  // unrelated accessor is added — it did, when `isReanchored` arrived — and each such
  // break teaches the reader to update the list rather than to think about the rule.
  const rollbackShaped = Object.keys(prefix).filter((key) => /rollback|revert|undo|restore|clear|discard/i.test(key))

  assert.deepEqual(rollbackShaped, [], 'CTX-010 forbids a rollback: a failed probe was never committed')

  // The next slot after a discarded probe reads the same committed epoch.
  assert.equal(prefix.epochOf(committed), 1n)
  assert.deepEqual(committed.Snapshot, candidate({ cutoff: 4 }))
})

// ── reanchor: retire, do not replace ───────────────────────────────────────

const reanchor = (state, { previousEpoch, nextEpoch, observedRun = 'msg_compaction' }) =>
  prefix.applyReanchor({ previousEpoch, nextEpoch, observedRun }, state)

test('HOST_006_reanchor_retires_the_snapshot_and_advances_the_epoch', () => {
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 7 }).value

  const result = reanchor(committed, { previousEpoch: 1, nextEpoch: 2 })
  assert.equal(result.ok, true, result.ok ? '' : result.error)

  // Retirement, not replacement: the projection cannot repoint the cutoff at a
  // position after the Host summary, because that index belongs to the voided
  // numbering and the Companion may have been behind the Host when compaction
  // happened.
  assert.equal(prefix.hasSnapshot(result.value), false)
  assert.equal(result.value.Snapshot, undefined)

  // The epoch still advances. This is a real cold boundary — the provider-visible
  // prefix changed and the seal barrier broke — and COMPANION-009's byte-stability
  // guarantee is scoped to one epoch, so staying put would state something false.
  assert.equal(prefix.epochOf(result.value), 2n)

  // The compaction that caused it is recorded, so the same observation cannot act
  // twice.
  assert.deepEqual(prefix.reanchoredRuns(result.value), ['msg_compaction'])
})

test('HOST_006_reanchoring_a_session_that_never_promoted_still_advances', () => {
  // A manual /compact on a session with no committed snapshot. Nothing to retire,
  // but the cold boundary is just as real, and the epoch is what the frame
  // projection's coverage reset is paired with under one fact.
  const result = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1 })

  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(prefix.epochOf(result.value), 1n)
  assert.equal(prefix.hasSnapshot(result.value), false)
})

test('PERSIST_010_reanchor_epoch_must_be_the_successor', () => {
  for (const nextEpoch of [0, 3, 9]) {
    assert.deepEqual(
      reanchor(prefix.empty, { previousEpoch: 0, nextEpoch }),
      { ok: false, error: 'NonSequentialPrefixEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})

test('HOST_006_the_same_compaction_is_never_reanchored_twice', () => {
  // Two observations of one pseudo-run must produce one retirement.
  const once = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1 }).value

  const replay = reanchor(once, { previousEpoch: 0, nextEpoch: 1 })
  assert.deepEqual(replay, { ok: false, error: 'CompactionAlreadyReanchored' })

  assert.equal(prefix.epochOf(once), 1n, 'the epoch did not move twice')
})

test('HOST_006_a_recorded_compaction_stays_refused_after_the_epoch_moves_on', () => {
  // The failure the recorded-run set exists for, and the reason the epoch check alone
  // is not enough.
  //
  // A compaction message stays in the Host transcript forever, so every later
  // reconcile observes it again. Once the epoch has advanced for an UNRELATED reason —
  // a promoted probe here — a freshly decided reanchor for that old compaction would
  // carry a `PreviousEpochId` that matches the current epoch. The epoch check would
  // accept it, the epoch would advance again, and the coverage the session had
  // legitimately rebuilt would be zeroed.
  const reanchored = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1, observedRun: 'msg_c1' }).value
  const promoted = rebase(reanchored, { previousEpoch: 1, nextEpoch: 2, cutoff: 4 }).value

  assert.equal(prefix.epochOf(promoted), 2n)

  // A well-formed line for the OLD compaction, correct against the current epoch.
  const stale = reanchor(promoted, { previousEpoch: 2, nextEpoch: 3, observedRun: 'msg_c1' })

  assert.deepEqual(stale, { ok: false, error: 'CompactionAlreadyReanchored' })
  assert.equal(prefix.epochOf(promoted), 2n, 'the promoted prefix survives')
  assert.equal(prefix.hasSnapshot(promoted), true)
})

test('CTX_012_probe_capability_returns_after_a_reanchor', () => {
  // The reanchor is not a permanent shutdown. Once the Companion rebuilds
  // coverage in the new numbering, a probe promotes normally — from cutoff 1,
  // because the retired snapshot no longer imposes a floor.
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 20 }).value
  const retired = reanchor(committed, { previousEpoch: 1, nextEpoch: 2 }).value

  const rebuilt = rebase(retired, { previousEpoch: 2, nextEpoch: 3, cutoff: 1, seal: 'seal-new' })

  assert.equal(rebuilt.ok, true, rebuilt.ok ? '' : rebuilt.error)
  assert.equal(prefix.epochOf(rebuilt.value), 3n)
  assert.deepEqual(rebuilt.value.Snapshot, candidate({ cutoff: 1, seal: 'seal-new' }))

  // A rebase does not disturb the recorded compactions.
  assert.deepEqual(prefix.reanchoredRuns(rebuilt.value), ['msg_compaction'])
})

test('HOST_006_a_genuinely_new_compaction_reanchors_again', () => {
  // A second, different pseudo-run on an already-reanchored session. It must be
  // accepted, or a second manual /compact would leave the session pointing at a
  // numbering the transcript no longer has.
  const first = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1, observedRun: 'msg_c1' }).value

  const second = reanchor(first, { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c2' })
  assert.equal(second.ok, true, second.ok ? '' : second.error)
  assert.equal(prefix.epochOf(second.value), 2n)

  // Both compactions are now recorded, so neither can act again.
  assert.deepEqual(prefix.reanchoredRuns(second.value), ['msg_c1', 'msg_c2'])
  assert.equal(prefix.isReanchored('msg_c1', second.value), true)
  assert.equal(prefix.isReanchored('msg_c2', second.value), true)
  assert.equal(prefix.isReanchored('msg_c3', second.value), false)
})
