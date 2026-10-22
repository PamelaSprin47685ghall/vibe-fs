import assert from 'node:assert/strict'
import test from 'node:test'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, awaitOwned, chaoticText, createSensor, rawDelta, repetitiveText, requestInterrupt } from './support/stream.mjs'

test('WHAT[degeneration-guard-006] separately created detectors have independent state', () => {
  const first = detector.create()
  const second = detector.create()
  detector.pushText(first, repetitiveText())
  assert.equal(detector.evaluate(first).state, 'TooRepetitive')
  assert.deepEqual(detector.evaluate(second), detector.evaluate(detector.create()))
})

test('WHAT[degeneration-guard-006] an explicit interrupt is preserved across a detector reset until exact reconciliation', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  requestInterrupt(handle, 'session', 'run', 'TooRepetitive')
  await awaitOwned(handle, 'session', 'run')
  sensor.resetDetector(handle, 'session')
  assert.deepEqual(aborts, ['session'])
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'run')
})

test('WHAT[degeneration-guard-006] dropping a session after its interrupt completed removes local ownership', async () => {
  const handle = createSensor({ owned: ['session'], abort: () => {}, continue: () => {} })
  requestInterrupt(handle, 'session', 'run', 'TooRepetitive')
  await awaitOwned(handle, 'session', 'run')
  sensor.dropSession(handle, 'session')
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
})

const normalText = ' const result = await repository.findById(orderId);'

test('WHAT[degeneration-guard-006] detector reset restores a full fresh statistic', () => {
  const fresh = detector.create()
  assert.equal(detector.pushText(fresh, normalText).state, 'Normal')
  const expected = detector.pushText(fresh, repetitiveText())
  assert.equal(expected.state, 'TooRepetitive')
  const reset = detector.create()
  assert.equal(detector.pushText(reset, normalText).state, 'Normal', 'a fresh detector is not contaminated by the truncated anomaly')
})

test('WHAT[degeneration-guard-006] the statistic announces once and never arms', async () => {
  const diagnostics = []
  const aborts = []
  const handle = createSensor({
    owned: ['session'],
    abort: id => aborts.push(id),
    continue: () => {},
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'run'))
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'run'))
  await new Promise(resolve => setImmediate(resolve))
  const observed = diagnostics
    .filter(([operation]) => operation === 'degeneration-guard')
    .map(([, fields]) => Object.fromEntries(fields))
    .filter(fields => fields.result === 'anomaly-observed')
  assert.equal(observed.length, 1, 'the same class is announced once per session')
  assert.deepEqual(aborts, [])
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
})

test('WHAT[degeneration-guard-006] a late delta from an explicitly interrupted run cannot contaminate new statistics', async () => {
  const diagnostics = []
  const continuations = []
  const handle = createSensor({
    owned: ['session'],
    abort: () => {},
    continue: (...args) => continuations.push(args),
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })
  requestInterrupt(handle, 'session', 'old-run', 'TooRepetitive')
  await awaitOwned(handle, 'session', 'old-run')
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'old-run')), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'old-run')
  const observedBefore = diagnostics.filter(([operation]) => operation === 'degeneration-guard').length
  // A late delta from the interrupted old run must not enter the fresh scratch.
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'old-run'))
  sensor.observe(handle, rawDelta('session', 'text', normalText, 'new-run'))
  sensor.observe(handle, rawDelta('session', 'thinking', chaoticText(), 'old-run'))
  const observedAfter = diagnostics
    .filter(([operation]) => operation === 'degeneration-guard')
    .map(([, fields]) => Object.fromEntries(fields))
    .filter(fields => fields.result === 'anomaly-observed')
  assert.equal(observedAfter.length, 0, 'a late old-run delta is not announced on top of the fresh run')
  assert.ok(observedBefore > 0, 'the explicit interrupt path still reports its effects')
})

test.todo('WHAT[degeneration-guard-006] actual Host run transitions reset detector state and session deletion drains in-flight guard tasks (GAP-145)')
