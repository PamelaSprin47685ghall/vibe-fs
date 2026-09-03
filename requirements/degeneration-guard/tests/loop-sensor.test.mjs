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

const rawDelta = (session, field, text) => ({
  type: 'message.part.delta',
  properties: {
    sessionID: session,
    messageID: 'msg_a',
    partID: 'prt_1',
    field,
    delta: text,
  },
})

test('WHAT[DG-008] LOOP_001_armed_anomaly_is_process_local', async () => {
  const first = createSensor({ owned: ['ses_a'], abort: () => {}, continue: () => {} })
  const second = createSensor({ owned: ['ses_a'], abort: () => {}, continue: () => {} })

  loopSensor.observe(first, loopSensor.textDelta('ses_a', repetitiveText()))
  await wait()

  assert.deepEqual(loopSensor.consumeAbortCause(first, 'ses_a'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  assert.deepEqual(loopSensor.consumeAbortCause(second, 'ses_a'), { cause: 'External' })
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

test('WHAT[DG-007] LOOP_006_low_side_interrupts_once_but_does_not_continue_before_reconcile', async () => {
  const aborts = []
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_low'],
    abort: (session) => aborts.push(session),
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_low', repetitiveText()))
  await wait()
  loopSensor.observe(sensor, loopSensor.textDelta('ses_low', repetitiveText()))
  await wait()

  assert.deepEqual(aborts, ['ses_low'])
  assert.deepEqual(continuations, [], 'abort completion is not the continuation fence')

  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_low'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_low', 'TooRepetitive']])

  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_low'), { cause: 'External' })
  await wait()
  assert.deepEqual(continuations, [['ses_low', 'TooRepetitive']], 'cause and continuation are one-shot')
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

  loopSensor.observe(sensor, rawDelta('ses_high', 'thinking', text))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_high'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRandom',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_high', 'TooRandom']])
})

test('WHAT[DG-007] LOOP_006_abort_failure_rolls_back_guard_ownership', async () => {
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_fail'],
    abort: () => ({ ok: false, error: 'host refused interrupt' }),
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_fail', repetitiveText()))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_fail'), { cause: 'External' })
  assert.deepEqual(continuations, [])
})

test('WHAT[DG-010] LOOP_007_unowned_session_never_interrupts', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_owned'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_stranger', repetitiveText()))
  await wait()
  assert.deepEqual(aborts, [])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_stranger'), { cause: 'External' })
})

test('WHAT[DG-006] LOOP_006_attempt_reset_preserves_armed_cause_until_reconcile', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_idle'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', repetitiveText()))
  await wait()
  loopSensor.resetDetector(sensor, 'ses_idle')
  loopSensor.observe(sensor, loopSensor.textDelta('ses_idle', repetitiveText()))
  await wait()

  assert.deepEqual(aborts, ['ses_idle'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_idle'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
})

test('WHAT[DG-011] LOOP_006_anomaly_resources_preserve_distinct_recovery_meanings', () => {
  assert.equal(
    providerLanguage.readText('SimplifiedChinese', 'runtime/degeneration-too-repetitive').trim(),
    '你的输出重复字符太多，建议更换表述方式。',
  )
  assert.equal(
    providerLanguage.readText('SimplifiedChinese', 'runtime/degeneration-too-random').trim(),
    '你的输出重复字符太少，不符合正常语料模式，建议更换表述方式。',
  )

  assert.match(
    providerLanguage.readText('English', 'runtime/degeneration-too-repetitive'),
    /too many repeated characters/i,
  )
  assert.match(
    providerLanguage.readText('English', 'runtime/degeneration-too-random'),
    /too few repeated characters/i,
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
  assert.doesNotMatch(ordinarySource, /continueAfterLoopKill/)
  assert.doesNotMatch(fallbackSource, /continueAfterLoopKill|LoopContinue/)
})

test('WHAT[DG-012] LOOP_012_degeneration_guard_is_the_single_closed_recovery_owner', async () => {
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_closed'],
    abort: () => {},
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_closed', repetitiveText()))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_closed'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_closed', 'TooRepetitive']])

  const ordinarySource = readFileSync(
    join(root, 'src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs'),
    'utf8',
  )
  const fissionSource = readFileSync(join(root, 'src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs'), 'utf8')

  assert.match(ordinarySource, /AbortCause\.DegenerationGuard _ -> AsyncSupport\.completedTask \(\)/)
  assert.match(
    fissionSource,
    /TurnAborted _, AbortCause\.DegenerationGuard _ ->[\s\S]{0,120}DegenerationInterrupted/,
  )
})
