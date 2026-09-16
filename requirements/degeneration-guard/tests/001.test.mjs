import assert from 'node:assert/strict'
import test from 'node:test'
import { decode, encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
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

test('WHAT[DG-001] LOOP_003_high_side_is_too_random_and_owns_its_continuation', async () => {
  const text = chaoticText()
  const evaluation = loopDetector.pushText(loopDetector.create(), text)
  assert.equal(evaluation.state, 'TooRandom', `weightedDistinct=${evaluation.weightedDistinctTokens}`)

  const continuations = []
  const sensor = createSensor({
    owned: ['ses_high'],
    abort: () => {},
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, rawDelta('ses_high', 'thinking', text, 'msg_high_1'))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_high', 'msg_high_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRandom',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_high', 'TooRandom']])
})
