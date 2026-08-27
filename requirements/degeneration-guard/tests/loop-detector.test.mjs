import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import { deriveLoopDetectorEnvelope } from '../../../scripts/lib/derive-loop-detector-envelope.mjs'
import {
  loopDetectorRepositoryInputFiles,
  loopDetectorRepositoryTexts,
} from '../../../scripts/lib/loop-detector-repository-corpus.mjs'

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)

const centralProbability = 0.95
const lowerQuantileProbability = (1 - centralProbability) / 2
const upperQuantileProbability = 1 - lowerQuantileProbability

const empiricalQuantile = (values, probability) => {
  const rank = Math.ceil(probability * values.length)
  const index = Math.min(values.length - 1, rank - 1)
  return Float64Array.from(values).sort()[index]
}

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

const referenceEnvelope = (tokens, lambda, initialValue) => {
  const lastSeen = new Map()
  let weightedDistinctTokens = initialValue
  let sum = 0
  const trajectory = new Float64Array(tokens.length)

  for (let index = 0; index < tokens.length; index += 1) {
    const step = index + 1
    const token = tokens[index]
    const previous = lastSeen.get(token)
    weightedDistinctTokens =
      lambda * weightedDistinctTokens +
      1 -
      (previous === undefined ? 0 : lambda ** (step - previous))
    lastSeen.set(token, step)
    trajectory[index] = weightedDistinctTokens
    sum += weightedDistinctTokens
  }

  return {
    mean: sum / tokens.length,
    minimum: empiricalQuantile(trajectory, lowerQuantileProbability),
    maximum: empiricalQuantile(trajectory, upperQuantileProbability),
  }
}

test('WHAT[DG-003] LOOP_003_fresh_detector_uses_repository_normal_prior', () => {
  const result = loopDetector.evaluate(loopDetector.create())
  assert.equal(result.state, 'Normal')
  assert.equal(result.isAnomalous, false)
  assert.equal(result.step, 0)
  close(result.weightedDistinctTokens, loopDetector.normalWeightedDistinctCount)
})

test('WHAT[DG-004] LOOP_004_runtime_envelope_is_freshly_derived_from_the_current_repository_without_numeric_snapshots', async () => {
  const derived = await deriveLoopDetectorEnvelope()

  assert.equal(derived.halfLife, 256)
  assert.equal(derived.centralProbability, 0.95)
  close(derived.lowerQuantileProbability, 0.025)
  close(derived.upperQuantileProbability, 0.975)
  close(loopDetector.halfLife, derived.halfLife)
  close(loopDetector.lambda, derived.lambda)
  close(loopDetector.normalWeightedDistinctCount, derived.normalPrior)
  close(loopDetector.centralProbability, derived.centralProbability)
  close(loopDetector.lowerQuantileProbability, derived.lowerQuantileProbability)
  close(loopDetector.upperQuantileProbability, derived.upperQuantileProbability)
  close(loopDetector.minimumWeightedDistinctCount, derived.minimum)
  close(loopDetector.maximumWeightedDistinctCount, derived.maximum)

  const tokens = encode(loopDetectorRepositoryTexts().join('\n'))
  const reference = referenceEnvelope(tokens, derived.lambda, derived.normalPrior)
  close(reference.mean, derived.normalPrior)
  close(reference.minimum, derived.minimum)
  close(reference.maximum, derived.maximum)
})

test('WHAT[DG-004] LOOP_004_repository_corpus_contains_normal_source_documents_only', () => {
  const files = loopDetectorRepositoryInputFiles().map((file) => file.replaceAll('\\', '/'))

  assert.ok(files.some((file) => file.endsWith('/src/Wanxiangshu/Execution/Session/LoopDetector.fs')))
  assert.ok(files.some((file) => file.endsWith('/requirements/degeneration-guard/WHAT.md')))
  assert.ok(!files.some((file) => file.endsWith('/package-lock.json')))
  assert.ok(!files.some((file) => file.endsWith('/scripts/checks/semantic-owners.json')))
  assert.ok(!files.some((file) => file.endsWith('/docs/index.html')))
})

test('WHAT[DG-003] LOOP_003_push_text_is_o200k_token_based', () => {
  const text = 'const π = await repository.load("订单-42");\nreturn { ok: true, revision: 17 };'
  const expected = referenceScore(text)
  const result = loopDetector.pushText(loopDetector.create(), text)

  assert.equal(result.step, encode(text).length)
  assert.equal(result.step, expected.step)
  close(result.weightedDistinctTokens, expected.weightedDistinctTokens)
})

test('WHAT[DG-001] LOOP_003_single_token_repetition_becomes_too_repetitive', () => {
  const unit = ' retry'
  assert.equal(encode(unit).length, 1, 'fixture must be one o200k token')

  const result = loopDetector.pushText(loopDetector.create(), unit.repeat(1000))
  assert.equal(result.isAnomalous, true, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'TooRepetitive')
  assert.ok(result.weightedDistinctTokens < loopDetector.minimumWeightedDistinctCount)
})

test('WHAT[DG-001] LOOP_003_repository_like_programmatic_text_stays_normal', () => {
  const body = `
export class OrderProcessor {
  constructor(private readonly repository: OrderRepository, private readonly paymentGateway: PaymentGateway) {}
  async processOrder(orderId: string, user: UserContext): Promise<OrderResult> {
    const order = await this.repository.findById(orderId);
    if (!order) throw new EntityNotFoundError("Order", orderId);
    const authorization = await this.paymentGateway.authorize({ amount: order.totalAmount, currency: order.currency });
    if (!authorization.approved) return { success: false, reason: authorization.declineReason };
    return { success: true, order: await this.repository.finalizeOrder(orderId, authorization.transactionId) };
  }
}
`

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isAnomalous, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'Normal')
})

test('WHAT[DG-005] LOOP_005_empty_push_is_noop', () => {
  const detector = loopDetector.create()
  const before = loopDetector.evaluate(detector)
  const after = loopDetector.pushText(detector, '')
  assert.deepEqual(after, before)
})

test('WHAT[DG-002] LOOP_009_text_and_reasoning_delta_decode_fail_closed', () => {
  assert.equal(loopDetector.tryDecodeTextDelta({ type: 'session.status' }), null)

  for (const field of ['text', 'reasoning', 'model_thought', 'thinking', 'reasoning_content']) {
    assert.deepEqual(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), {
      sessionId: 'ses_loop',
      messageId: 'msg_a',
      partId: 'prt_1',
      field,
      delta: 'zzzz',
    })
  }

  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    assert.equal(loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        messageID: 'msg_a',
        partID: 'prt_1',
        field,
        delta: 'zzzz',
      },
    }), null)
  }
})
