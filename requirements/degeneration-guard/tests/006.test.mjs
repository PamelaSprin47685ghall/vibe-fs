import assert from 'node:assert/strict'
import test from 'node:test'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, createSensor, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-006] separately created detectors have independent state', () => {
  const first = detector.create()
  const second = detector.create()
  detector.pushText(first, repetitiveText())
  assert.equal(detector.evaluate(first).state, 'TooRepetitive')
  assert.deepEqual(detector.evaluate(second), detector.evaluate(detector.create()))
})

test('WHAT[degeneration-guard-006] explicit detector reset preserves the armed cause until exact reconciliation', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.resetDetector(handle, 'session')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  assert.deepEqual(aborts, ['session'])
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await awaitOwned(handle, 'session', 'run')
})

test('WHAT[degeneration-guard-006] dropping a session after its interrupt completed removes local ownership', async () => {
  const handle = createSensor({ owned: ['session'], abort: () => {}, continue: () => {} })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.dropSession(handle, 'session')
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
})

test.todo('WHAT[degeneration-guard-006] actual Host run transitions reset detector state and session deletion drains in-flight guard tasks (GAP-145)')
