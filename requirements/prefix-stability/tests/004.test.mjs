import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");

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

test('WHAT[prefix-stability-004] CTX_011_promoted_cutoff_may_not_retreat', () => {
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 6 }).value

  const backwards = rebase(committed, { previousEpoch: 1, nextEpoch: 2, cutoff: 3 })
  assert.deepEqual(backwards, { ok: false, error: 'CutoffRetreated' })

  const forwards = rebase(committed, { previousEpoch: 1, nextEpoch: 2, cutoff: 9 })
  assert.equal(forwards.ok, true, forwards.ok ? '' : forwards.error)
})
test('WHAT[prefix-stability-004] CTX_011_same_cutoff_with_a_tighter_B_is_a_new_candidate', () => {
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
test('WHAT[prefix-stability-004] CTX_011_an_identical_candidate_is_reported_as_not_new', () => {
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
test('WHAT[prefix-stability-004] PERSIST_010_rebase_epoch_must_be_the_successor', () => {
  for (const nextEpoch of [0, 2, 5]) {
    assert.deepEqual(
      rebase(prefix.empty, { previousEpoch: 0, nextEpoch, cutoff: 3 }),
      { ok: false, error: 'NonSequentialPrefixEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})
}
