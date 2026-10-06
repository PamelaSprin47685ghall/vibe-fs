import assert from 'node:assert/strict'
import test from 'node:test'
import * as detector from '../../../dist/Execution/Session/LoopDetectorSurface.js'
import * as sensor from '../../../dist/OpenCode/Host/LoopSensorSurface.js'
import { awaitOwned, chaoticText, createSensor, deferred, rawDelta, repetitiveText } from './support/stream.mjs'

test('WHAT[degeneration-guard-006] separately created detectors have independent state', () => {
  const first = detector.create()
  const second = detector.create()
  detector.pushText(first, repetitiveText())
  assert.equal(detector.evaluate(first).state, 'TooRepetitive')
  assert.deepEqual(detector.evaluate(second), detector.evaluate(detector.create()))
})

test('WHAT[degeneration-guard-006] explicit detector reset preserves the armed cause until exact reconciliation', async () => {
  const aborts = []
  const handle = createSensor({ owned: ['session'], abort: id => aborts.push(id), continue: () => {} })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.resetDetector(handle, 'session')
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  assert.deepEqual(aborts, ['session'])
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'DegenerationGuard', anomaly: 'TooRepetitive' })
  await sensor.activeTask(handle, 'session', 'run')
})

test('WHAT[degeneration-guard-006] dropping a session after its interrupt completed removes local ownership', async () => {
  const handle = createSensor({ owned: ['session'], abort: () => {}, continue: () => {} })
  sensor.observe(handle, sensor.textDelta('session', repetitiveText(), 'run'))
  await awaitOwned(handle, 'session', 'run')
  sensor.dropSession(handle, 'session')
  assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'run'), { cause: 'External' })
  assert.equal(sensor.activeTask(handle, 'session', 'run'), null)
})

const normalText = ' const result = await repository.findById(orderId);'

const armedEvaluations = diagnostics => diagnostics
  .filter(([operation]) => operation === 'degeneration-guard')
  .map(([, fields]) => Object.fromEntries(fields))
  .filter(fields => fields.result === 'armed')

const assertFreshEvaluation = (fields, expected) => {
  assert.equal(Number(fields.detector_step), expected.step)
  assert.ok(
    Math.abs(Number(fields.weighted_distinct_token_count) - expected.weightedDistinctTokens) <= 0.0001,
    'weighted count must match a fresh detector within the four-decimal diagnostic precision',
  )
}

const consumeAndDrain = async (handle, session, run, kind) => {
  await awaitOwned(handle, session, run)
  assert.deepEqual(await sensor.consumeAbortCause(handle, session, run), { cause: 'DegenerationGuard', anomaly: kind })
  await sensor.activeTask(handle, session, run)
  assert.equal(sensor.activeTask(handle, session, run), null)
  assert.deepEqual(await sensor.consumeAbortCause(handle, session, run), { cause: 'External' })
}

