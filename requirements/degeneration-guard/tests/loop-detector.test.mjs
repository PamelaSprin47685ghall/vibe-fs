import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import { deriveLoopDetectorEnvelope } from '../../../scripts/lib/derive-loop-detector-envelope.mjs'

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

test('WHAT[DG-003] LOOP_003_fresh_detector_uses_repository_normal_prior', () => {
  const result = loopDetector.evaluate(loopDetector.create())
  assert.equal(result.state, 'Normal')
  assert.equal(result.isAnomalous, false)
  assert.equal(result.step, 0)
  close(result.weightedDistinctTokens, loopDetector.normalWeightedDistinctCount)
})

test('WHAT[DG-004] LOOP_004_runtime_envelope_is_freshly_derived_from_the_current_repository_without_numeric_snapshots', () => {
  const derived = deriveLoopDetectorEnvelope()

  close(loopDetector.halfLife, derived.halfLife)
  close(loopDetector.lambda, derived.lambda)
  close(loopDetector.normalWeightedDistinctCount, derived.normalPrior)
  close(loopDetector.minimumWeightedDistinctCount, derived.minimum)
  close(loopDetector.maximumWeightedDistinctCount, derived.maximum)
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
  close(result.weightedDistinctTokens, 1, 1e-2)
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
