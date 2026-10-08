import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, awaitOwned, createSensor, deferred, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-007] a successful interrupt is single-flight and continuation waits for explicit reconciliation', async () => {
  const aborts = []
  const continuations = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: (...args) => continuations.push(args) })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  assert.deepEqual(aborts, ['session'])
  assert.deepEqual(continuations, [])
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(aborts, ['session'])
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
    assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
    assert.deepEqual(continuations, [])
    assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
    assert.ok(diagnostics.some(([, fields]) => fields.some(([name, value]) => name === 'result' && value === 'failed')))
  })
}

for (const failure of ['refused', 'thrown']) {
  test(`WHAT[degeneration-guard-007] ${failure} interruption cannot cause a second interrupt for the same run`, async () => {
    const aborts = []
    const handle = createSensor({
      owned: ['session'],
      abort: id => {
        aborts.push(id)
        if (failure === 'thrown') throw new Error('transport failed')
        return { ok: false, error: 'refused' }
      },
      continue: () => assert.fail('a refused interrupt cannot send continuation'),
    })
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    await awaitOwned(handle, 'session', 'run')
    sensor.resetDetector(handle, 'session')
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    await sensor.activeTask(handle, 'session', 'run')
    assert.deepEqual(aborts, ['session'])
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'next-run'))
    await awaitOwned(handle, 'session', 'next-run')
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    await sensor.activeTask(handle, 'session', 'run')
    assert.deepEqual(aborts, ['session', 'session'])
  })
}

test('WHAT[degeneration-guard-007] reconciled abort waits for interrupt acceptance before continuing exactly once', async () => {
  const interrupt = deferred()
  const continuation = deferred()
  const continuations = []
  const handle = createSensor({
    owned: ['session'], abort: () => interrupt.promise,
    continue: (...args) => { continuations.push(args); return continuation.promise },
  })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  const interrupted = sensor.activeTask(handle, 'session', 'run')
  assert.notEqual(interrupted, null)
  const cause = sensor.consumeAbortCause(handle, abortSource('session', 'run'))
  try {
    assert.deepEqual(continuations, [])
    assert.equal(sensor.activeTask(handle, 'session', 'run'), interrupted)
    interrupt.resolve({ ok: true })
    assert.deepEqual(await cause, { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
    assert.deepEqual(continuations, [['session', 'TooRepetitive']])
    const continued = sensor.activeTask(handle, 'session', 'run')
    assert.notEqual(continued, null)
    continuation.resolve({ ok: true })
    await continued
    assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
    assert.deepEqual(continuations, [['session', 'TooRepetitive']])
  } finally {
    interrupt.resolve({ ok: true })
    continuation.resolve({ ok: true })
    await interrupted
    await cause
  }
})

for (const failure of ['refused', 'thrown']) {
  test(`WHAT[degeneration-guard-007] pending reconciliation remains external when interruption is ${failure}`, async () => {
    const interrupt = deferred()
    const continuations = []
    const handle = createSensor({
      owned: ['session'], abort: () => interrupt.promise,
      continue: (...args) => continuations.push(args),
    })
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    const interrupted = sensor.activeTask(handle, 'session', 'run')
    const cause = sensor.consumeAbortCause(handle, abortSource('session', 'run'))
    if (failure === 'thrown') interrupt.reject(new Error('transport failed'))
    else interrupt.resolve({ ok: false, error: 'refused' })
    await interrupted
    assert.deepEqual(await cause, { cause: 'External' })
    assert.deepEqual(continuations, [])
    assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
  })
}

for (const outcome of ['accepted', 'refused', 'thrown']) {
  test(`WHAT[degeneration-guard-007] ${outcome} retired interrupt cannot consume or erase a replacement with the same run identity`, async () => {
    const retiredInterrupt = deferred()
    const replacementInterrupt = deferred()
    const continuations = []
    let aborts = 0
    const handle = createSensor({
      owned: ['session'],
      abort: () => ++aborts === 1 ? retiredInterrupt.promise : replacementInterrupt.promise,
      continue: (...args) => continuations.push(args),
    })
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    const retiredTask = sensor.activeTask(handle, 'session', 'run')
    const retiredCause = sensor.consumeAbortCause(handle, abortSource('session', 'run'))
    sensor.dropSession(handle, 'session')
    sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
    const replacementTask = sensor.activeTask(handle, 'session', 'run')
    assert.notEqual(replacementTask, null)
    assert.notEqual(replacementTask, retiredTask)
    try {
      if (outcome === 'thrown') retiredInterrupt.reject(new Error('old transport failed'))
      else retiredInterrupt.resolve(outcome === 'accepted' ? { ok: true } : { ok: false, error: 'old request refused' })
      await retiredTask
      assert.deepEqual(await retiredCause, { cause: 'External' })
      assert.equal(sensor.activeTask(handle, 'session', 'run'), replacementTask)
      assert.deepEqual(continuations, [])
      replacementInterrupt.resolve({ ok: true })
      await replacementTask
      assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), {
        cause: 'DegenerationGuard', anomaly: 'TooRepetitive',
      })
      await sensor.activeTask(handle, 'session', 'run')
      assert.deepEqual(continuations, [['session', 'TooRepetitive']])
      assert.equal(aborts, 2)
    } finally {
      retiredInterrupt.resolve({ ok: true })
      replacementInterrupt.resolve({ ok: true })
      await retiredTask
      await replacementTask
      await retiredCause
    }
  })
}
