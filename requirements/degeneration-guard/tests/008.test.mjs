import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, createSensor, rawDelta, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-008] separately constructed sensor instances do not share armed anomalies', async () => {
  const options = { owned: ['session'], abort: () => {}, continue: () => {} }
  const first = createSensor(options)
  const second = createSensor(options)
  sensor.observe(first, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(first, 'session', 'run')
  assert.deepEqual(await sensor.consumeAbortCause(second, 'session', 'run'), { cause: 'External' })
  assert.deepEqual(await sensor.consumeAbortCause(first, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(first, 'session', 'run')
})

test('WHAT[degeneration-guard-008] wrong run and wrong session cannot consume the exact armed cause or its owned task', async () => {
  const continuations = []
  const handle = createSensor({ owned: ['session', 'other'], abort: () => {}, continue: (...args) => continuations.push(args) })
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  for (const [id, run] of [['session', 'old-run'], ['other', 'run']]) {
    assert.deepEqual(await sensor.consumeAbortCause(handle, id, run), { cause: 'External' })
    assert.equal(sensor.activeTask(handle, id, run), null)
  }
  assert.notEqual(sensor.activeTask(handle, 'session', 'run'), null)
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
})

test('WHAT[degeneration-guard-008] deltas without a physical message identity cannot arm or interrupt', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  for (const message of [undefined, null]) sensor.observe(handle, sensor.textDelta('session', repetitiveText(), message))
  const missing = rawDelta('session', 'text', repetitiveText())
  delete missing.properties.messageID
  sensor.observe(handle, missing)
  assert.deepEqual(aborts, [])
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'never-observed'), { cause: 'External' })
  assert.equal(sensor.activeTask(handle, 'session', 'never-observed'), null)
})

test.todo('WHAT[degeneration-guard-008] real process restart discards armed state and real Host reconciliation preserves exact physical run identity (GAP-145)')
