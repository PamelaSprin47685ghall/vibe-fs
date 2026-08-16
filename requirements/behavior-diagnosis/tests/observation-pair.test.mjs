// Rulebook domain ObservationUnit / WorkLogObservation pairing
// (tips zip frames; no EventStore cutover).
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  RulebookObservation_pairTipsAndFrames as pairTipsAndFrames,
  RulebookObservation_ofTipsAndFrames as ofTipsAndFrames,
  RulebookObservation_workLogFromUnits as workLogFromUnits,
} from '../../../dist/Enforcer/Rulebook.js'
import { listItems, toList } from '../../verification-system/tests/support/domain.mjs'

const pair = (tips, frames) => listItems(pairTipsAndFrames(toList(tips), toList(frames)))

const tipCycles = (pairs) => toList(pairs.map(([name, cycle]) => [name, cycle]))
const digests = (xs) => toList(xs)

const workLog = (tips, frames) =>
  listItems(ofTipsAndFrames(tipCycles(tips), digests(frames)))

test('WHAT[BD-015] RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame', () => {
  const units = pair(
    ['tip-a', 'tip-b'],
    [
      ['d1', 'body-1'],
      ['d2', 'body-2'],
    ],
  )
  assert.equal(units.length, 2)
  assert.equal(units[0].TipName, 'tip-a')
  assert.equal(units[0].FrameDigest, 'd1')
  assert.equal(units[0].FrameBody, 'body-1')
  assert.equal(units[1].TipName, 'tip-b')
  assert.equal(units[1].FrameDigest, 'd2')
})

test('WHAT[BD-015] RULEBOOK_OBS_002_leftover_tips_append_unpaired', () => {
  const units = pair(['t1', 't2', 't3'], [['d1', undefined]])
  assert.equal(units.length, 3)
  assert.equal(units[0].TipName, 't1')
  assert.equal(units[0].FrameDigest, 'd1')
  assert.equal(units[1].TipName, 't2')
  assert.equal(units[1].FrameDigest, undefined)
  assert.equal(units[2].TipName, 't3')
  assert.equal(units[2].FrameDigest, undefined)
})

test('WHAT[BD-015] RULEBOOK_OBS_003_leftover_frames_append_without_tip', () => {
  const units = pair(['t1'], [
    ['d1', 'a'],
    ['d2', 'b'],
  ])
  assert.equal(units.length, 2)
  assert.equal(units[0].TipName, 't1')
  assert.equal(units[1].TipName, undefined)
  assert.equal(units[1].FrameDigest, 'd2')
  assert.equal(units[1].FrameBody, 'b')
})

test('WHAT[BD-015] RULEBOOK_OBS_004_empty_inputs_yield_empty', () => {
  assert.deepEqual(pair([], []), [])
})

test('WHAT[BD-015] RULEBOOK_OBS_005_ofTipsAndFrames_pairs_cycle_and_digest', () => {
  const obs = workLog(
    [
      ['boundary-type', 'run-1'],
      ['capability-isomorphism', 'run-2'],
    ],
    ['sha-a', 'sha-b'],
  )
  assert.equal(obs.length, 2)
  assert.equal(obs[0].TipName, 'boundary-type')
  assert.equal(obs[0].CycleId, 'run-1')
  assert.equal(obs[0].FrameDigest, 'sha-a')
  assert.equal(obs[1].TipName, 'capability-isomorphism')
  assert.equal(obs[1].CycleId, 'run-2')
  assert.equal(obs[1].FrameDigest, 'sha-b')
})

test('WHAT[BD-015] RULEBOOK_OBS_006_ofTipsAndFrames_leftover_tips_keep_none_digest', () => {
  const obs = workLog(
    [
      ['t1', 'c1'],
      ['t2', 'c2'],
      ['t3', 'c3'],
    ],
    ['d1'],
  )
  assert.equal(obs.length, 3)
  assert.equal(obs[0].FrameDigest, 'd1')
  assert.equal(obs[1].FrameDigest, undefined)
  assert.equal(obs[2].FrameDigest, undefined)
  assert.equal(obs[2].CycleId, 'c3')
})

test('WHAT[BD-015] RULEBOOK_OBS_007_ofTipsAndFrames_drops_leftover_frames', () => {
  // WorkLogObservation is tip-anchored; unpaired frames do not invent tips.
  const obs = workLog([['t1', 'c1']], ['d1', 'd2', 'd3'])
  assert.equal(obs.length, 1)
  assert.equal(obs[0].FrameDigest, 'd1')
})

test('WHAT[BD-015] RULEBOOK_OBS_008_workLogFromUnits_uses_unit_digests', () => {
  const units = pairTipsAndFrames(
    toList(['a', 'b']),
    toList([
      ['d1', 'body'],
      ['d2', undefined],
    ]),
  )
  const obs = listItems(
    workLogFromUnits(
      toList([
        ['a', 'c-a'],
        ['b', 'c-b'],
      ]),
      units,
    ),
  )
  assert.equal(obs.length, 2)
  assert.equal(obs[0].TipName, 'a')
  assert.equal(obs[0].CycleId, 'c-a')
  assert.equal(obs[0].FrameDigest, 'd1')
  assert.equal(obs[1].FrameDigest, 'd2')
})
