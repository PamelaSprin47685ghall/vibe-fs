import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import { decode, encode, vocabularySize } from 'gpt-tokenizer/encoding/o200k_base'
import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import * as providerLanguage from '../../../dist/Participant/Provider/LanguageSurface.js'

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

test('WHAT[degeneration-guard-008] LOOP_001_armed_anomaly_is_process_local', async () => {
  const first = createSensor({ owned: ['ses_a'], abort: () => {}, continue: () => {} })
  const second = createSensor({ owned: ['ses_a'], abort: () => {}, continue: () => {} })

  loopSensor.observe(first, loopSensor.textDelta('ses_a', repetitiveText(), 'msg_a'))
  await wait()

  assert.deepEqual(loopSensor.consumeAbortCause(first, 'ses_a', 'msg_a'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  assert.deepEqual(loopSensor.consumeAbortCause(second, 'ses_a', 'msg_a'), { cause: 'External' })
})

test('WHAT[degeneration-guard-008] LOOP_013_wrong_run_never_consumes_or_clears_newer_anomaly', async () => {
  const aborts = []
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_scoped'],
    abort: (session) => aborts.push(session),
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, rawDelta('ses_scoped', 'text', repetitiveText(), 'msg_run_1'))
  await wait()
  assert.deepEqual(aborts, ['ses_scoped'])

  // A late/wrong run must not consume the armed anomaly and must not observe the owned task.
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_scoped', 'msg_old_run'), { cause: 'External' })
  assert.equal(loopSensor.activeTask(sensor, 'ses_scoped', 'msg_old_run'), null)

  // The armed run still owns its interrupt task and its one-shot continuation.
  assert.notEqual(loopSensor.activeTask(sensor, 'ses_scoped', 'msg_run_1'), null)
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_scoped', 'msg_run_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_scoped', 'TooRepetitive']])

  // Duplicate abort on the exact run is External: cause and continuation are one-shot.
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_scoped', 'msg_run_1'), { cause: 'External' })
  await wait()
  assert.deepEqual(continuations, [['ses_scoped', 'TooRepetitive']])
})

test('WHAT[degeneration-guard-008] LOOP_016_delta_without_message_id_is_observed_only', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_norun'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_norun', repetitiveText(), undefined))
  loopSensor.observe(sensor, rawDeltaWithoutMessage('ses_norun', 'text', repetitiveText()))
  loopSensor.observe(sensor, loopSensor.textDelta('ses_norun', repetitiveText(), null))
  await wait()

  assert.deepEqual(aborts, [], 'a delta without a run never interrupts')
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_norun', 'msg_never_observed'), {
    cause: 'External',
  })
  assert.equal(loopSensor.activeTask(sensor, 'ses_norun', 'msg_never_observed'), null)
})
