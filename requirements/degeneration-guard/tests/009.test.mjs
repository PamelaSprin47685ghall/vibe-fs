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

test('WHAT[DG-009] LOOP_017_new_attempt_waits_for_active_continuation_drain', async () => {
  const aborts = []
  const continuations = []
  let releaseContinue = () => {}
  const continueGate = new Promise((resolve) => {
    releaseContinue = resolve
  })
  const sensor = createSensor({
    owned: ['ses_next'],
    abort: (session) => aborts.push(session),
    continue: (session, anomaly) => {
      continuations.push([session, anomaly])
      return continueGate
    },
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_next', repetitiveText(), 'msg_next_1'))
  await wait()
  assert.deepEqual(aborts, ['ses_next'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_next', 'msg_next_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })

  // Same session, new attempt while the owned continuation is still active: no new arm.
  const ownedContinuation = loopSensor.activeTask(sensor, 'ses_next', 'msg_next_1')
  assert.notEqual(ownedContinuation, null)
  assert.equal(loopSensor.activeTask(sensor, 'ses_next', 'msg_next_2'), null)
  loopSensor.observe(sensor, loopSensor.textDelta('ses_next', repetitiveText(), 'msg_next_2'))
  await wait()
  assert.deepEqual(aborts, ['ses_next'], 'active continuation blocks a same-session re-arm')

  // Drain the exact owned task through the production implementation, then retry.
  releaseContinue()
  await ownedContinuation
  await wait()
  assert.equal(loopSensor.activeTask(sensor, 'ses_next', 'msg_next_1'), null)

  loopSensor.observe(sensor, loopSensor.textDelta('ses_next', repetitiveText(), 'msg_next_2'))
  await wait()
  assert.deepEqual(aborts, ['ses_next', 'ses_next'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_next', 'msg_next_2'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [
    ['ses_next', 'TooRepetitive'],
    ['ses_next', 'TooRepetitive'],
  ])
})

test('WHAT[DG-009] LOOP_014_active_interrupt_tracked_in_owned_work_lifecycle', async () => {
  let workExecuted = 0
  const sensor = loopSensor.create({
    owned: ['ses_work'],
    abort: () => {
      workExecuted += 1
      return { ok: true }
    },
    continue: () => {
      workExecuted += 1
      return { ok: true }
    },
    diagnostic: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_work', repetitiveText(), 'msg_work_1'))
  await wait()
  assert.equal(workExecuted, 1, 'abortSession was dispatched within owned-work lifecycle')

  const ownedInterrupt = loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1')
  assert.notEqual(ownedInterrupt, null)
  await ownedInterrupt
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_work', 'msg_work_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })

  const ownedContinue = loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1')
  assert.notEqual(ownedContinue, null)
  await ownedContinue
  await wait()
  assert.equal(workExecuted, 2, 'continueSession was dispatched within owned-work lifecycle')
  assert.equal(loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1'), null)
})

test('WHAT[DG-009] LOOP_018_diagnostics_name_kind_not_side', async () => {
  const diagnostics = []
  const sensor = createSensor({
    owned: ['ses_diag'],
    abort: () => {},
    continue: () => {},
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_diag', repetitiveText(), 'msg_diag_1'))
  await wait()
  loopSensor.consumeAbortCause(sensor, 'ses_diag', 'msg_diag_1')
  await wait()

  assert.ok(diagnostics.length > 0, 'guard reports its effects as diagnostics')
  for (const [, fields] of diagnostics) {
    for (const [name] of fields) {
      assert.notEqual(name, 'side')
    }
  }
  const armed = diagnostics.find(([, fields]) =>
    fields.some(([name, value]) => name === 'result' && value === 'armed'),
  )
  assert.ok(armed, 'interrupt arms visibly')
  assert.ok(
    armed[1].some(([name, value]) => name === 'kind' && value === 'too-repetitive'),
    'armed diagnostic carries the degeneration kind',
  )
})

test('WHAT[DG-009] LOOP_008_guard_has_no_fallback_or_nudge_recovery_path', () => {
  const sensorSource = readFileSync(join(root, 'src/Wanxiangshu/OpenCode/Host/LoopSensor.fs'), 'utf8')
  const ordinarySource = readFileSync(
    join(root, 'src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs'),
    'utf8',
  )
  const fallbackSource = readFileSync(
    join(root, 'src/Wanxiangshu/Participant/Provider/Attempt/Fallback/Workflow.fs'),
    'utf8',
  )

  assert.doesNotMatch(sensorSource, /Fallback|ProviderRetryAttempt|AABB|Nudge/)
  assert.doesNotMatch(sensorSource, /"side"/)
  assert.doesNotMatch(ordinarySource, /continueAfterLoopKill/)
  assert.doesNotMatch(fallbackSource, /continueAfterLoopKill|LoopContinue/)
})
