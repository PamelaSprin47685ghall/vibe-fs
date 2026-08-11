// Journal ObservationProjection: Blog + Enforcement as Observation substrate.
// Physical facts stay BlogEntryCommitted / BlogSquashCommitted; this view only
// names the paired fold (rulebook BlogObservation* residual).
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  observationsOf,
  observationsAfterSquash,
} from '../../../dist/Journal/ObservationProjection.js'
import {
  blogProjection as blog,
  enforcementProjection as enf,
  listItems,
} from '../support/domain.mjs'

const entryFrame = (n) => blog.frame({ kind: 'Entry', digest: `sha-entry-${n}`, ref: `blob-entry-${n}` })
const squashFrame = (n) => blog.frame({ kind: 'Squash', digest: `sha-squash-${n}`, ref: `blob-squash-${n}` })

const commitEntry = (state, { epoch = 0, from, to, cutoffFrom, cutoffTo, digest = `digest-${cutoffTo}`, n = 1 }) =>
  blog.applyEntry(
    {
      epoch,
      previous: from,
      next: to,
      previousCutoff: cutoffFrom,
      nextCutoff: cutoffTo,
      digest,
      frame: entryFrame(n),
    },
    state,
  )

const cycleRecord = (n, field) =>
  enf.cycleRecord({
    mainSessionId: 'ses-main',
    bloggerSessionId: 'ses-blog',
    run: `msg_tip_${n}`,
    textRef: `blob-cycle-${n}`,
    textDigest: `sha-cycle-${n}`,
    tipRuleId: field,
    fieldNameAtCommit: field,
  })

const readObs = (enforcement, blogState) =>
  listItems(observationsOf(enforcement, blogState)).map((o) => ({
    tipName: o.TipName,
    cycleId: o.CycleId,
    frameDigest: o.FrameDigest,
  }))

test('OBS_PROJ_001_empty_halves_yield_empty_observations', () => {
  assert.deepEqual(readObs(undefined, undefined), [])
  assert.deepEqual(readObs(enf.empty, blog.empty), [])
})

test('OBS_PROJ_002_zip_recent_tips_with_blog_frame_digests', () => {
  let blogState = blog.empty
  let enfState = enf.empty

  for (let n = 1; n <= 2; n++) {
    const field = `field-${n}`
    const applied = enf.applyFromEntry(enfState, cycleRecord(n, field))
    assert.equal(applied.ok, true, applied.ok ? '' : applied.error)
    enfState = applied.value

    const entry = commitEntry(blogState, {
      from: n - 1,
      to: n,
      cutoffFrom: n - 1,
      cutoffTo: n,
      n,
    })
    assert.equal(entry.ok, true, entry.ok ? '' : entry.error)
    blogState = entry.value
  }

  assert.deepEqual(readObs(enfState, blogState), [
    { tipName: 'field-1', cycleId: 'msg_tip_1', frameDigest: 'sha-entry-1' },
    { tipName: 'field-2', cycleId: 'msg_tip_2', frameDigest: 'sha-entry-2' },
  ])
})

test('OBS_PROJ_003_squash_co_moves_tips_and_frames_as_observation', () => {
  // 1:1 Entry → tip. BlogSquashCommitted collapses oldest K frames; Enforcement
  // applySquash drops oldest K tips — Observation history moves together.
  let blogState = blog.empty
  let enfState = enf.empty

  for (let n = 1; n <= 3; n++) {
    const applied = enf.applyFromEntry(enfState, cycleRecord(n, `field-${n}`))
    assert.equal(applied.ok, true)
    enfState = applied.value

    const entry = commitEntry(blogState, {
      from: n - 1,
      to: n,
      cutoffFrom: n - 1,
      cutoffTo: n,
      n,
    })
    assert.equal(entry.ok, true)
    blogState = entry.value
  }

  assert.equal(readObs(enfState, blogState).length, 3)

  const squashedBlog = blog.applySquash(
    { previousEpoch: 0, nextEpoch: 1, count: 2, frame: squashFrame(1) },
    blogState,
  )
  assert.equal(squashedBlog.ok, true, squashedBlog.ok ? '' : squashedBlog.error)

  const after = listItems(observationsAfterSquash(2, enfState, squashedBlog.value)).map((o) => ({
    tipName: o.TipName,
    cycleId: o.CycleId,
    frameDigest: o.FrameDigest,
  }))

  // Tips: drop oldest 2 → keep field-3. Frames: [Squash, Entry(3)].
  // Front-zip pairs remaining tip with the new squash frame digest.
  assert.deepEqual(blog.frameKinds(squashedBlog.value), ['Squash', 'Entry'])
  assert.deepEqual(after, [
    { tipName: 'field-3', cycleId: 'msg_tip_3', frameDigest: 'sha-squash-1' },
  ])

  // Direct halves agree with afterSquash helper.
  const tipsOnly = enf.applySquash(2, enfState)
  assert.deepEqual(readObs(tipsOnly, squashedBlog.value), after)
})

test('OBS_PROJ_004_unpaired_tip_when_blog_lags', () => {
  let enfState = enf.empty
  const applied = enf.applyFromEntry(enfState, cycleRecord(1, 'solo-tip'))
  assert.equal(applied.ok, true)
  enfState = applied.value

  assert.deepEqual(readObs(enfState, blog.empty), [
    { tipName: 'solo-tip', cycleId: 'msg_tip_1', frameDigest: undefined },
  ])
})
