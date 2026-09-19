import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const magicTodo = await import("../../../dist/Mission/Obligation/Todo/MagicTodoSemanticSurface.js");

const t = (id) => id

test('WHAT[prefix-stability-004] PREFIX_STABILITY_lag1_rebase_consumes_one_previous_committed_locator', () => {
  assert.equal(magicTodo.requiresLag1Rebase(undefined), false, 'T1 has no committed predecessor')
  assert.equal(magicTodo.requiresLag1Rebase(t('T1')), true, 'later committed checkpoints have one lag-1 predecessor')
})
test('WHAT[prefix-stability-004] PREFIX_STABILITY_todo_checkpoint_commit_uses_the_existing_epoch_contract', () => {
  const commit = magicTodo.buildTodoCheckpointCommit({
    sessionId: 'ses_1',
    managerLifeId: 'life-1',
    previousEpoch: 2,
    snapshot: {
      ref: 'blob-frozen',
      frozenDigest: 'frozen-1',
      cutoff: 5,
      prefixDigest: 'prefix-5',
      sealRoot: 'seal-5',
      syntheticId: 'synthetic-5',
    },
    previousCommitted: t('T1'),
    trigger: t('T2'),
    yBundleRef: 'blob-y',
    yBundleDigest: 'y-1',
    providerPrefixDigest: 'provider-prefix-digest',
  })

  assert.equal(commit.sessionId, 'ses_1')
  assert.equal(commit.managerLifeId, 'life-1')
  assert.equal(commit.previousEpoch, 2n)
  assert.equal(commit.nextEpoch, 3n)
  assert.deepEqual(commit.evidenceKind, {
    kind: 'TodoCheckpoint',
    triggerTodoWriteId: 'T2',
    coveredBeforeTodoWriteId: 'T1',
  })
  assert.equal(commit.cutoffExclusive, 5)
  assert.equal(commit.coveredPrefixDigest, 'prefix-5')
  assert.equal(commit.sealRoot, 'seal-5')
  assert.equal(commit.syntheticMessageId, 'synthetic-5')
  assert.equal(commit.frozenRecordPrefixRef, 'blob-frozen')
  assert.equal(commit.frozenRecordPrefixDigest, 'frozen-1')
  assert.equal(commit.yBundleRef, 'blob-y')
  assert.equal(commit.yBundleDigest, 'y-1')
  assert.equal(commit.providerPrefixDigest, 'provider-prefix-digest')
  assert.equal(commit.solvingProviderRun, null)
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
