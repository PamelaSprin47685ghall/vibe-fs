import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, createSensor, deferred, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-009] pending continuation retains an owned task and prevents a second same-session arm', async () => {
  const completion = deferred()
  const aborts = []
  const continuations = []
  const handle = createSensor({
    owned: ['session'], abort: id => aborts.push(id),
    continue: (...args) => { continuations.push(args); return completion.promise },
  })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run-1'))
  await awaitOwned(handle, 'session', 'run-1')
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run-1'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  const owned = sensor.activeTask(handle, 'session', 'run-1')
  assert.notEqual(owned, null)
  try {
    assert.equal(sensor.activeTask(handle, 'session', 'run-2'), null)
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run-2'))
    assert.deepEqual(aborts, ['session'])
    assert.deepEqual(continuations, [['session', 'TooRepetitive']])
  } finally {
    completion.resolve({ ok: true })
    await owned
  }
  assert.equal(sensor.activeTask(handle, 'session', 'run-1'), null)
  sensor.resetDetector(handle, 'session')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run-2'))
  await awaitOwned(handle, 'session', 'run-2')
  assert.deepEqual(aborts, ['session', 'session'])
  sensor.consumeAbortCause(handle, 'session', 'run-2')
  await awaitOwned(handle, 'session', 'run-2')
  assert.deepEqual(continuations, [['session', 'TooRepetitive'], ['session', 'TooRepetitive']])
})

test('WHAT[degeneration-guard-009] actual continuation rejection is diagnosed without a second local recovery', async () => {
  const diagnostics = []
  const continuations = []
  const handle = createSensor({
    owned: ['session'], abort: () => {},
    continue: (...args) => { continuations.push(args); return { ok: false, error: 'known refusal' } },
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.consumeAbortCause(handle, 'session', 'run')
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
  assert.ok(diagnostics.some(([, fields]) => fields.some(([name, value]) => name === 'provider_error' && value === 'known refusal')))
})

test.todo('WHAT[degeneration-guard-009] actual dispatch preserves accepted unknown and definitely refused continuation outcomes and owned-work cancellation (GAP-145)')
