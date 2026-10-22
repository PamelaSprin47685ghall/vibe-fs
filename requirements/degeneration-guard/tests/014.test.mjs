import assert from 'node:assert/strict'
import test from 'node:test'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { abortSource, awaitOwned, chaoticText, createSensor, deferred, rawDelta, repetitiveText, requestInterrupt } from './support/stream.mjs'

const anomalyDiagnostics = diagnostics => diagnostics
  .filter(([operation]) => operation === 'degeneration-guard')
  .map(([, fields]) => Object.fromEntries(fields))
  .filter(fields => fields.result === 'anomaly-observed')

test('WHAT[degeneration-guard-014] authorized repetitive-text generation completes normally and never interrupts', async () => {
  const aborts = []
  const continuations = []
  const diagnostics = []
  const handle = createSensor({
    owned: ['session'],
    abort: id => aborts.push(id),
    continue: (...args) => continuations.push(args),
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null, 'no owned interrupt exists')
  assert.deepEqual(aborts, [], 'the statistic must not call InterruptAttempt')
  assert.deepEqual(continuations, [], 'the statistic must not dispatch a rewrite prompt')
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
  assert.equal(anomalyDiagnostics(diagnostics).length, 1, 'the statistic still reports once as a diagnostic')
})

test('WHAT[degeneration-guard-014] a task full of distinct identifiers completes normally and never interrupts', async () => {
  const aborts = []
  const continuations = []
  const handle = createSensor({
    owned: ['session'],
    abort: id => aborts.push(id),
    continue: (...args) => continuations.push(args),
  })
  sensor.observe(handle, sensor.textDelta('session', chaoticText(), 'run'))
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
  assert.deepEqual(aborts, [], 'distinct identifiers must not interrupt')
  assert.deepEqual(continuations, [])
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'External' })
})

test('WHAT[degeneration-guard-014] an explicitly requested interruption still aborts its exact attempt', async () => {
  const aborts = []
  const continuations = []
  const handle = createSensor({
    owned: ['session'],
    abort: id => aborts.push(id),
    continue: (...args) => continuations.push(args),
  })
  requestInterrupt(handle, 'session', 'run', 'TooRepetitive')
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(aborts, ['session'], 'the explicit request interrupts the exact session')
  // A wrong run cannot consume the armed cause.
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'wrong-run')), { cause: 'External' })
  assert.deepEqual(await sensor.consumeAbortCause(handle, abortSource('session', 'run')), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'run')
  assert.deepEqual(continuations, [['session', 'TooRepetitive']])
})

test('WHAT[degeneration-guard-014] a late delta from an old attempt does not affect the new run statistics', async () => {
  const diagnostics = []
  const handle = createSensor({
    owned: ['session'],
    abort: () => {},
    continue: () => {},
    diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
  })
  // Seed the old run with abnormal text (advisory only).
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'old-run'))
  await new Promise(resolve => setImmediate(resolve))
  assert.equal(anomalyDiagnostics(diagnostics).length, 1)
  // A fresh run with ordinary text, then a late old-run delta.
  sensor.resetDetector(handle, 'session')
  sensor.observe(handle, rawDelta('session', 'text', ' const result = await repository.findById(orderId);', 'new-run'))
  sensor.observe(handle, rawDelta('session', 'text', repetitiveText(), 'old-run'))
  assert.equal(sensor.activeTask(handle, 'session', 'old-run'), null)
  assert.equal(sensor.activeTask(handle, 'session', 'new-run'), null)
  assert.deepEqual(anomalyDiagnostics(diagnostics).length, 1, 'a late old-run delta is not announced on top of the fresh run')
})

for (const continuationFails of [false, true]) {
  test(`WHAT[degeneration-guard-014] diagnostic exceptions leave base execution unchanged (continuationFails=${continuationFails})`, async () => {
    const run = async (diagnosticThrows) => {
      const trace = []
      const handle = sensor.create({
        owned: ['session'],
        diagnostic: (operation, fields) => {
          if (diagnosticThrows) throw new Error('diagnostic unavailable')
        },
        abort: id => { trace.push(['interrupt', id]); return { ok: true } },
        continue: (id, kind) => {
          trace.push(['continue', id, kind])
          return continuationFails ? { ok: false, error: 'refused' } : { ok: true }
        },
      })
      requestInterrupt(handle, 'session', 'run', 'TooRepetitive')
      await awaitOwned(handle, 'session', 'run')
      trace.push(['cause', await sensor.consumeAbortCause(handle, abortSource('session', 'run'))])
      await sensor.activeTask(handle, 'session', 'run')
      trace.push(['remaining', sensor.activeTask(handle, 'session', 'run')])
      return trace
    }
    const normal = await run(false)
    const failed = await run(true)
    assert.deepEqual(failed, normal)
    assert.deepEqual(normal, [
      ['interrupt', 'session'],
      ['continue', 'session', 'TooRepetitive'],
      ['cause', { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' }],
      ['remaining', null],
    ])
  })
}

test('WHAT[degeneration-guard-014] removing the statistic does not change the explicit interrupt path', async () => {
  // The detector surface is the removable statistic; the explicit interruption
  // does not depend on it at all.
  const fresh = detector.create()
  assert.equal(detector.pushText(fresh, repetitiveText()).state, 'TooRepetitive')
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  requestInterrupt(handle, 'session', 'run', 'TooRepetitive')
  await awaitOwned(handle, 'session', 'run')
  assert.deepEqual(aborts, ['session'])
})
