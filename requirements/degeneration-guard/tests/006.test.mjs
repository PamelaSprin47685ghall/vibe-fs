import assert from 'node:assert/strict'
import test from 'node:test'

import * as loopDetector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

test('WHAT[DG-006] LOOP_005_two_detectors_are_independent_attempts', () => {
  const a = loopDetector.create()
  const b = loopDetector.create()

  loopDetector.pushText(a, ' retry'.repeat(1000))
  assert.equal(loopDetector.evaluate(a).state, 'TooRepetitive')
  assert.equal(loopDetector.evaluate(b).isAnomalous, false)
  assert.equal(loopDetector.evaluate(b).state, 'Normal')
  assert.equal(loopDetector.evaluate(b).step, 0)
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

  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_drop', 'msg_drop_1'), { cause: 'External' })
  assert.equal(loopSensor.activeTask(sensor, 'ses_drop', 'msg_drop_1'), null)
})
