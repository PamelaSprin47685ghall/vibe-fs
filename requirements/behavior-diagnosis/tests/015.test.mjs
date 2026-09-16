// BD-015: Paired observation view of tips and frames
import assert from 'node:assert/strict'
import test from 'node:test'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'

const frame = (digest, body) => ({ digest, body })
const pair = (tips, frames) => observation.pairTipsAndFrames(tips, frames)
const workLog = (tips, frames) => observation.ofTipsAndFrames(
  tips.map(([tipName, cycleId]) => ({ tipName, cycleId })),
  frames,
)
const cycleRecord = (n, field) => ({
  mainSessionId: 'ses-main',
  bloggerSessionId: 'ses-blog',
  run: `msg_tip_${n}`,
  toolCallIds: [],
  textRef: `blob-cycle-${n}`,
  textDigest: `sha-cycle-${n}`,
  tipRuleId: field,
  fieldNameAtCommit: field,
  evidenceRef: undefined,
  observedPrefixEpoch: 0,
})
const readObs = (enforcement, blogState) => observation.observationsOf(enforcement, blogState).map((o) => ({
  tipName: o.tipName,
  cycleId: o.cycleId,
  frameDigest: o.frameDigest,
}))

test('WHAT[BD-015] RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame', () => {
  const units = pair(['tip-a', 'tip-b'], [frame('d1', 'body-1'), frame('d2', 'body-2')])
  assert.equal(units.length, 2)
  assert.equal(units[0].tipName, 'tip-a')
  assert.equal(units[0].frameDigest, 'd1')
  assert.equal(units[0].frameBody, 'body-1')
  assert.equal(units[1].tipName, 'tip-b')
  assert.equal(units[1].frameDigest, 'd2')
})

test('WHAT[BD-015] RULEBOOK_OBS_002_leftover_tips_append_unpaired', () => {
  const units = pair(['t1', 't2', 't3'], [frame('d1', undefined)])
  assert.equal(units.length, 3)
  assert.equal(units[0].tipName, 't1')
  assert.equal(units[0].frameDigest, 'd1')
  assert.equal(units[1].tipName, 't2')
  assert.equal(units[1].frameDigest, null)
  assert.equal(units[2].tipName, 't3')
  assert.equal(units[2].frameDigest, null)
})

test('WHAT[BD-015] RULEBOOK_OBS_003_leftover_frames_append_without_tip', () => {
  const units = pair(['t1'], [frame('d1', 'a'), frame('d2', 'b')])
  assert.equal(units.length, 2)
  assert.equal(units[0].tipName, 't1')
  assert.equal(units[1].tipName, null)
  assert.equal(units[1].frameDigest, 'd2')
  assert.equal(units[1].frameBody, 'b')
})

test('WHAT[BD-015] RULEBOOK_OBS_004_empty_inputs_yield_empty', () => {
  assert.deepEqual(pair([], []), [])
})

test('WHAT[BD-015] RULEBOOK_OBS_005_ofTipsAndFrames_pairs_cycle_and_digest', () => {
  const obs = workLog([
    ['boundary-type', 'run-1'],
    ['capability-isomorphism', 'run-2'],
  ], ['sha-a', 'sha-b'])
  assert.equal(obs.length, 2)
  assert.equal(obs[0].tipName, 'boundary-type')
  assert.equal(obs[0].cycleId, 'run-1')
  assert.equal(obs[0].frameDigest, 'sha-a')
  assert.equal(obs[1].tipName, 'capability-isomorphism')
  assert.equal(obs[1].cycleId, 'run-2')
  assert.equal(obs[1].frameDigest, 'sha-b')
})

test('WHAT[BD-015] RULEBOOK_OBS_006_ofTipsAndFrames_leftover_tips_keep_none_digest', () => {
  const obs = workLog([
    ['t1', 'c1'],
    ['t2', 'c2'],
    ['t3', 'c3'],
  ], ['d1'])
  assert.equal(obs.length, 3)
  assert.equal(obs[0].frameDigest, 'd1')
  assert.equal(obs[1].frameDigest, null)
  assert.equal(obs[2].frameDigest, null)
  assert.equal(obs[2].cycleId, 'c3')
})

test('WHAT[BD-015] RULEBOOK_OBS_007_ofTipsAndFrames_drops_leftover_frames', () => {
  const obs = workLog([['t1', 'c1']], ['d1', 'd2', 'd3'])
  assert.equal(obs.length, 1)
  assert.equal(obs[0].frameDigest, 'd1')
})

test('WHAT[BD-015] RULEBOOK_OBS_008_workLogFromUnits_uses_unit_digests', () => {
  const units = pair(['a', 'b'], [frame('d1', 'body'), frame('d2', undefined)])
  const obs = observation.workLogFromUnits(
    [
      { tipName: 'a', cycleId: 'c-a' },
      { tipName: 'b', cycleId: 'c-b' },
    ],
    units,
  )
  assert.equal(obs.length, 2)
  assert.equal(obs[0].tipName, 'a')
  assert.equal(obs[0].cycleId, 'c-a')
  assert.equal(obs[0].frameDigest, 'd1')
  assert.equal(obs[1].frameDigest, 'd2')
})

test('WHAT[BD-015] OBS_PROJ_001_empty_halves_yield_empty_observations', () => {
  assert.deepEqual(readObs(null, null), [])
  assert.deepEqual(readObs(observation.emptyEnforcement, observation.emptyBlog), [])
})

test('WHAT[BD-015] OBS_PROJ_004_unpaired_tip_when_blog_lags', () => {
  const applied = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, 'solo-tip'))
  assert.equal(applied.ok, true)
  assert.deepEqual(readObs(applied.value, observation.emptyBlog), [
    { tipName: 'solo-tip', cycleId: 'msg_tip_1', frameDigest: null },
  ])
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
