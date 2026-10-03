import assert from 'node:assert/strict'
import test from 'node:test'
import { encode } from 'gpt-tokenizer/encoding/o200k_base'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, chaoticText, createSensor, rawDelta } from './support/stream.mjs'

test('WHAT[degeneration-guard-001] a finite repeated token stream crosses the lower envelope', () => {
  assert.equal(encode(' retry').length, 1)
  const result = detector.pushText(detector.create(), ' retry'.repeat(1000))
  assert.equal(result.state, 'TooRepetitive')
  assert.equal(result.isAnomalous, true)
  assert.ok(result.weightedDistinctTokens < detector.minimumWeightedDistinctCount)
})

test('WHAT[degeneration-guard-001] this ordinary code sample remains normal', () => {
  const body = `export async function processOrder(orderId, repository, paymentGateway) {
    const order = await repository.findById(orderId);
    if (!order) throw new Error("Order not found");
    const authorization = await paymentGateway.authorize({ amount: order.totalAmount, currency: order.currency });
    if (!authorization.approved) return { success: false, reason: authorization.declineReason };
    return { success: true, order: await repository.finalizeOrder(orderId, authorization.transactionId) };
  }`
  const result = detector.pushText(detector.create(), body)
  assert.equal(result.isAnomalous, false)
  assert.equal(result.state, 'Normal')
})

test('WHAT[degeneration-guard-001] a finite diverse stream produces TooRandom and its own local continuation', async () => {
  const text = chaoticText()
  assert.equal(detector.pushText(detector.create(), text).state, 'TooRandom')
  const continuations = []
  const handle = createSensor({ owned: ['session'], abort: () => {}, continue: (...args) => continuations.push(args) })
  sensor.observe(handle, rawDelta('session', 'thinking', text, 'run'))
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRandom' })
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(continuations, [['session', 'TooRandom']])
})
