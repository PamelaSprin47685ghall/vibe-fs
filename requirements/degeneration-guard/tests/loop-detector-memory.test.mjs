// requirements/degeneration-guard/tests/loop-detector-memory.test.mjs
//
// LOOP-005: the detector is O(HASH_BUCKETS · K) memory — fixed hash buckets,
// no structure that grows with the stream. LOOP-003: judgement is a single
// N_eff threshold crossing with no latch (no continuous-hit requirement, no
// hysteresis). LOOP-005 lifecycle: a fresh detector per attempt means two
// detectors never share state.

import assert from 'node:assert/strict'
import test from 'node:test'

import { loopDetector } from '../../verification-system/tests/support/domain.mjs'

const { LoopEffectiveThreshold } = await import('../../../dist/Execution/Session/LoopDetector.js')

const diverse = () => {
  const alphabet = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_{}();,.='
  let body = ''
  for (let i = 0; i < 2000; i += 1) {
    const x = Math.sin(i * 12.9898) * 43758.5453
    body += alphabet[Math.floor((x - Math.floor(x)) * alphabet.length)]
  }
  return body
}

test('LOOP_005_detector_memory_is_bounded_by_fixed_buckets_not_stream_length', () => {
  const detector = loopDetector.create()
  const sizes = () => ({
    buckets: detector.Value.length,
    kernels: detector.Cross.length,
    totals: detector.Total.length,
    lastStep: detector.LastStep.length,
    prefix: detector.Prefix.length,
  })

  const before = sizes()
  assert.deepEqual(before, { buckets: 4096, kernels: 3, totals: 3, lastStep: 4096, prefix: 4 })

  loopDetector.pushText(detector, 'x'.repeat(200000))
  const afterSmall = sizes()
  assert.deepEqual(afterSmall, before, '10^5 characters must not grow any detector structure')

  loopDetector.pushText(detector, 'x'.repeat(200000))
  assert.deepEqual(sizes(), before, 'stream length must never appear in the memory footprint')
  assert.ok(detector.Step > 0, 'the step counter advances while memory stays fixed')
})

test('LOOP_003_threshold_crossing_is_a_single_event_with_no_latch', () => {
  // Degenerate stream crosses N_eff <= threshold → LOOP.
  const detector = loopDetector.create()
  const degenerate = loopDetector.pushText(detector, 'x'.repeat(4000))
  assert.equal(degenerate.isLoop, true)
  assert.ok(degenerate.effective <= LoopEffectiveThreshold)

  // No hysteresis: the same detector, fed diverse text afterwards, returns to
  // Normal. The detector judges the current stream, not a latched verdict.
  const recovered = loopDetector.pushText(detector, diverse())
  assert.equal(recovered.isLoop, false)
  assert.equal(recovered.state, 'Normal')
  assert.ok(recovered.effective > LoopEffectiveThreshold)
})

test('LOOP_003_judgement_does_not_require_consecutive_hits', () => {
  // Single crossing trips the detector on the very push that crosses the
  // threshold; there is no "N consecutive hits" window to wait through.
  const detector = loopDetector.create()
  const crossed = loopDetector.pushText(detector, 'ab'.repeat(3000))
  assert.equal(crossed.isLoop, true)
})

test('LOOP_005_two_detectors_are_independent_attempts', () => {
  const a = loopDetector.create()
  const b = loopDetector.create()

  loopDetector.pushText(a, 'x'.repeat(4000))
  assert.equal(loopDetector.evaluate(a).isLoop, true)
  // The second attempt's detector never saw attempt A's stream: still innocent.
  assert.equal(loopDetector.evaluate(b).isLoop, false)
  assert.equal(loopDetector.evaluate(b).state, 'Normal')
})
