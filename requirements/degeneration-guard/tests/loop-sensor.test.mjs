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

test('WHAT[DG-008] LOOP_001_armed_anomaly_is_process_local', async () => {
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

test('WHAT[DG-007] LOOP_006_abort_failure_rolls_back_guard_ownership', async () => {
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

test('WHAT[DG-007] LOOP_006_abort_throw_is_observed_without_recovery', async () => {
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

test('WHAT[DG-010] LOOP_007_unowned_session_never_interrupts', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_owned'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_stranger', repetitiveText(), 'msg_stranger_1'))
  await wait()
  assert.deepEqual(aborts, [])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_stranger', 'msg_stranger_1'), { cause: 'External' })
})

test('WHAT[DG-006] LOOP_006_attempt_reset_preserves_armed_cause_until_reconcile', async () => {
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

test('WHAT[DG-008] LOOP_013_wrong_run_never_consumes_or_clears_newer_anomaly', async () => {
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

test('WHAT[DG-008] LOOP_016_delta_without_message_id_is_observed_only', async () => {
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

test('WHAT[DG-006] LOOP_015_drop_session_cleans_active_tasks_and_detectors', async () => {
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

test('WHAT[DG-012] LOOP_012_degeneration_guard_is_the_single_closed_recovery_owner', async () => {
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_closed'],
    abort: () => {},
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_closed', repetitiveText(), 'msg_closed_1'))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_closed', 'msg_closed_1'), {
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
