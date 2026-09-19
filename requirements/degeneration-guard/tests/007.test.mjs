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

test('WHAT[degeneration-guard-007] LOOP_006_low_side_interrupts_once_but_does_not_continue_before_reconcile', async () => {
  const aborts = []
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_low'],
    abort: (session) => aborts.push(session),
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_low', repetitiveText(), 'msg_low_1'))
  await wait()
  loopSensor.observe(sensor, loopSensor.textDelta('ses_low', repetitiveText(), 'msg_low_1'))
  await wait()

  assert.deepEqual(aborts, ['ses_low'])
  assert.deepEqual(continuations, [], 'abort completion is not the continuation fence')

  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_low', 'msg_low_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_low', 'TooRepetitive']])

  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_low', 'msg_low_1'), { cause: 'External' })
  await wait()
  assert.deepEqual(continuations, [['ses_low', 'TooRepetitive']], 'cause and continuation are one-shot')
})

test('WHAT[degeneration-guard-007] LOOP_006_abort_failure_rolls_back_guard_ownership', async () => {
  const aborts = []
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_fail'],
    abort: (session) => {
      aborts.push(session)
      return { ok: false, error: 'host refused interrupt' }
    },
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_fail', repetitiveText(), 'msg_fail_1'))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_fail', 'msg_fail_1'), { cause: 'External' })
  assert.deepEqual(continuations, [])
  assert.equal(loopSensor.activeTask(sensor, 'ses_fail', 'msg_fail_1'), null)

  // Rollback released single-flight: the same run can arm again and is recorded again.
  loopSensor.observe(sensor, loopSensor.textDelta('ses_fail', repetitiveText(), 'msg_fail_1'))
  await wait()
  assert.deepEqual(aborts, ['ses_fail', 'ses_fail'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_fail', 'msg_fail_1'), { cause: 'External' })
  assert.deepEqual(continuations, [])
})

test('WHAT[degeneration-guard-007] LOOP_006_abort_throw_is_observed_without_recovery', async () => {
  const aborts = []
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_throw'],
    abort: (session) => {
      aborts.push(session)
      throw new Error('transport exploded')
    },
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_throw', repetitiveText(), 'msg_throw_1'))
  await wait()
  assert.deepEqual(aborts, ['ses_throw'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_throw', 'msg_throw_1'), { cause: 'External' })
  assert.deepEqual(continuations, [])
  assert.equal(loopSensor.activeTask(sensor, 'ses_throw', 'msg_throw_1'), null)
})
