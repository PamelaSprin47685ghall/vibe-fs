// tests/unit/Domain/loop-detector.test.mjs — LOOP-003/004/005/011 pure detector.
//
// Final design: ignore whitespace + '-', sliding 4-grams, slow exp mixture,
// normal-code prior (N_eff=256), LOOP when N_eff ≤ 140. Layer 1 only.

import assert from 'node:assert/strict'
import test from 'node:test'
import { loopDetector, loopEventCodec } from '../support/domain.mjs'

test('LOOP_004_constants_match_the_clause', () => {
  assert.equal(loopDetector.ngramSize, 4)
  assert.equal(loopDetector.hashBuckets, 4096)
  assert.equal(loopDetector.k, 3)
  assert.equal(loopDetector.normalEffectiveCount, 256)
  assert.ok(Math.abs(loopDetector.normalHhi - 1 / 256) < 1e-12)
  assert.equal(loopDetector.garbageEffectiveCount, 24)
  assert.equal(loopDetector.loopEffectiveThreshold, 140)
  assert.ok(Math.abs(loopDetector.loopHhi - 1 / 140) < 1e-12)
  assert.ok(
    Math.abs(
      loopDetector.loopEffectiveThreshold -
        (loopDetector.normalEffectiveCount + loopDetector.garbageEffectiveCount) / 2,
    ) < 1e-12,
  )
})

test('LOOP_003_fresh_detector_is_innocent_normal_code_prior', () => {
  const detector = loopDetector.create()
  const result = loopDetector.evaluate(detector)

  assert.equal(result.state, 'Normal')
  assert.equal(result.isLoop, false)
  assert.equal(result.step, 0)
  assert.ok(Math.abs(result.effective - 256) < 1e-6, `n_eff=${result.effective}`)
  assert.ok(Math.abs(result.hhi - 1 / 256) < 1e-9, `hhi=${result.hhi}`)
})

test('LOOP_003_fewer_than_four_characters_keeps_prior', () => {
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'abc')

  assert.equal(result.state, 'Normal')
  assert.equal(result.isLoop, false)
  assert.equal(result.step, 0)
  assert.ok(Math.abs(result.effective - 256) < 1e-6)
})

test('LOOP_003_whitespace_and_minus_are_ignored_and_do_not_advance', () => {
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, ' \n\t\r-'.repeat(500))

  assert.equal(result.step, 0)
  assert.equal(result.isLoop, false)
  assert.equal(result.state, 'Normal')
  assert.ok(Math.abs(result.effective - 256) < 1e-6)
})

test('LOOP_003_single_character_long_run_is_loop', () => {
  // Pure single-character stream collapses 4-grams to one bucket → HHI→1.
  // Prior dilutes slowly; need enough grams to overcome N_eff=256 seed.
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, 'x'.repeat(4000))

  assert.equal(result.isLoop, true, `n_eff=${result.effective} hhi=${result.hhi}`)
  assert.equal(result.state, 'Loop')
  assert.ok(result.effective <= loopDetector.loopEffectiveThreshold)
  assert.ok(result.hhi >= loopDetector.loopHhi)
})

test('LOOP_003_diverse_alphabet_stays_normal', () => {
  // Pseudo-random alnum stream keeps N_eff hundreds under the slow kernel.
  // Periodic alphabets (period 62 etc.) correctly trip N_eff≤140 — not diverse.
  const alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_{}();,.='
  let body = ''
  for (let i = 0; i < 4000; i++) {
    const x = Math.sin(i * 12.9898) * 43758.5453
    body += alphabet[Math.floor((x - Math.floor(x)) * alphabet.length)]
  }
  const detector = loopDetector.create()
  const result = loopDetector.pushText(detector, body)

  assert.equal(result.isLoop, false, `n_eff=${result.effective} hhi=${result.hhi}`)
  assert.equal(result.state, 'Normal')
  assert.ok(result.effective > loopDetector.loopEffectiveThreshold)
  assert.ok(result.hhi < loopDetector.loopHhi)
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

test('LOOP_005_ignored_chars_do_not_form_grams_or_dilute_prior', () => {
  const withIgnored = loopDetector.create()
  const withoutIgnored = loopDetector.create()

  const ignored = 'a-b c\td\ne\rf-g h'
  const compact = 'abcdefgh'

  const ignoredResult = loopDetector.pushText(withIgnored, ignored)
  const compactResult = loopDetector.pushText(withoutIgnored, compact)

  assert.equal(ignoredResult.step, compactResult.step)
  assert.ok(Math.abs(ignoredResult.effective - compactResult.effective) < 1e-9)
  assert.ok(Math.abs(ignoredResult.hhi - compactResult.hhi) < 1e-12)
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
