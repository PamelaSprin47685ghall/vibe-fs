// tests-mjs/Domain/loop-detector.test.mjs — LOOP-003/004/005/011 pure detector.
//
// Final design: sliding 4-grams, slow exp mixture, normal-code prior (N_eff=64),
// LOOP when HHI ≥ 0.03. Layer 1 only.

import assert from 'node:assert/strict'
import test from 'node:test'
import { loopDetector, loopEventCodec } from '../domain.mjs'

test('LOOP_004_constants_match_the_clause', () => {
  assert.equal(loopDetector.ngramSize, 4)
  assert.equal(loopDetector.hashBuckets, 4096)
  assert.equal(loopDetector.k, 3)
  assert.equal(loopDetector.normalEffectiveCount, 64)
  assert.ok(Math.abs(loopDetector.normalHhi - 1 / 64) < 1e-12)
  assert.equal(loopDetector.loopHhi, 0.03)
  assert.ok(Math.abs(loopDetector.loopEffectiveThreshold - 1 / 0.03) < 1e-9)
})

test('LOOP_003_fresh_detector_is_innocent_normal_code_prior', () => {
  const detector = loopDetector.create()
  const result = loopDetector.evaluate(detector)

  assert.equal(result.state, 'Normal')
  assert.equal(result.isLoop, false)
  assert.equal(result.step, 0)
  assert.ok(Math.abs(result.effective - 64) < 1e-6, `n_eff=${result.effective}`)
  assert.ok(Math.abs(result.hhi - 1 / 64) < 1e-9, `hhi=${result.hhi}`)
})

test('LOOP_003_fewer_than_four_characters_keeps_prior', () => {
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'abc')

  assert.equal(result.state, 'Normal')
  assert.equal(result.isLoop, false)
  assert.equal(result.step, 0)
  assert.ok(Math.abs(result.effective - 64) < 1e-6)
})

test('LOOP_003_single_character_long_run_is_loop', () => {
  // A pure single-character stream collapses 4-grams to one bucket → HHI→1.
  // Prior dilutes slowly; need enough grams to overcome N_eff=64 seed.
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'x'.repeat(4000))

  assert.equal(result.isLoop, true, `n_eff=${result.effective} hhi=${result.hhi}`)
  assert.equal(result.state, 'Loop')
  assert.ok(result.hhi >= loopDetector.loopHhi)
  assert.ok(result.effective <= loopDetector.loopEffectiveThreshold)
})

test('LOOP_003_diverse_alphabet_stays_normal', () => {
  // Cycle a large alphabet so 4-grams stay diverse under the slow kernel.
  const alphabet = 'abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  const body = alphabet.repeat(80)
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, body)

  assert.equal(result.isLoop, false, `n_eff=${result.effective} hhi=${result.hhi}`)
  assert.equal(result.state, 'Normal')
  assert.ok(result.hhi < loopDetector.loopHhi)
  assert.ok(result.effective > loopDetector.loopEffectiveThreshold)
})

test('LOOP_005_streaming_matches_batch_push', () => {
  const text = 'deadbeef'.repeat(200)
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
  assert.ok(Math.abs(streamResult.hhi - batchResult.hhi) < 1e-12)
})

test('LOOP_005_whitespace_and_punctuation_count_as_characters', () => {
  const detector = loopDetector.create()
  // Pure whitespace collapses diversity just like a single character.
  const result = loopDetector.pushText(detector, ' \n\t,'.repeat(1000))

  assert.ok(result.step > 0)
  assert.equal(result.isLoop, true, `n_eff=${result.effective}`)
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

  // LOOP-002 / LOOP-007: any non-text field is fail-closed at the codec.
  for (const field of ['model_thought', 'thinking', 'tool', 'reasoning_content']) {
    assert.equal(
      loopEventCodec.tryDecodeTextDelta({
        type: 'message.part.delta',
        properties: {
          sessionID: 'ses_loop',
          messageID: 'msg_a',
          partID: 'prt_1',
          field,
          delta: 'zzzz',
        },
      }),
      undefined,
      `field=${field} must not decode`,
    )
  }

  assert.equal(
    loopEventCodec.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: { delta: 'x', field: 'text' },
    }),
    undefined,
  )
})
