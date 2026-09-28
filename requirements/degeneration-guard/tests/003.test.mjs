import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import { envelopeBounds } from '../../../scripts/lib/derive-loop-detector-envelope.mjs'

const close = (actual, expected) => assert.ok(Math.abs(actual - expected) <= 1e-9, `${actual} != ${expected}`)

test('WHAT[degeneration-guard-003] fresh detector starts at the repository normal prior', () => {
  const result = detector.evaluate(detector.create())
  assert.equal(result.state, 'Normal')
  assert.equal(result.isAnomalous, false)
  assert.equal(result.step, 0)
  close(result.weightedDistinctTokens, detector.normalWeightedDistinctCount)
})

test('WHAT[degeneration-guard-003] token score equals decayed prior plus the last-occurrence weight of each distinct token', () => {
  for (const text of ['const π = await repository.load("订单-42");\nreturn { ok: true, revision: 17 };', ' retry repeat retry repeated retry']) {
    const tokens = encode(text)
    const last = new Map(tokens.map((token, index) => [token, index + 1]))
    const expected = detector.normalWeightedDistinctCount * detector.lambda ** tokens.length
      + [...last.values()].reduce((sum, position) => sum + detector.lambda ** (tokens.length - position), 0)
    const result = detector.pushText(detector.create(), text)
    assert.equal(result.step, tokens.length)
    close(result.weightedDistinctTokens, expected)
  }
})

test('WHAT[degeneration-guard-003] both exact envelope boundaries are normal and strictly outside values are anomalous', () => {
  const minimum = detector.minimumWeightedDistinctCount
  const maximum = detector.maximumWeightedDistinctCount
  assert.equal(detector.classify(minimum), 'Normal')
  assert.equal(detector.classify(maximum), 'Normal')
  assert.equal(detector.classify(minimum - 0.01), 'TooRepetitive')
  assert.equal(detector.classify(maximum + 0.01), 'TooRandom')
})

test('WHAT[degeneration-guard-003] empirical bounds handle singleton equal values and nearest ranks', () => {
  assert.deepEqual(envelopeBounds([42], 0.5), { minimum: 42, maximum: 42 })
  assert.deepEqual(envelopeBounds([7, 7, 7, 7, 7], 0.5), { minimum: 7, maximum: 7 })
  assert.deepEqual(envelopeBounds([-1e15, 0, 1e15], 0.5), { minimum: 0, maximum: 1e15 })
  const samples = [10, 20, 30, 40, 50, 60, 70, 80, 90, 100]
  assert.deepEqual(envelopeBounds(samples, 0.5), { minimum: 50, maximum: 100 })
  assert.deepEqual(envelopeBounds(samples, 0.9), { minimum: 90, maximum: 100 })
  assert.deepEqual(envelopeBounds(samples, 1), { minimum: 100, maximum: 100 })
  assert.deepEqual(envelopeBounds(Array.from({ length: 40 }, (_, index) => index + 1), 0.025), { minimum: 1, maximum: 40 })
})

test('WHAT[degeneration-guard-003] missing samples and invalid probability cannot define an envelope', () => {
  assert.throws(() => envelopeBounds([], 0.5), /no samples/)
  for (const probability of [0, -0.2, 1.5, NaN]) assert.throws(() => envelopeBounds([1, 2, 3], probability), /invalid probability/)
})
