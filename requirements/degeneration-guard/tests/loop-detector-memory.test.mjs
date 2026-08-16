import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'

import { loopDetector } from '../../verification-system/tests/support/domain.mjs'

const trackedTokenCount = (detector) => Array.from(detector.LastSeenTokenStep).length

const diverse = () =>
  Array.from(
    { length: 800 },
    (_, index) =>
      `let value_${index} = repository_${index % 31}.load("entity-${index}", ${index * 7919}); // owner-${index % 47}`,
  ).join('\n')

test('WHAT[DG-005] LOOP_005_detector_memory_is_bounded_by_tokenizer_vocabulary_not_stream_length', () => {
  const detector = loopDetector.create()
  const text = diverse()
  const distinct = new Set(encode(text)).size

  loopDetector.pushText(detector, text)
  assert.equal(trackedTokenCount(detector), distinct)
  assert.ok(trackedTokenCount(detector) <= loopDetector.tokenizerVocabularySize)

  loopDetector.pushText(detector, text)
  assert.equal(
    trackedTokenCount(detector),
    distinct,
    'repeating an arbitrarily longer stream cannot grow state past observed token ids',
  )
})

test('WHAT[DG-003] LOOP_003_threshold_crossing_is_a_single_event_with_no_latch', () => {
  const detector = loopDetector.create()
  const degenerate = loopDetector.pushText(detector, ' retry'.repeat(1000))
  assert.equal(degenerate.isLoop, true)
  assert.ok(degenerate.weightedDistinctTokens <= loopDetector.loopWeightedDistinctThreshold)

  const recovered = loopDetector.pushText(detector, diverse())
  assert.equal(recovered.isLoop, false)
  assert.equal(recovered.state, 'Normal')
  assert.ok(recovered.weightedDistinctTokens > loopDetector.loopWeightedDistinctThreshold)
})

test('WHAT[DG-003] LOOP_003_judgement_does_not_require_consecutive_hits', () => {
  const detector = loopDetector.create()
  let result = loopDetector.evaluate(detector)

  for (let index = 0; index < 200 && !result.isLoop; index += 1) {
    result = loopDetector.pushText(detector, ' retry')
  }

  assert.equal(result.isLoop, true)
  assert.ok(result.step < 100, `single-token repetition crossed at step ${result.step}`)
})

test('WHAT[DG-006] LOOP_005_two_detectors_are_independent_attempts', () => {
  const a = loopDetector.create()
  const b = loopDetector.create()

  loopDetector.pushText(a, ' retry'.repeat(1000))
  assert.equal(loopDetector.evaluate(a).isLoop, true)
  assert.equal(loopDetector.evaluate(b).isLoop, false)
  assert.equal(loopDetector.evaluate(b).state, 'Normal')
  assert.equal(loopDetector.evaluate(b).step, 0)
})
