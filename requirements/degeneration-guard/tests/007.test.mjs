import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, createSensor, deferred, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-007] a successful interrupt is single-flight and continuation waits for explicit reconciliation', async () => {
  const aborts = []
  const continuations = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: (...args) => continuations.push(args) })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  assert.deepEqual(aborts, ['session'])
  assert.deepEqual(continuations, [])
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
  assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
})

for (const failure of ['refused', 'thrown']) {
  test(`WHAT[degeneration-guard-007] ${failure} interrupt does not claim an external abort or send continuation`, async () => {
    const continuations = []
    const diagnostics = []
    const handle = createSensor({
      owned: ['session'],
      abort: () => {
        if (failure === 'thrown') throw new Error('transport failed')
        return { ok: false, error: 'Host refused interrupt' }
      },
      continue: (...args) => continuations.push(args),
      diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
    })
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    await awaitOwned(handle, 'session', 'run')
    assert.deepEqual(sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
    assert.deepEqual(continuations, [])
    assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
    assert.ok(diagnostics.some(([, fields]) => fields.some(([name, value]) => name === 'result' && value === 'failed')))
  })
}

test('WHAT[degeneration-guard-007] a failed interruption cannot cause a second interrupt for the same run', { todo: 'GAP-146: failure currently releases the same-run guard' }, async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => { aborts.push(id); return { ok: false, error: 'refused' } }, continue: () => {} })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(aborts, ['session'])
})

test('WHAT[degeneration-guard-007] reconciled abort cannot start continuation while the interrupt is still pending', { todo: 'GAP-146: reconciliation currently starts continuation before interrupt settlement' }, async () => {
  const interrupt = deferred()
  const continuations = []
  const handle = createSensor({ owned: ['session'], abort: () => interrupt.promise, continue: (...args) => continuations.push(args) })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  const interrupted = sensor.activeTask(handle, 'session', 'run')
  assert.notEqual(interrupted, null)
  sensor.consumeAbortCause(handle, 'session', 'run')
  const continued = sensor.activeTask(handle, 'session', 'run')
  try {
    assert.deepEqual(continuations, [])
  } finally {
    interrupt.resolve({ ok: true })
    await interrupted
    await continued
  }
})
