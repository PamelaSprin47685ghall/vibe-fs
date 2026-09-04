import assert from 'node:assert/strict'
import test from 'node:test'

import * as loopSensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { assertOptionalObservationNoninterference } from '../../structured-workflow/tests/support/m6-boundary-proof.mjs'

test('WHAT[DG-013] diagnostic failure cannot alter loop guard arm interrupt consume or continuation', async () => {
  assert.throws(
    () => loopSensor.create({ owned: ['session-1'], abort: () => {}, continue: () => {} }),
    /requires a diagnostic callback/,
  )

  const diagnostics = []
  const aborts = []
  const continuations = []
  const sensor = loopSensor.create({
    owned: ['session-1'],
    diagnostic: (operation) => {
      diagnostics.push(operation)
      throw new Error('diagnostic unavailable')
    },
    abort: (session) => aborts.push(session),
    continue: (session, kind) => continuations.push([session, kind]),
  })

  loopSensor.observe(sensor, loopSensor.textDelta('session-1', ' retry'.repeat(2000)))
  await new Promise(setImmediate)
  assert.deepEqual(aborts, ['session-1'])
  assert.deepEqual(loopSensor.consumeAbortCause(sensor, 'session-1'), {
    cause: 'DegenerationGuard',
    anomaly: 'TooRepetitive',
  })
  await new Promise(setImmediate)
  assert.deepEqual(continuations, [['session-1', 'TooRepetitive']])
  assert.deepEqual(diagnostics, ['degeneration-guard', 'degeneration-guard', 'degeneration-guard'])
  await assertOptionalObservationNoninterference()
})
