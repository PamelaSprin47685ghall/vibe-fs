import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, deferred, requestInterrupt } from './support/stream.mjs'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')

const repetitiveText = () => ' retry'.repeat(2000)

const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

test('WHAT[degeneration-guard-009] LOOP_017_new_attempt_waits_for_active_continuation_drain', async () => {
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

  requestInterrupt(sensor, 'ses_next', 'msg_next_1', 'TooRepetitive')
  await loopSensor.activeTask(sensor, 'ses_next', 'msg_next_1')
  assert.deepEqual(aborts, ['ses_next'])
  assert.deepEqual(await loopSensor.consumeAbortCause(sensor, abortSource('ses_next', 'msg_next_1')), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })

  // Same session, new attempt while the owned continuation is still active: no new arm.
  const ownedContinuation = loopSensor.activeTask(sensor, 'ses_next', 'msg_next_1')
  assert.notEqual(ownedContinuation, null)
  assert.equal(loopSensor.activeTask(sensor, 'ses_next', 'msg_next_2'), null)
  assert.equal(loopSensor.requestInterrupt(sensor, 'ses_next', 'msg_next_2', 'TooRepetitive'), false,
    'an active continuation blocks a same-session re-arm')
  assert.deepEqual(aborts, ['ses_next'])

  // Drain the exact owned task through the production implementation, then retry.
  releaseContinue()
  await ownedContinuation
  assert.equal(loopSensor.activeTask(sensor, 'ses_next', 'msg_next_1'), null)

  requestInterrupt(sensor, 'ses_next', 'msg_next_2', 'TooRepetitive')
  await loopSensor.activeTask(sensor, 'ses_next', 'msg_next_2')
  assert.deepEqual(aborts, ['ses_next', 'ses_next'])
  assert.deepEqual(await loopSensor.consumeAbortCause(sensor, abortSource('ses_next', 'msg_next_2')), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await loopSensor.activeTask(sensor, 'ses_next', 'msg_next_2')
  assert.deepEqual(continuations, [
    ['ses_next', 'TooRepetitive'],
    ['ses_next', 'TooRepetitive'],
  ])
})

test('WHAT[degeneration-guard-009] LOOP_014_active_interrupt_tracked_in_owned_work_lifecycle', async () => {
  let workExecuted = 0
  const continuation = deferred()
  const sensor = loopSensor.create({
    owned: ['ses_work'],
    abort: () => {
      workExecuted += 1
      return { ok: true }
    },
    continue: () => {
      workExecuted += 1
      return continuation.promise
    },
    diagnostic: () => {},
  })

  requestInterrupt(sensor, 'ses_work', 'msg_work_1', 'TooRepetitive')
  assert.equal(workExecuted, 1, 'abortSession was dispatched within owned-work lifecycle')

  const ownedInterrupt = loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1')
  assert.notEqual(ownedInterrupt, null)
  await ownedInterrupt
  assert.deepEqual(await loopSensor.consumeAbortCause(sensor, abortSource('ses_work', 'msg_work_1')), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })

  const ownedContinue = loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1')
  assert.notEqual(ownedContinue, null)
  continuation.resolve({ ok: true })
  await ownedContinue
  assert.equal(workExecuted, 2, 'continueSession was dispatched within owned-work lifecycle')
  assert.equal(loopSensor.activeTask(sensor, 'ses_work', 'msg_work_1'), null)
})

test('WHAT[degeneration-guard-009] LOOP_018_diagnostics_name_kind_not_side', async () => {
  const diagnostics = []
  const sensor = createSensor({
    owned: ['ses_diag'],
    abort: () => {},
    continue: () => {},
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })

  requestInterrupt(sensor, 'ses_diag', 'msg_diag_1', 'TooRepetitive')
  await loopSensor.activeTask(sensor, 'ses_diag', 'msg_diag_1')
  await loopSensor.consumeAbortCause(sensor, abortSource('ses_diag', 'msg_diag_1'))
  await loopSensor.activeTask(sensor, 'ses_diag', 'msg_diag_1')

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
    armed[1].some(([name, value]) => name === 'failure_class' && value === 'too-repetitive'),
    'armed diagnostic carries the degeneration kind',
  )
})

test('WHAT[degeneration-guard-009] LOOP_008_guard_has_no_fallback_or_nudge_recovery_path', () => {
  const sensorSource = readFileSync(join(root, 'src/Wanxiangshu/Execution/Session/LoopSensor.fs'), 'utf8')
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

test('WHAT[degeneration-guard-009] LOOP_019_failed_continuation_send_is_reported_and_leaves_the_guard_reusable', async () => {
  const diagnostics = []
  const attempts = []
  const refusedContinuation = deferred()
  let attempt = 0

  const sensor = createSensor({
    owned: ['ses_retry'],
    abort: () => ({ ok: true }),
    continue: () => {
      attempt += 1
      attempts.push(attempt)

      return attempt === 1 ? refusedContinuation.promise : { ok: true }
    },
    diagnostic: (operation, fields) => diagnostics.push({ operation, fields, fields_json: JSON.stringify(fields ?? '') }),
  })

  requestInterrupt(sensor, 'ses_retry', 'msg_retry_1', 'TooRepetitive')

  const interrupted = loopSensor.activeTask(sensor, 'ses_retry', 'msg_retry_1')
  assert.notEqual(interrupted, null, 'the interrupt is owned work that can be awaited')
  await interrupted

  assert.deepEqual(await loopSensor.consumeAbortCause(sensor, abortSource('ses_retry', 'msg_retry_1')), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })

  const continued = loopSensor.activeTask(sensor, 'ses_retry', 'msg_retry_1')
  assert.notEqual(continued, null, 'the guard continuation is owned work that can be awaited')
  refusedContinuation.resolve({ ok: false, error: 'no active authority profile' })
  await continued

  assert.deepEqual(attempts, [1], 'the guard tried to continue exactly once')

  const reported = diagnostics.filter((entry) => entry.operation === 'degeneration-guard')
  assert.ok(
    reported.some((entry) => entry.fields_json.includes('failed')),
    'a failed continuation must be reported, not swallowed',
  )

  assert.equal(
    loopSensor.activeTask(sensor, 'ses_retry', 'msg_retry_1'),
    null,
    'a failed continuation must not leave a phantom active task behind',
  )

  requestInterrupt(sensor, 'ses_retry', 'msg_retry_2', 'TooRepetitive')

  const secondInterrupt = loopSensor.activeTask(sensor, 'ses_retry', 'msg_retry_2')
  assert.notEqual(secondInterrupt, null, 'the sensor must still interrupt after a failed continuation')
  await secondInterrupt
  assert.deepEqual(
    await loopSensor.consumeAbortCause(sensor, abortSource('ses_retry', 'msg_retry_2')),
    { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' },
    'the second repetition carries its own cause instead of reusing the stale one',
  )
  await loopSensor.activeTask(sensor, 'ses_retry', 'msg_retry_2')
})

test.todo('WHAT[degeneration-guard-009] actual dispatch preserves accepted unknown and definitely refused continuation outcomes and owned-work cancellation (GAP-145)')
