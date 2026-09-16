import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import test from 'node:test'
import { fileURLToPath } from 'node:url'

import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'

const root = join(dirname(fileURLToPath(import.meta.url)), '../../..')
const wait = (ms = 15) => new Promise((resolve) => setTimeout(resolve, ms))
const repetitiveText = () => ' retry'.repeat(2000)
const createSensor = (options) => loopSensor.create({ diagnostic: () => {}, ...options })

test('WHAT[DG-012] LOOP_012_degeneration_guard_is_the_single_closed_recovery_owner', async () => {
  const continuations = []
  const sensor = createSensor({
    owned: ['ses_closed'],
    abort: () => {},
    continue: (session, anomaly) => continuations.push([session, anomaly]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('ses_closed', repetitiveText(), 'msg_closed_1'))
  await wait()
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'ses_closed', 'msg_closed_1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await wait()
  assert.deepEqual(continuations, [['ses_closed', 'TooRepetitive']])

  const ordinarySource = readFileSync(
    join(root, 'src/Wanxiangshu/Composition/Turn/OrdinaryTurnWorkflow.fs'),
    'utf8',
  )
  const fissionSource = readFileSync(join(root, 'src/Wanxiangshu/Execution/Fission/OpenCode/Host.fs'), 'utf8')

  assert.match(ordinarySource, /AbortCause\.DegenerationGuard _ -> AsyncSupport\.completedTask \(\)/)
  assert.match(
    fissionSource,
    /TurnAborted _, AbortCause\.DegenerationGuard _ ->[\s\S]{0,120}DegenerationInterrupted/,
  )
})
