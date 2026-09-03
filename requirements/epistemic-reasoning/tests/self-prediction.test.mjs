import test from 'node:test'
import assert from 'node:assert/strict'

import { loadGecSurface } from './gec-support.mjs'

test('WHAT[EPI-025] epsilon-clipped-log-score-stays-finite-on-zero-probability', async () => {
  const gecSurface = await loadGecSurface()
  const clipped = await gecSurface.selfPrediction({
    workId: 'work_001',
    predicted: { a: 0, b: 1 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(clipped.ok, true)
  assert.equal(clipped.workId, 'work_001')
  assert.equal(clipped.epsilon, 0.001)
  assert.ok(Number.isFinite(clipped.logScore))
  assert.ok(Math.abs(clipped.logScore - Math.log(0.001)) < 1e-12)
  const exact = await gecSurface.selfPrediction({
    workId: 'work_001',
    predicted: { a: 0.7, b: 0.3 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(exact.ok, true)
  assert.ok(Math.abs(exact.logScore - Math.log(0.7)) < 1e-12)
})

test('WHAT[EPI-025] brier-score-on-valid-simplex-computes-squared-error', async () => {
  const gecSurface = await loadGecSurface()
  const result = await gecSurface.selfPrediction({
    workId: 'work_002',
    predicted: { a: 0.7, b: 0.2, c: 0.1 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(result.ok, true)
  assert.ok(Math.abs(result.brierScore - 0.14) < 1e-12)
})

test('WHAT[EPI-025] brier-score-rejects-prediction-outside-the-simplex', async () => {
  const gecSurface = await loadGecSurface()
  const negative = await gecSurface.selfPrediction({
    workId: 'work_003',
    predicted: { a: -0.2, b: 1.2 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(negative.ok, false)
  assert.match(negative.error, /simplex/i)
  const unnormalized = await gecSurface.selfPrediction({
    workId: 'work_003',
    predicted: { a: 0.8, b: 0.8 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(unnormalized.ok, false)
  assert.match(unnormalized.error, /simplex/i)
})

test('WHAT[EPI-025] commit-before-reveal-rejects-unsealed-prediction-and-binds-work', async () => {
  const gecSurface = await loadGecSurface()
  const sealed = await gecSurface.selfPrediction({
    workId: 'work_004',
    predicted: { a: 0.6, b: 0.4 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: true,
    heldOut: false,
  })
  assert.equal(sealed.ok, true)
  assert.equal(sealed.workId, 'work_004')
  const unsealed = await gecSurface.selfPrediction({
    workId: 'work_004',
    predicted: { a: 0.6, b: 0.4 },
    outcome: 'a',
    epsilon: 0.001,
    committedBeforeStimulus: false,
    heldOut: false,
  })
  assert.equal(unsealed.ok, false)
  assert.match(unsealed.error, /commit|reveal|seal/i)
})

test('WHAT[EPI-025] raw-score-keeps-calibration-sharpness-separate-and-held-out-gates-update', async () => {
  const gecSurface = await loadGecSurface()
  const base = {
    workId: 'work_005',
    predicted: { a: 0.6, b: 0.4 },
    outcome: 'b',
    epsilon: 0.001,
    committedBeforeStimulus: true,
  }
  const inSample = await gecSurface.selfPrediction({ ...base, heldOut: false })
  assert.equal(inSample.ok, true)
  assert.ok('calibration' in inSample)
  assert.ok('sharpness' in inSample)
  assert.ok(!('answer' in inSample))
  assert.equal(inSample.calibrationUpdateAllowed, false)
  const heldOut = await gecSurface.selfPrediction({ ...base, heldOut: true })
  assert.equal(heldOut.ok, true)
  assert.equal(heldOut.calibrationUpdateAllowed, true)
  assert.ok(Number.isFinite(heldOut.sharpness))
})
