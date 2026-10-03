import assert from 'node:assert/strict'
import test from 'node:test'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, repetitiveText } from './support/stream.mjs'

const execute = async (diagnosticThrows, continuationFails) => {
  const trace = []
  const diagnostics = []
  const handle = sensor.create({
    owned: ['session'],
    diagnostic: (operation, fields) => {
      diagnostics.push([operation, fields])
      if (diagnosticThrows) throw new Error('diagnostic unavailable')
    },
    abort: id => { trace.push(['interrupt', id]); return { ok: true } },
    continue: (id, kind) => {
      trace.push(['continue', id, kind])
      return continuationFails ? { ok: false, error: 'refused' } : { ok: true }
    },
  })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  trace.push(['cause', await sensor.consumeAbortCause(handle, 'session', 'run')])
  await sensor.activeTask(handle, 'session', 'run')
  trace.push(['duplicate', await sensor.consumeAbortCause(handle, 'session', 'run')])
  trace.push(['remaining', sensor.activeTask(handle, 'session', 'run')])
  assert.ok(diagnostics.length > 0)
  return { trace, diagnostics }
}

test('WHAT[degeneration-guard-013] the diagnostic capability is mandatory', () => {
  assert.throws(() => sensor.create({ owned: ['session'], abort: () => {}, continue: () => {} }), /requires a diagnostic callback/)
})

for (const continuationFails of [false, true]) {
  test(`WHAT[degeneration-guard-013] diagnostic exceptions preserve actual control trace and continuation failure=${continuationFails}`, async () => {
    const normal = await execute(false, continuationFails)
    const failed = await execute(true, continuationFails)
    assert.deepEqual(failed, normal)
    assert.deepEqual(normal.trace, [
      ['interrupt', 'session'],
      ['continue', 'session', 'TooRepetitive'],
      ['cause', { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' }],
      ['duplicate', { cause: 'External' }],
      ['remaining', null],
    ])
  })
}
