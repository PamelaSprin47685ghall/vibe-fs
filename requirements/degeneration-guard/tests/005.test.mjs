import test from 'node:test'

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { encode } = await import("gpt-tokenizer/encoding/o200k_base");
const loopDetector = await import("../../../dist/Execution/Session/LoopDetectorSurface.js");

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

test('WHAT[degeneration-guard-005] LOOP_005_detector_memory_is_bounded_by_tokenizer_vocabulary_not_stream_length', () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { execFileSync } = await import("node:child_process");
const { mkdtempSync, rmSync, unlinkSync, writeFileSync } = await import("node:fs");
const { tmpdir } = await import("node:os");
const { default: path } = await import("node:path");
const { default: test } = await import("node:test");
const { Worker } = await import("node:worker_threads");
const { encode } = await import("gpt-tokenizer/encoding/o200k_base");
const loopDetector = await import("../../../dist/Execution/Session/LoopDetectorSurface.js");
const { deriveLoopDetectorEnvelope, encodeParallel, envelopeBounds, loadLoopDetectorRepositoryCorpusV1 } = await import("../../../scripts/lib/derive-loop-detector-envelope.mjs");
const { loopDetectorRepositoryInputFiles } = await import("../../../scripts/lib/loop-detector-repository-corpus.mjs");

const close = (actual, expected, tolerance = 1e-9) =>
  assert.ok(Math.abs(actual - expected) <= tolerance, `${actual} != ${expected}`)
const lowerQuantileProbability = 0.025
const upperQuantileProbability = 1.0
const centralProbability = upperQuantileProbability - lowerQuantileProbability
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

test('WHAT[degeneration-guard-005] LOOP_005_empty_push_is_noop', () => {
  const detector = loopDetector.create()
  const before = loopDetector.evaluate(detector)
  const after = loopDetector.pushText(detector, '')
  assert.deepEqual(after, before)
})
}
