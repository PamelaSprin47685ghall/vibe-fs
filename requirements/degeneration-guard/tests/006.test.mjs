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

test('WHAT[degeneration-guard-006] LOOP_005_two_detectors_are_independent_attempts', () => {
  const a = loopDetector.create()
  const b = loopDetector.create()

  loopDetector.pushText(a, ' retry'.repeat(1000))
  assert.equal(loopDetector.evaluate(a).state, 'TooRepetitive')
  assert.equal(loopDetector.evaluate(b).isAnomalous, false)
  assert.equal(loopDetector.evaluate(b).state, 'Normal')
  assert.equal(loopDetector.evaluate(b).step, 0)
})
}

{
const { default: assert } = await import("node:assert/strict");
const { readFileSync } = await import("node:fs");
const { dirname, join } = await import("node:path");
const { default: test } = await import("node:test");
const { fileURLToPath } = await import("node:url");
const { decode, encode, vocabularySize } = await import("gpt-tokenizer/encoding/o200k_base");
const loopDetector = await import("../../../dist/Execution/Session/LoopDetectorSurface.js");
const loopSensor = await import("../../../dist/OpenCode/Host/LoopSensorSurface.js");
const providerLanguage = await import("../../../dist/Participant/Provider/LanguageSurface.js");

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })
const chaoticText = () => {
  const pieces = []

  for (let token = 0; token < vocabularySize && pieces.length < 512; token += 1) {
    let piece
    try {
      piece = decode([token])
    } catch {
      continue
    }

    if (!/^ [A-Za-z]{4,}$/.test(piece)) continue
    const roundTrip = encode(piece)
    if (roundTrip.length === 1 && roundTrip[0] === token) pieces.push(piece)
  }

  assert.equal(pieces.length, 512, 'fixture needs hundreds of stable, distinct single tokens')
  const text = pieces.join('')
  assert.ok(new Set(encode(text).slice(0, 300)).size > 250, 'fixture prefix must remain highly diverse')
  return text
}
const rawDelta = (session, field, text, messageId = 'msg_a') => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: messageId,
    partID: 'prt_1',
    field,
    delta: text,
  },
})
const rawDeltaWithoutMessage = (session, field, text) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    partID: 'prt_1',
    field,
    delta: text,
  },
})

test('WHAT[degeneration-guard-006] LOOP_006_attempt_reset_preserves_armed_cause_until_reconcile', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_idle'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', repetitiveText(), 'msg_idle_1'))
  await wait()
  loopSensor.resetDetector(sensor, 'ses_idle')
  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', repetitiveText(), 'msg_idle_1'))
  await wait()

  assert.deepEqual(aborts, ['ses_idle'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_idle', 'msg_idle_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
})
test('WHAT[degeneration-guard-006] LOOP_015_drop_session_cleans_active_tasks_and_detectors', async () => {
  const sensor = createSensor({
    owned: ['ses_drop'],
    abort: () => ({ ok: true }),
    continue: () => ({ ok: true }),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_drop', repetitiveText(), 'msg_drop_1'))
  await wait()
  assert.notEqual(loopSensor.activeTask(sensor, 'ses_drop', 'msg_drop_1'), null)
  loopSensor.dropSession(sensor, 'ses_drop')

  // Consuming after drop returns External and owns nothing.
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_drop', 'msg_drop_1'), { cause: 'External' })
  assert.equal(loopSensor.activeTask(sensor, 'ses_drop', 'msg_drop_1'), null)
})
}
