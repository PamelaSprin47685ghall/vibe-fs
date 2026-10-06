import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, awaitOwned, createSensor, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-010] supplied eligibility rejects one session and admits a positive control', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['owned'], abort: id => aborts.push(id), continue: () => {} })
  sensor.observe(handle, sensor.textDelta('stranger', repetitiveText(), 'run'))
  assert.deepEqual(aborts, [])
  assert.equal(sensor.activeTask(handle, 'stranger', 'run'), null)
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('stranger', 'run')), { cause: 'External' })
  sensor.observe(handle, sensor.textDelta('owned', repetitiveText(), 'run'))
  await awaitOwned(handle, 'owned', 'run')
  assert.deepEqual(aborts, ['owned'])
})

test.todo('WHAT[degeneration-guard-010] actual Host exempts roots compaction and unmanaged internal runs while retaining owned managed children (GAP-145)')
