import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

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

test('WHAT[prefix-stability-012] invalid later prefix facts leave the supplied committed projection unchanged', () => {
  const committed = rebase(prefix.empty, { previousEpoch: 0, nextEpoch: 1, cutoff: 7 }).value
  const reanchored = reanchor(committed, { previousEpoch: 1, nextEpoch: 2, observedRun: 'msg_c1' }).value

  // A subsequent, ill-formed attempt (stale epoch / non-successor / replay of
  // the same compaction) is refused — and the committed reanchor stays exactly
  // as it was: epoch advanced once, snapshot retired, run recorded.
  assert.deepEqual(
    rebase(reanchored, { previousEpoch: 1, nextEpoch: 2, cutoff: 9 }),
    { ok: false, error: 'StalePrefixEpoch' },
  )
  assert.deepEqual(
    reanchor(reanchored, { previousEpoch: 2, nextEpoch: 3, observedRun: 'msg_c1' }),
    { ok: false, error: 'CompactionAlreadyReanchored' },
  )
  assert.deepEqual(
    rebase(reanchored, { previousEpoch: 2, nextEpoch: 4, cutoff: 9 }),
    { ok: false, error: 'NonSequentialPrefixEpoch' },
  )
  assert.equal(prefix.epochOf(reanchored), 2n)
  assert.equal(prefix.hasSnapshot(reanchored), false)
  assert.deepEqual(prefix.reanchoredRuns(reanchored), ['msg_c1'])
})

test.todo('WHAT[prefix-stability-012] committed rebase and reanchor survive actual later provider Failed and Aborted settlement plus journal reopen; GAP-106')
