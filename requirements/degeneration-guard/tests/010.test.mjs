import assert from 'node:assert/strict'
import test from 'node:test'

import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

test('WHAT[DG-010] LOOP_007_unowned_session_never_interrupts', async () => {
  const aborts = []
  const sensor = createSensor({
    owned: ['ses_owned'],
    abort: (session) => aborts.push(session),
    continue: () => {},
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_stranger', repetitiveText(), 'msg_stranger_1'))
  await wait()
  assert.deepEqual(aborts, [])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_stranger', 'msg_stranger_1'), { cause: 'External' })
})
