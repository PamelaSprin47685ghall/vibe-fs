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

test('WHAT[DG-002] LOOP_002_sensor_observes_text_and_reasoning_only', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_text'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  for (const field of ['tool', 'tool_call', 'custom_metadata']) {
    loopSensor.observe(sensor, rawDelta('ses_text', field, repetitiveText()))
  }
  await wait()
  assert.deepEqual(aborts, [])

  loopSensor.observe(sensor, rawDelta('ses_text', 'reasoning', repetitiveText()))
  await wait()
  assert.deepEqual(aborts, ['ses_text'])
})
}
