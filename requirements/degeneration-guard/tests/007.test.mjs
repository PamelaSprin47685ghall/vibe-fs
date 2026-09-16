import assert from 'node:assert/strict'
import test from 'node:test'

import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

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
