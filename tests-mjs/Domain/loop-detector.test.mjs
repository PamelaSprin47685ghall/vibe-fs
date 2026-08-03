// tests-mjs/Domain/loop-detector.test.mjs — LOOP-003/004/005/011 pure detector.
//
// Layer 1 only: no Host, no abort, no journal. The sensor/AABB bridge is a
// separate path; this file locks the metric and the O(1) state machine.

import assert from 'node:assert/strict'
import test from 'node:test'
import { loopDetector, loopEventCodec } from '../domain.mjs'

test('LOOP_004_constants_match_the_clause', () => {
  assert.equal(loopDetector.minChars, 256)
  assert.equal(loopDetector.loopThreshold, 6)
  assert.equal(loopDetector.hashBuckets, 4096)
  assert.equal(loopDetector.k, 3)
})

test('LOOP_003_short_stream_stays_warming_up', () => {
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'a'.repeat(loopDetector.minChars - 1))

  assert.deepEqual(
    { state: result.state, isLoop: result.isLoop, step: result.step, effective: result.effective },
    { state: 'WarmingUp', isLoop: false, step: loopDetector.minChars - 1, effective: undefined },
  )
})

test('LOOP_003_single_character_repetition_is_loop', () => {
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'x'.repeat(loopDetector.minChars))

  assert.equal(result.state, 'Loop')
  assert.equal(result.isLoop, true)
  assert.equal(result.step, loopDetector.minChars)
  assert.ok(result.effective !== undefined)
  assert.ok(result.effective < loopDetector.loopThreshold, `n_eff=${result.effective}`)
  // A pure single character should land near 1.0 (hashing cannot invent diversity).
  assert.ok(result.effective < 1.5, `n_eff=${result.effective}`)
})

test('LOOP_003_diverse_alphabet_is_normal', () => {
  // 26 letters cycled — far above the single-digit threshold once the window fills.
  const alphabet = 'abcdefghijklmnopqrstuvwxyz'
  const body = alphabet.repeat(Math.ceil(loopDetector.minChars / alphabet.length)).slice(0, loopDetector.minChars)
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, body)

  assert.equal(result.isLoop, false)
  assert.equal(result.state, 'Normal')
  assert.ok(result.effective >= loopDetector.loopThreshold, `n_eff=${result.effective}`)
})

test('LOOP_005_streaming_character_by_character_matches_batch_push', () => {
  const text = 'deadbeef'.repeat(40).slice(0, loopDetector.minChars)
  const batch = loopDetector.create()
  const stream = loopDetector.create()

  const batchResult = loopDetector.pushText(batch, text)

  let streamResult
  for (const character of text) {
    streamResult = loopDetector.pushCharacter(stream, character)
  }

  assert.equal(streamResult.step, batchResult.step)
  assert.equal(streamResult.isLoop, batchResult.isLoop)
  assert.ok(Math.abs(streamResult.effective - batchResult.effective) < 1e-9)
})

test('LOOP_005_whitespace_and_punctuation_count_as_characters', () => {
  // Pure spaces fill the warm-up and still loop: LOOP-004 forbids ignoring them.
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, ' \n\t,'.repeat(loopDetector.minChars).slice(0, loopDetector.minChars))

  assert.equal(result.step, loopDetector.minChars)
  assert.equal(result.isLoop, true)
})

test('LOOP_009_text_delta_decodes_fail_closed', () => {
  assert.equal(loopEventCodec.isLoopTextDelta({ type: 'session.status' }), false)
  assert.equal(loopEventCodec.tryDecodeTextDelta({ type: 'session.status' }), undefined)

  const ok = loopEventCodec.tryDecodeTextDelta({
    type: 'message.part.delta',
    properties: {
      sessionID: 'ses_loop',
      messageID: 'msg_a',
      partID: 'prt_1',
      field: 'text',
      delta: 'aaaa',
    },
  })

  assert.deepEqual(ok, {
    sessionId: 'ses_loop',
    messageId: 'msg_a',
    partId: 'prt_1',
    field: 'text',
    delta: 'aaaa',
  })

  // Reasoning field is ignored so a thinking loop does not kill formal text.
  assert.equal(
    loopEventCodec.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        field: 'reasoning',
        delta: 'zzzz',
      },
    }),
    undefined,
  )

  // Missing session id is refuse, not invent.
  assert.equal(
    loopEventCodec.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: { delta: 'x', field: 'text' },
    }),
    undefined,
  )
})
