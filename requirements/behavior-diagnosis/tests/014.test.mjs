import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const blog = await import("../../../dist/Enforcer/BlogSurface.js");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const cycle = ({ run = 'msg-e1', tipRuleId = 'enforcement-tip-1', fieldNameAtCommit = 'field-tip-1', toolCalls = [], evidenceRef } = {}) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blogger',
  run,
  toolCallIds: toolCalls,
  textRef: 'blob-e1',
  textDigest: 'sha-e1',
  tipRuleId,
  fieldNameAtCommit,
  evidenceRef,
  observedPrefixEpoch: 0,
})
const apply = (state, value, n = 1) => {
  const enforcement = observation.applyEnforcementCycle(state.enforcement, cycle(value))
  assert.equal(enforcement.ok, true, enforcement.ok ? '' : enforcement.error)
  const committed = observation.applyBlogEntry(
    {
      frameEpoch: 0,
      previousIngestedThroughSequence: n - 1,
      nextIngestedThroughSequence: n,
      previousCoverableTurnCutoffExclusive: n - 1,
      nextCoverableTurnCutoffExclusive: n,
      nextCoveredPrefixDigest: `d-${n}`,
    },
    observation.blogFrame({
      kind: 'Entry',
      digest: `sha-e${n}`,
      ref: `blob-e${n}`,
      coveredFrom: n - 1,
      coveredThrough: n,
    }),
    state.blog,
  )
  assert.equal(committed.ok, true, committed.ok ? '' : committed.error)
  return { enforcement: enforcement.value, blog: committed.value }
}

test('WHAT[BD-014] ENFORCER_045_duplicate_provider_run_rejected_by_fold', () => {
  const first = observation.applyEnforcementCycle(observation.emptyEnforcement, cycle({ run: 'msg-dup' }))
  assert.equal(first.ok, true)
  const duplicate = observation.applyEnforcementCycle(first.value, cycle({ run: 'msg-dup' }))
  assert.equal(duplicate.ok, false)
  assert.match(duplicate.error, /already recorded/)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const enforcer = await import("../../../dist/Enforcer/Surface.js");
const observation = await import("../../../dist/Enforcer/ObservationSurface.js");

const fields = enforcer.fieldNames()
const cycleRecord = (n, field) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blog',
  run: `msg_tip_${n}`,
  toolCallIds: [`call-${n}`],
  textRef: `blob-t${n}`,
  textDigest: `sha-t${n}`,
  tipRuleId: field,
  fieldNameAtCommit: field,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})

test('WHAT[BD-014] ENFORCER_TIP_08_each_committed_cycle_records_exactly_one_tip', () => {
  const applied = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, fields[0]))
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.deepEqual(observation.recentTips(applied.value), [{ ruleId: fields[0], fieldName: fields[0], cycleId: 'msg_tip_1' }])
})
test('WHAT[BD-014] ENFORCER_TIP_09_replay_preserves_tip', () => {
  const state = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, fields[2])).value
  const duplicate = observation.applyEnforcementCycle(state, cycleRecord(1, fields[2]))
  assert.equal(duplicate.ok, false)
  assert.equal(observation.recentTips(state)[0].cycleId, 'msg_tip_1')
})
test('WHAT[BD-014] ENFORCER_TIP_10_recent_tips_cap_at_8', () => {
  let state = observation.emptyEnforcement
  for (let n = 1; n <= 12; n += 1) {
    const applied = observation.applyEnforcementCycle(state, cycleRecord(n, fields[n % fields.length]))
    assert.equal(applied.ok, true)
    state = applied.value
  }
  const tips = observation.recentTips(state)
  assert.equal(tips.length, 8)
  assert.equal(tips[0].cycleId, 'msg_tip_5')
  assert.equal(tips[7].cycleId, 'msg_tip_12')
})
test('WHAT[BD-014] ENFORCER_TIP_11_recent_tips_order_oldest_to_newest', () => {
  let state = observation.emptyEnforcement
  for (let n = 1; n <= 3; n += 1) {
    const applied = observation.applyEnforcementCycle(state, cycleRecord(n, fields[n]))
    assert.equal(applied.ok, true)
    state = applied.value
  }
  assert.deepEqual(observation.recentTips(state).map((t) => t.cycleId), ['msg_tip_1', 'msg_tip_2', 'msg_tip_3'])
})
}
