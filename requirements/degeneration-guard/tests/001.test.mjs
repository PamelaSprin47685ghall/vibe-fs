import test from 'node:test'

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

test('WHAT[degeneration-guard-001] LOOP_003_single_token_repetition_becomes_too_repetitive', () => {
  const unit = ' retry'
  assert.equal(encode(unit).length, 1, 'fixture must be one o200k token')

  const result = loopDetector.pushText(loopDetector.create(), unit.repeat(1000))
  assert.equal(result.isAnomalous, true, `weightedDistinct=${result.weightedDistinctTokens}`)
  assert.equal(result.state, 'TooRepetitive')
  assert.ok(result.weightedDistinctTokens < loopDetector.minimumWeightedDistinctCount)
})
test('WHAT[degeneration-guard-001] LOOP_003_repository_like_programmatic_text_stays_normal', () => {
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

test('WHAT[degeneration-guard-001] LOOP_003_high_side_is_too_random_and_owns_its_continuation', async () => {
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
}
