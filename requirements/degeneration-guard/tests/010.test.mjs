import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, awaitOwned, createSensor, requestInterrupt } from './support/stream.mjs'

test('WHAT[degeneration-guard-010] supplied eligibility rejects one session and admits a positive control', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['owned'], abort: id => aborts.push(id), continue: () => {} })
  assert.equal(sensor.requestInterrupt(handle, 'stranger', 'run', 'TooRepetitive'), false,
    'an explicit request outside the owned scope never arms')
  assert.deepEqual(aborts, [])
  assert.equal(sensor.activeTask(handle, 'stranger', 'run'), null)
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('stranger', 'run')), { cause: 'External' })
  requestInterrupt(handle, 'owned', 'run', 'TooRepetitive')
  await awaitOwned(handle, 'owned', 'run')
  assert.deepEqual(aborts, ['owned'])
})

test.todo('WHAT[degeneration-guard-010] actual Host exempts roots compaction and unmanaged internal runs while retaining owned managed children (GAP-145)')
