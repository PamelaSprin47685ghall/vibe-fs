import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'

const trackedTokenCount = (detector) => loopDetector.trackedTokenCount(detector)

const diverse = () => `
export class OrderProcessor {
  constructor(private readonly repository: OrderRepository, private readonly paymentGateway: PaymentGateway) {}
  async processOrder(orderId: string, user: UserContext): Promise<OrderResult> {
    const order = await this.repository.findById(orderId);
    if (!order) throw new EntityNotFoundError("Order", orderId);
    if (order.status !== OrderStatus.Pending) return { success: false, reason: "Order is not in pending state", currentStatus: order.status };
    const authorization = await this.paymentGateway.authorize({ amount: order.totalAmount, currency: order.currency, customerId: user.paymentCustomerId });
    if (!authorization.approved) {
      await this.repository.updateStatus(orderId, OrderStatus.PaymentFailed);
      return { success: false, reason: authorization.declineReason };
    }
    const updated = await this.repository.finalizeOrder(orderId, { transactionId: authorization.transactionId, processedAt: new Date() });
    await this.notifyCustomer(user.email, updated);
    return { success: true, order: updated };
  }
}
function calculateTax(income, filingStatus) {
  const brackets = getTaxBrackets(filingStatus);
  let tax = 0;
  for (const bracket of brackets) {
    if (income > bracket.min) {
      const taxable = Math.min(income - bracket.min, bracket.max - bracket.min);
      tax += taxable * bracket.rate;
    }
  }
  return { income, tax, effectiveRate: tax / income };
}
async function checkInventoryAvailability(warehouseId, skuList) {
  const inventory = await db.warehouseInventory.query({ warehouseId, skus: skuList });
  const missing = [];
  for (const item of skuList) {
    const stock = inventory.find(i => i.sku === item.sku);
    if (!stock || stock.quantity < item.required) missing.push(item.sku);
  }
  return { available: missing.length === 0, missingItems: missing };
}
`

test('WHAT[DG-005] LOOP_005_detector_memory_is_bounded_by_tokenizer_vocabulary_not_stream_length', () => {
  const detector = loopDetector.create()
  const text = diverse()
  const distinct = new Set(encode(text)).size

  loopDetector.pushText(detector, text)
  assert.equal(trackedTokenCount(detector), distinct)
  assert.ok(trackedTokenCount(detector) <= loopDetector.vocabularySize)

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
