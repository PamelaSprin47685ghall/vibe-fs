// Journal ObservationProjection: Blog + Enforcement as one paired view.
import assert from 'node:assert/strict'
import test from 'node:test'
import * as observation from '../../../dist/Enforcer/ObservationSurface.js'

const entryFrame = (n) => observation.blogFrame({ kind: 'Entry', digest: `sha-entry-${n}`, ref: `blob-entry-${n}` })
const squashFrame = (n) => observation.blogFrame({ kind: 'Squash', digest: `sha-squash-${n}`, ref: `blob-squash-${n}` })
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
const commitEntry = (state, { from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  observation.applyBlogEntry(
    {
      frameEpoch: 0,
      previousIngestedThroughSequence: from,
      nextIngestedThroughSequence: to,
      previousCoverableTurnCutoffExclusive: cutoffFrom,
      nextCoverableTurnCutoffExclusive: cutoffTo,
      nextCoveredPrefixDigest: digest,
    },
    entryFrame(n),
    state,
  )
const readObs = (enforcement, blog) => observation.observationsOf(enforcement, blog).map((o) => ({
  tipName: o.tipName,
  cycleId: o.cycleId,
  frameDigest: o.frameDigest,
}))

test('WHAT[BD-015] OBS_PROJ_001_empty_halves_yield_empty_observations', () => {
  assert.deepEqual(readObs(null, null), [])
  assert.deepEqual(readObs(observation.emptyEnforcement, observation.emptyBlog), [])
})

test('WHAT[BD-012] OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests', () => {
  let blog = observation.emptyBlog
  let enforcement = observation.emptyEnforcement

  for (let n = 1; n <= 2; n += 1) {
    const applied = observation.applyEnforcementCycle(enforcement, cycleRecord(n, `field-${n}`))
    assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
    enforcement = applied.value

    const entry = commitEntry(blog, {
      from: n - 1,
      to: n,
      cutoffFrom: n - 1,
      cutoffTo: n,
      n,
    })
    assert.equal(entry.ok, true, entry.ok ? '' : entry.error)
    blog = entry.value
  }

  assert.deepEqual(readObs(enforcement, blog), [
    { tipName: 'field-1', cycleId: 'msg_tip_1', frameDigest: 'sha-entry-1' },
    { tipName: 'field-2', cycleId: 'msg_tip_2', frameDigest: 'sha-entry-2' },
  ])
})

test('WHAT[BD-016] OBS_PROJ_003_squash_co_moves_tips_and_frames_as_observation', () => {
  let blog = observation.emptyBlog
  let enforcement = observation.emptyEnforcement

  for (let n = 1; n <= 3; n += 1) {
    const applied = observation.applyEnforcementCycle(enforcement, cycleRecord(n, `field-${n}`))
    assert.equal(applied.ok, true)
    enforcement = applied.value

    const entry = commitEntry(blog, {
      from: n - 1,
      to: n,
      cutoffFrom: n - 1,
      cutoffTo: n,
      n,
    })
    assert.equal(entry.ok, true)
    blog = entry.value
  }

  assert.equal(readObs(enforcement, blog).length, 3)

  const squashed = observation.applyBlogSquash(
    { previousFrameEpoch: 0, nextFrameEpoch: 1, coveredFrameCount: 2 },
    squashFrame(1),
    blog,
  )
  assert.equal(squashed.ok, true, squashed.ok ? '' : squashed.error)
  const after = observation.observationsAfterSquash(2, enforcement, squashed.value).map((o) => ({
    tipName: o.tipName,
    cycleId: o.cycleId,
    frameDigest: o.frameDigest,
  }))

  assert.deepEqual(observation.frameKinds(squashed.value), ['Squash', 'Entry'])
  assert.deepEqual(after, [
    { tipName: 'field-3', cycleId: 'msg_tip_3', frameDigest: 'sha-squash-1' },
  ])

  const tipsOnly = observation.applyEnforcementSquash(2, enforcement)
  assert.deepEqual(readObs(tipsOnly, squashed.value), after)
})

test('WHAT[BD-015] OBS_PROJ_004_unpaired_tip_when_blog_lags', () => {
  const applied = observation.applyEnforcementCycle(observation.emptyEnforcement, cycleRecord(1, 'solo-tip'))
  assert.equal(applied.ok, true)
  assert.deepEqual(readObs(applied.value, observation.emptyBlog), [
    { tipName: 'solo-tip', cycleId: 'msg_tip_1', frameDigest: undefined },
  ])
})
