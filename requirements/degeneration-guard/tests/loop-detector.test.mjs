import assert from 'node:assert/strict'
import test from 'node:test'
import { encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'

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
  assert.equal(loopDetector.vocabularySize, vocabularySize)
  assert.equal(loopDetector.halfLife, 64)
  close(loopDetector.lambda, 2 ** (-1 / 64))
  assert.equal(loopDetector.theoreticalLoopWeightedDistinctCount, 1)
  assert.equal(loopDetector.confidenceLevel, 0.95)
  assert.equal(loopDetector.confidenceQuantile, 0.05)
  assert.ok(loopDetector.normalWeightedDistinctCount > loopDetector.loopWeightedDistinctThreshold)
  assert.ok(
    loopDetector.loopWeightedDistinctThreshold >
      loopDetector.theoreticalLoopWeightedDistinctCount,
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
  close(result.weightedDistinctTokens, 1, 1e-2)
})

test('WHAT[DG-001] LOOP_003_diverse_programmatic_text_stays_normal', () => {
  const body = `
export class OrderProcessor {
  constructor(private readonly repository: OrderRepository, private readonly paymentGateway: PaymentGateway) {}

  async processOrder(orderId: string, user: UserContext): Promise<OrderResult> {
    const order = await this.repository.findById(orderId);
    if (!order) {
      throw new EntityNotFoundError("Order", orderId);
    }
    if (order.status !== OrderStatus.Pending) {
      return { success: false, reason: "Order is not in pending state", currentStatus: order.status };
    }
    const authorization = await this.paymentGateway.authorize({
      amount: order.totalAmount,
      currency: order.currency,
      customerId: user.paymentCustomerId,
    });
    if (!authorization.approved) {
      await this.repository.updateStatus(orderId, OrderStatus.PaymentFailed);
      return { success: false, reason: authorization.declineReason };
    }
    const updated = await this.repository.finalizeOrder(orderId, {
      transactionId: authorization.transactionId,
      processedAt: new Date(),
    });
    await this.notifyCustomer(user.email, updated);
    return { success: true, order: updated };
  }
}
`

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.ok(result.weightedDistinctTokens > loopDetector.loopWeightedDistinctThreshold)
})

test('WHAT[DG-001] LOOP_003_markdown_table_repeated_structure_with_varied_tokens_is_normal', () => {
  const body = `
# Deployment Matrix and Service Configuration

| Service Name | Cluster | Zone | Min Instances | Max Instances | CPU Target | Memory (GB) | Health Check URL | Owner Team |
| :--- | :--- | :--- | ---: | ---: | ---: | ---: | :--- | :--- |
| ingress-gateway | prod-us-east-1 | us-east-1a | 4 | 32 | 70% | 8 | /healthz/ready | networking |
| auth-session-broker | prod-us-east-1 | us-east-1b | 2 | 16 | 60% | 16 | /v1/system/status | identity-core |
| catalog-search-indexer | prod-eu-west-1 | eu-west-1a | 3 | 24 | 75% | 32 | /internal/probes/liveness | search-discovery |
| billing-invoice-worker | prod-ap-southeast-1 | ap-southeast-1c | 1 | 8 | 50% | 4 | /metrics/health | payments-ledger |
`

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.ok(result.weightedDistinctTokens > loopDetector.loopWeightedDistinctThreshold)
})

test('WHAT[DG-001] LOOP_003_ascii_graph_repeated_connectors_with_varied_tokens_is_normal', () => {
  const body = `
# System Architecture Diagram

\`\`\`
       +-----------------------+
       |   Cloudflare CDN      |
       +-----------+-----------+
                   | (HTTPS / TLS 1.3)
                   v
       +-----------------------+
       |   Kong API Gateway    | <--- JWT Validation / Rate Limiting
       +-----------+-----------+
                   |
         +---------+---------+
         |                   |
         v                   v
+-----------------+ +-----------------+
| Checkout Worker | | Inventory Micro |
+--------+--------+ +--------+--------+
         |                   |
         +---------+---------+
                   | (gRPC Protocol)
                   v
       +-----------------------+
       | PostgreSQL Primary DB |
       +-----------------------+
\`\`\`
`

  const result = loopDetector.pushText(loopDetector.create(), body)
  assert.equal(result.isLoop, false, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.ok(result.weightedDistinctTokens > loopDetector.loopWeightedDistinctThreshold)
})

test('WHAT[DG-005] LOOP_005_empty_push_is_noop', () => {
  const detector = loopDetector.create()
  const before = loopDetector.evaluate(detector)
  const after = loopDetector.pushText(detector, '')
  assert.deepEqual(after, before)
})

test('WHAT[DG-002] LOOP_009_text_delta_decodes_fail_closed', () => {
  assert.equal(loopDetector.isLoopTextDelta({ type: 'session.status' }), false)
  assert.equal(loopDetector.tryDecodeTextDelta({ type: 'session.status' }), null)

  const ok = loopDetector.tryDecodeTextDelta({
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
    loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: {
        sessionID: 'ses_loop',
        field: 'reasoning',
        delta: 'zzzz',
      },
    }),
    null,
  )

  for (const field of ['model_thought', 'thinking', 'tool', 'reasoning_content']) {
    assert.equal(
      loopDetector.tryDecodeTextDelta({
        type: 'message.part.delta',
        properties: {
          sessionID: 'ses_loop',
          messageID: 'msg_a',
          partID: 'prt_1',
          field,
          delta: 'zzzz',
        },
      }),
      null,
      `field=${field} must not decode`,
    )
  }

  assert.equal(
    loopDetector.tryDecodeTextDelta({
      type: 'message.part.delta',
      properties: { delta: 'x', field: 'text' },
    }),
    null,
  )
})