for (const [kind, field, anomalyText] of [
  ['TooRepetitive', 'text', repetitiveText],
  ['TooRandom', 'thinking', chaoticText],
]) {
  test(
    'WHAT[degeneration-guard-006] ' + kind + ' truncation starts fresh statistics through three consume/drain cycles',
    async () => {
      const text = anomalyText()
      const aborts = []
      const continuations = []
      const diagnostics = []
      const handle = createSensor({
        owned: ['session'], abort: id => aborts.push(id),
        continue: (...args) => continuations.push(args),
        diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
      })
      for (const cycle of [1, 2, 3]) {
        const run = 'run-' + cycle
        const fresh = detector.create()
        assert.equal(detector.pushText(fresh, normalText).state, 'Normal')
        sensor.observe(handle, rawDelta('session', field, normalText, run))
        assert.equal(aborts.length, cycle - 1, 'short normal output must not inherit the truncated anomaly')
        assert.equal(sensor.activeTask(handle, 'session', run), null)

        const expected = detector.pushText(fresh, text)
        assert.equal(expected.state, kind)
        sensor.observe(handle, rawDelta('session', field, text, run))
        assert.equal(aborts.length, cycle, 'a later genuine anomaly must still interrupt')
        const armed = armedEvaluations(diagnostics)
        assert.equal(armed.length, cycle)
        assertFreshEvaluation(armed.at(-1), expected)
        await consumeAndDrain(handle, 'session', run, kind)
        assert.deepEqual(continuations, Array.from({ length: cycle }, () => ['session', kind]))
      }
      sensor.observe(handle, rawDelta('session', field, normalText, 'run-4'))
      assert.deepEqual(aborts, ['session', 'session', 'session'])
      assert.equal(sensor.activeTask(handle, 'session', 'run-4'), null)
    },
  )

  test('WHAT[degeneration-guard-006] ' + kind + ' truncation preserves another session with the same run identity', async () => {
    const text = anomalyText()
    const otherDetector = detector.create()
    assert.equal(detector.pushText(otherDetector, normalText).state, 'Normal')
    const aborts = []
    const continuations = []
    const diagnostics = []
    const handle = createSensor({
      owned: ['session', 'other'], abort: id => aborts.push(id),
      continue: (...args) => continuations.push(args),
      diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
    })
    sensor.observe(handle, rawDelta('other', field, normalText, 'shared-run'))
    sensor.observe(handle, rawDelta('session', field, text, 'shared-run'))
    await consumeAndDrain(handle, 'session', 'shared-run', kind)
    assert.deepEqual(aborts, ['session'])
    assert.deepEqual(continuations, [['session', kind]])

    assert.equal(detector.pushText(otherDetector, normalText).state, 'Normal')
    sensor.observe(handle, rawDelta('other', field, normalText, 'shared-run'))
    assert.deepEqual(aborts, ['session'])
    const expected = detector.pushText(otherDetector, text)
    assert.equal(expected.state, kind)
    sensor.observe(handle, rawDelta('other', field, text, 'shared-run'))
    assert.deepEqual(aborts, ['session', 'other'])
    const armed = armedEvaluations(diagnostics)
    assert.equal(armed.length, 2)
    assert.equal(armed[1].session_id, 'other')
    assertFreshEvaluation(armed[1], expected)
    await consumeAndDrain(handle, 'other', 'shared-run', kind)
    assert.deepEqual(continuations, [['session', kind], ['other', kind]])
  })

  test('WHAT[degeneration-guard-006] late ' + kind + ' deltas from a truncated run cannot contaminate new statistics', async () => {
    const text = anomalyText()
    const continuation = deferred()
    const aborts = []
    const continuations = []
    const diagnostics = []
    const handle = createSensor({
      owned: ['session'], abort: id => aborts.push(id),
      continue: (...args) => {
        continuations.push(args)
        return continuations.length === 1 ? continuation.promise : { ok: true }
      },
      diagnostic: (operation, fields) => diagnostics.push([operation, fields]),
    })
    sensor.observe(handle, rawDelta('session', field, text, 'old-run'))
    await awaitOwned(handle, 'session', 'old-run')
    assert.deepEqual(await sensor.consumeAbortCause(handle, 'session', 'old-run'), { cause: 'DegenerationGuard', anomaly: kind })
    const continued = sensor.activeTask(handle, 'session', 'old-run')
    assert.notEqual(continued, null)
    try {
      sensor.observe(handle, rawDelta('session', 'text', text, 'old-run'))
      assert.equal(sensor.activeTask(handle, 'session', 'old-run'), continued)
    } finally {
      continuation.resolve({ ok: true })
      await continued
    }
    assert.equal(sensor.activeTask(handle, 'session', 'old-run'), null)

    const fresh = detector.create()
    assert.equal(detector.pushText(fresh, normalText).state, 'Normal')
    sensor.observe(handle, rawDelta('session', field, normalText, 'new-run'))
    sensor.observe(handle, rawDelta('session', 'thinking', text, 'old-run'))
    assert.equal(sensor.activeTask(handle, 'session', 'old-run'), null)
    assert.equal(detector.pushText(fresh, normalText).state, 'Normal')
    sensor.observe(handle, rawDelta('session', field, normalText, 'new-run'))
    assert.deepEqual(aborts, ['session'], 'late text/reasoning deltas must neither re-arm nor poison the new run')
    assert.equal(sensor.activeTask(handle, 'session', 'new-run'), null)

    const expected = detector.pushText(fresh, text)
    assert.equal(expected.state, kind)
    sensor.observe(handle, rawDelta('session', field, text, 'new-run'))
    assert.deepEqual(aborts, ['session', 'session'])
    const armed = armedEvaluations(diagnostics)
    assert.equal(armed.length, 2)
    assertFreshEvaluation(armed[1], expected)
    await consumeAndDrain(handle, 'session', 'new-run', kind)
    assert.deepEqual(continuations, [['session', kind], ['session', kind]])
  })
}

test.todo('WHAT[degeneration-guard-006] actual Host run transitions reset detector state and session deletion drains in-flight guard tasks (GAP-145)')
