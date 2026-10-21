import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const prefix = await import("../../../dist/Context/Prefix/Surface.js");

const reanchor = (state, { previousEpoch, nextEpoch, observedRun = 'msg_compaction' }) =>
  prefix.applyReanchor({ previousEpoch, nextEpoch, observedRun }, state)

test('WHAT[prefix-stability-006] HOST_006_reanchor_advances_the_epoch_and_records_the_compaction', () => {
  const result = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1 })
  assert.equal(result.ok, true, result.ok ? '' : result.error)

  // This is a real cold boundary — the provider-visible prefix changed and the
  // seal barrier broke — and COMPANION-009's byte-stability guarantee is scoped
  // to one epoch, so staying put would state something false.
  assert.equal(prefix.epochOf(result.value), 1n)

  // The compaction that caused it is recorded, so the same observation cannot act
  // twice.
  assert.deepEqual(prefix.reanchoredRuns(result.value), ['msg_compaction'])
})
test('WHAT[prefix-stability-006] HOST_006_reanchoring_a_session_that_never_promoted_still_advances', () => {
  // A manual /compact on a session with no committed prefix. Nothing to retire,
  // but the cold boundary is just as real, and the epoch is what the frame
  // projection's coverage reset is paired with under one fact.
  const result = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1 })

  assert.equal(result.ok, true, result.ok ? '' : result.error)
  assert.equal(prefix.epochOf(result.value), 1n)
})
test('WHAT[prefix-stability-006] PERSIST_010_reanchor_epoch_must_be_the_successor', () => {
  for (const nextEpoch of [0, 3, 9]) {
    assert.deepEqual(
      reanchor(prefix.empty, { previousEpoch: 0, nextEpoch }),
      { ok: false, error: 'NonSequentialPrefixEpoch' },
      `nextEpoch ${nextEpoch} must be refused after epoch 0`,
    )
  }
})
test('WHAT[prefix-stability-006] HOST_006_the_same_compaction_is_never_reanchored_twice', () => {
  // Two observations of one pseudo-run must produce one retirement.
  const once = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1 }).value

  const replay = reanchor(once, { previousEpoch: 0, nextEpoch: 1 })
  assert.deepEqual(replay, { ok: false, error: 'CompactionAlreadyReanchored' })

  assert.equal(prefix.epochOf(once), 1n, 'the epoch did not move twice')
})
test('WHAT[prefix-stability-006] HOST_006_a_recorded_compaction_stays_refused_after_the_epoch_moves_on', () => {
  // The failure the recorded-run set exists for, and the reason the epoch check alone
  // is not enough.
  //
  // A compaction message stays in the Host transcript forever, so every later
  // reconcile observes it again. Once the epoch has advanced for an UNRELATED reason —
  // a tenure handover here — a freshly decided reanchor for that old compaction would
  // carry a `PreviousEpochId` that matches the current epoch. The epoch check would
  // accept it, the epoch would advance again, and the coverage the session had
  // legitimately rebuilt would be zeroed.
  const reanchored = reanchor(prefix.empty, { previousEpoch: 0, nextEpoch: 1, observedRun: 'msg_c1' }).value

  const handedOver =
    prefix.applyTenureReanchor(
      { previousEpoch: 1, nextEpoch: 2, incumbencyId: 'incumbent-1' },
      reanchored,
    ).value

  assert.equal(prefix.epochOf(handedOver), 2n)

  // A well-formed line for the OLD compaction, correct against the current epoch.
  const stale = reanchor(handedOver, { previousEpoch: 2, nextEpoch: 3, observedRun: 'msg_c1' })

  assert.deepEqual(stale, { ok: false, error: 'CompactionAlreadyReanchored' })
  assert.equal(prefix.epochOf(handedOver), 2n, 'the unrelated advance survives')
})
test('WHAT[prefix-stability-006] HOST_006_a_genuinely_new_compaction_reanchors_again', () => {
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
}

{
const assert = (await import('node:assert/strict')).default
const prefix = await import('../../../dist/Context/Prefix/Surface.js')
const contextFold = await import('../../../dist/Context/Companion/FoldSurface.js')
const session = 'ses_prefix_owner_fold'
let sequence = 0
const envelope = (caseName, payload) => ({
  runtime: 'rt_prefix_owner_fold',
  seq: ++sequence,
  observedAt: '2026-10-21T00:00:00Z',
  id: `prefix-owner-${sequence}`,
  session,
  run: null,
  fact: { family: 'Context', case: caseName, payload: { SessionId: session, ...payload } },
})
const reanchor = (previous, next, run) => envelope('ContextReanchored', {
  PreviousEpochId: previous, NextEpochId: next, ObservedCompactionRun: run,
})
const tenure = (previous, next, incumbency) => envelope('TenureReanchored', {
  PreviousEpochId: previous, NextEpochId: next, WorkId: 'work-prefix', IncumbencyId: incumbency,
})
const checkpoint = callId => envelope('ViewCheckpointCommitted', { ToolCallId: callId })
const folded = (lines, replay = false) => {
  const result = replay ? contextFold.replay(lines) : contextFold.fold(lines)
  assert.equal(result.ok, true, JSON.stringify(result.error))
  return result.value
}
const callsOf = value => (value.viewCheckpoints[session]?.checkpoints ?? []).map(item => item.callId)

test('WHAT[prefix-stability-006] absorbed reanchors preserve rebuilt checkpoints through canonical replay', () => {
  const lines = [
    reanchor('0', '1', 'compaction-1'),
    checkpoint('before-tenure'),
    tenure('1', '2', 'inc-1'),
    checkpoint('after-tenure'),
    reanchor('0', '1', 'stale-unrecorded-run'),
    reanchor('2', '3', 'compaction-1'),
    tenure('2', '3', 'inc-1'),
  ]
  for (const replay of [false, true]) {
    const value = folded(lines, replay)
    assert.deepEqual(callsOf(value), ['after-tenure'])
    assert.deepEqual(value.sessions[session].PrefixEpoch, {
      EpochId: 2n, ReanchoredRuns: ['compaction-1'], ReanchoredTenures: ['inc-1'],
    })
  }
})

test('WHAT[prefix-stability-006] only a committed successor clears checkpoints and corrupt lines fail the fold', () => {
  const initial = reanchor('0', '1', 'compaction-1')
  const second = reanchor('1', '2', 'compaction-2')
  const value = folded([initial, checkpoint('retired'), second])
  assert.deepEqual(callsOf(value), [])
  assert.deepEqual(value.sessions[session].PrefixEpoch.ReanchoredRuns, ['compaction-1', 'compaction-2'])
  const rejected = contextFold.fold([initial, checkpoint('retained'), reanchor('1', '3', 'corrupt-run')])
  assert.equal(rejected.ok, false)
  assert.match(JSON.stringify(rejected.error), /successor/)
})

test('WHAT[prefix-stability-006] owner proposals preserve exact int64 epochs and do not commit before append', () => {
  const state = { epoch: '9007199254740993', reanchoredRuns: ['handled'], reanchoredTenures: [] }
  const before = structuredClone(state)
  const candidate = prefix.proposeCompaction(['handled', 'new'], state)
  assert.deepEqual(candidate, { previousEpoch: 9007199254740993n, nextEpoch: 9007199254740994n, observedRun: 'new' })
  assert.deepEqual(state, before)
  const result = prefix.applyReanchor(candidate, state)
  assert.equal(result.ok, true)
  assert.equal(prefix.epochOf(result.value), 9007199254740994n)
  assert.deepEqual(prefix.reanchoredRuns(result.value), ['handled', 'new'])
  assert.equal(prefix.proposeCompaction(['handled', 'new'], result.value), null)
})

test('WHAT[prefix-stability-006] int64 maximum cannot propose a wrapped successor', () => {
  const state = { epoch: '9223372036854775807', reanchoredRuns: ['handled'], reanchoredTenures: [] }
  assert.equal(prefix.proposeCompaction(['handled'], state), null)
  assert.throws(() => prefix.proposeCompaction(['new'], state), /int64 domain/)
  assert.deepEqual(prefix.applyReanchor({ previousEpoch: state.epoch, nextEpoch: '-9223372036854775808', observedRun: 'new' }, state), {
    ok: false, error: 'NonSequentialPrefixEpoch',
  })
  assert.deepEqual(prefix.applyReanchor({ previousEpoch: '-9223372036854775808', nextEpoch: '-9223372036854775807', observedRun: 'other' }, state), {
    ok: false, error: 'StalePrefixEpoch',
  })
})
}
