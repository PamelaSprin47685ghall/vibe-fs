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

test('WHAT[PREFIX-STABILITY-012] PREFIX_STABILITY_committed_reanchor_survives_subsequent_failure', () => {
  // CTX-015 / HOST-006：已提交的 reanchor（ContextReanchored）与 rebase
  // （PrefixRebaseCommitted）不因后续 provider failure 回滚。投影层没有
  // provider 结局输入；「失败后回滚」在类别上不存在（同 CTX-010 的
  // 无 rollback 断言模式），且失败的重试（refusal）不触碰已提交状态。
  for (const forbidden of ['rollback', 'revert', 'undo', 'restore', 'clear', 'discard']) {
    assert.equal(typeof prefix[forbidden], 'undefined', `${forbidden} must not be an epoch API`)
  }

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
