// Rulebook domain ObservationUnit pairing (tips zip frames; no EventStore cutover).
import assert from 'node:assert/strict'
import test from 'node:test'
import {
  RulebookObservation_pairTipsAndFrames as pairTipsAndFrames,
} from '../../../dist/Domain/RulebookObservation.js'
import { listItems } from '../support/domain.mjs'
import { ofArray } from '../../../dist/fable_modules/fable-library-js.5.13.0/List.js'

const pair = (tips, frames) => listItems(pairTipsAndFrames(ofArray(tips), ofArray(frames)))

test('RULEBOOK_OBS_001_zip_equal_length_pairs_tip_then_frame', () => {
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

test('RULEBOOK_OBS_002_leftover_tips_append_unpaired', () => {
  const units = pair(['t1', 't2', 't3'], [['d1', undefined]])
  assert.equal(units.length, 3)
  assert.equal(units[0].TipName, 't1')
  assert.equal(units[0].FrameDigest, 'd1')
  assert.equal(units[1].TipName, 't2')
  assert.equal(units[1].FrameDigest, undefined)
  assert.equal(units[2].TipName, 't3')
  assert.equal(units[2].FrameDigest, undefined)
})

test('RULEBOOK_OBS_003_leftover_frames_append_without_tip', () => {
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

test('RULEBOOK_OBS_004_empty_inputs_yield_empty', () => {
  assert.deepEqual(pair([], []), [])
})
