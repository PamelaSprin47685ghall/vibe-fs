// Tip-v2 contract items: catalog, codec, RecentTips and squash co-move.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as enforcer from '../../../dist/Enforcer/Surface.js'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'

test('WHAT[BD-001] ENFORCER_TIP_01_catalog_has_exactly_120_valid_rules', () => {
  assert.equal(enforcer.ruleCount(), 120)
  assert.equal(enforcer.fieldNames().length, 120)
  assert.equal(new Set(enforcer.fieldNames()).size, 120)
})

test('WHAT[BD-001] ENFORCER_TIP_02_and_16_tip_enum_equals_catalog_fields_and_package', () => {
  const fields = enforcer.fieldNames()
  assert.deepEqual(fields, enforcer.rules().map((r) => r.fieldName))
  assert.equal(new Set(fields).size, 120)
})

test('WHAT[BD-008] ENFORCER_TIP_03_04_facade_surface_has_tip_not_numeric_scores', () => {
  const sample = enforcer.decodeCall({ text: 'x', tip: enforcer.fieldNames()[0] })
  assert.equal(sample.ok, true)
  assert.equal(typeof sample.value.tip.ruleId, 'string')
  assert.equal(sample.value.tip.fieldName in Object.fromEntries(enforcer.fieldNames().map((f) => [f, true])), true)
  assert.equal(sample.value.Scores, undefined)
})

test('WHAT[BD-006] ENFORCER_TIP_05_missing_tip_fails', () => {
  const r = enforcer.decodeCall({ text: 'entry' })
  assert.equal(r.ok, false)
  assert.equal(r.error, enforcer.missingTipError)
})

test('WHAT[BD-007] ENFORCER_TIP_06_unknown_tip_resolves_to_catalog', () => {
  const r = enforcer.decodeCall({ text: 'entry', tip: 'totally-unknown-field' })
  assert.equal(r.ok, true)
  assert.ok(enforcer.fieldNames().includes(r.value.tip.fieldName))
})

test('WHAT[BD-007] ENFORCER_TIP_07_valid_field_maps_rule_id_exactly', () => {
  const field = 'primitive-obsession'
  const rule = enforcer.tryFindByField(field)
  const r = enforcer.decodeCall({ text: 'entry', tip: field })
  assert.ok(rule)
  assert.equal(r.ok, true)
  assert.equal(r.value.tip.ruleId, rule.ruleId)
  assert.equal(r.value.tip.fieldName, field)
})

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
  const applied = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, enforcer.fieldNames()[0]))
  assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
  assert.deepEqual(observation.recentTips(applied.value), [{ ruleId: enforcer.fieldNames()[0], fieldName: enforcer.fieldNames()[0], cycleId: 'msg_tip_1' }])
})

test('WHAT[BD-014] ENFORCER_TIP_09_replay_preserves_tip', () => {
  const state = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, enforcer.fieldNames()[2])).value
  const duplicate = observation.applyEnforcementCycle(state, cycleRecord(1, enforcer.fieldNames()[2]))
  assert.equal(duplicate.ok, false)
  assert.equal(observation.recentTips(state)[0].cycleId, 'msg_tip_1')
})

test('WHAT[BD-014] ENFORCER_TIP_10_recent_tips_cap_at_8', () => {
  let state = observation.emptyEnforcement
  const fields = enforcer.fieldNames()
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
  const fields = enforcer.fieldNames()
  for (let n = 1; n <= 3; n += 1) {
    const applied = observation.applyEnforcementCycle(state, cycleRecord(n, fields[n]))
    assert.equal(applied.ok, true)
    state = applied.value
  }
  assert.deepEqual(observation.recentTips(state).map((t) => t.cycleId), ['msg_tip_1', 'msg_tip_2', 'msg_tip_3'])
})

test('WHAT[BD-016] ENFORCER_TIP_12_squash_co_truncates_recent_tips', () => {
  let state = observation.emptyEnforcement
  const fields = enforcer.fieldNames()
  for (let n = 1; n <= 2; n += 1) {
    const applied = observation.applyEnforcementCycle(state, cycleRecord(n, fields[n - 1]))
    assert.equal(applied.ok, true)
    state = applied.value
  }
  assert.deepEqual(observation.recentTips(observation.applyEnforcementSquash(1, state)).map((t) => t.cycleId), ['msg_tip_2'])
  assert.deepEqual(observation.recentTips(observation.applyEnforcementSquash(2, state)), [])
  assert.deepEqual(observation.recentTips(observation.applyEnforcementSquash(5, state)), [])
  assert.deepEqual(observation.recentTips(observation.applyEnforcementSquash(0, state)).map((t) => t.cycleId), ['msg_tip_1', 'msg_tip_2'])
})

test('WHAT[BD-015] ENFORCER_TIP_13_pairing_observation_units_preserves_discrete_identity', () => {
  const units = observation.pairTipsAndFrames(['tip-1', 'tip-2'], [{ digest: 'd1', body: 'b1' }])
  assert.equal(units.length, 2)
  assert.deepEqual(units[0], { tipName: 'tip-1', frameDigest: 'd1', frameBody: 'b1' })
  assert.deepEqual(units[1], { tipName: 'tip-2', frameDigest: null, frameBody: null })
})

test('WHAT[BD-015] ENFORCER_TIP_14_work_log_observations_drop_unpaired_frames_and_anchor_on_tips', () => {
  const obs = observation.ofTipsAndFrames(
    [{ tipName: 'tip-1', cycleId: 'c1' }],
    ['d1', 'd2', 'd3'],
  )
  assert.equal(obs.length, 1)
  assert.deepEqual(obs[0], { tipName: 'tip-1', cycleId: 'c1', frameDigest: 'd1' })
})

test('WHAT[BD-009] ENFORCER_TIP_15_assistant_step_classification_protocol', () => {
  const single = enforcer.classifyAssistantStep({
    messageId: 'msg-1',
    parts: [{ tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work' } } }],
  })
  assert.equal(single.acceptedCalls, 1)
  assert.equal(single.protocol, 'CommitCandidate')

  const zero = enforcer.classifyAssistantStep({
    messageId: 'msg-0',
    parts: [{ type: 'text', text: 'no tool' }],
  })
  assert.equal(zero.acceptedCalls, 0)
  assert.equal(zero.protocol, 'ProjectMessages')

  const multiple = enforcer.classifyAssistantStep({
    messageId: 'msg-2',
    parts: [
      { tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work1' } } },
      { tool: 'chronicle', state: { status: 'completed', input: { tip: 'primitive-obsession', text: 'work2' } } },
    ],
  })
  assert.equal(multiple.acceptedCalls, 2)
  assert.equal(multiple.protocol, 'ProtocolRepair')
})
