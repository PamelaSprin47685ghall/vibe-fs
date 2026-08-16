import assert from 'node:assert/strict'
import test from 'node:test'
import { encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'

import { loopDetector, loopEventCodec } from '../../verification-system/tests/support/domain.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

const referenceScore = (text) => {
  const lastSeen = new Map()
  let weightedDistinctTokens = loopDetector.normalWeightedDistinctCount
  let step = 0

  for (const token of encode(text)) {
    step += 1
    const previous = lastSeen.get(token)
    weightedDistinctTokens =
      loopDetector.lambda * weightedDistinctTokens +
      1 -
      (previous === undefined ? 0 : loopDetector.lambda ** (step - previous))
    lastSeen.set(token, step)
  }

  return { weightedDistinctTokens, step }
}

test('WHAT[DG-004] LOOP_004_constants_are_token_calibrated', () => {
  assert.equal(loopDetector.tokenizerVocabularySize, vocabularySize)
  assert.equal(loopDetector.halfLife, 64)
  close(loopDetector.lambda, 2 ** (-1 / 64))
  assert.equal(loopDetector.theoreticalLoopWeightedDistinctCount, 1)
  close(
    loopDetector.loopWeightedDistinctThreshold,
    (loopDetector.normalWeightedDistinctCount + loopDetector.theoreticalLoopWeightedDistinctCount) / 2,
  )
})

test('WHAT[DG-003] LOOP_003_fresh_detector_uses_repository_normal_prior', () => {
  const result = loopDetector.evaluate(loopDetector.create())
  assert.equal(result.state, 'Normal')
  assert.equal(result.isLoop, false)
  assert.equal(result.step, 0)
  close(result.weightedDistinctTokens, loopDetector.normalWeightedDistinctCount)
})

test('WHAT[DG-003] LOOP_003_push_text_is_o200k_token_based', () => {
  const text = 'const π = await repository.load("订单-42");\nreturn { ok: true, revision: 17 };'
  const expected = referenceScore(text)
  const result = loopDetector.pushText(loopDetector.create(), text)

  assert.equal(result.step, encode(text).length)
  assert.equal(result.step, expected.step)
  close(result.weightedDistinctTokens, expected.weightedDistinctTokens)
})

test('WHAT[DG-003] LOOP_003_whitespace_and_punctuation_are_tokens_not_character_exceptions', () => {
  const text = ' \n\t\r-'.repeat(200)
  const result = loopDetector.pushText(loopDetector.create(), text)
  assert.equal(result.step, encode(text).length)
  assert.ok(result.step > 0)
})

test('WHAT[DG-001] LOOP_003_single_token_repetition_converges_to_theoretical_loop', () => {
  const unit = ' retry'
  assert.equal(encode(unit).length, 1, 'fixture must be one o200k token')

  const result = loopDetector.pushText(loopDetector.create(), unit.repeat(1000))
  assert.equal(result.isLoop, true, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'Loop')
  assert.ok(result.weightedDistinctTokens <= loopDetector.loopWeightedDistinctThreshold)
  close(result.weightedDistinctTokens, 1, 1e-3)
})

test('WHAT[DG-001] LOOP_003_diverse_programmatic_text_stays_normal', () => {
  const body = Array.from(
    { length: 500 },
    (_, index) =>
      `const result_${index} = await shard_${index % 23}.load("entity-${index}", { revision: ${index * 17 + 3}, owner: "worker-${index % 41}" });`,
  ).join('\n')

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.ok(result.weightedDistinctTokens > loopDetector.loopWeightedDistinctThreshold)
})

test('WHAT[DG-001] LOOP_003_markdown_table_repeated_structure_with_varied_tokens_is_normal', () => {
  const body = [
    '| Component | Owner | Revision | Evidence |',
    '| --- | --- | ---: | --- |',
    ...Array.from(
      { length: 500 },
      (_, index) =>
        `| component-${index} | team-${index % 37} | ${1000 + index} | artifact-${index}-${(index * 7919).toString(16)} |`,
    ),
  ].join('\n')

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
})

test('WHAT[DG-001] LOOP_003_ascii_graph_repeated_connectors_with_varied_tokens_is_normal', () => {
  const body = Array.from(
    { length: 500 },
    (_, index) =>
      `[stage_${index}] ----queue_${index % 29}/${100 + index}----> [stage_${index + 1}] status=${['cold', 'warm', 'ready'][index % 3]} owner=worker_${index % 43}`,
  ).join('\n')

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
})

test('WHAT[DG-005] LOOP_005_empty_push_is_noop', () => {
  const detector = loopDetector.create()
  const before = loopDetector.evaluate(detector)
  const after = loopDetector.pushText(detector, '')
  assert.deepEqual(after, before)
})

test('WHAT[DG-002] LOOP_009_text_delta_decodes_fail_closed', () => {
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
