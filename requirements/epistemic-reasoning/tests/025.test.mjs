import test from 'node:test'

{
const { default: test } = await import("node:test");
const { default: assert } = await import("node:assert/strict");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");


test('WHAT[EPI-025] epsilon-clipped-log-score-stays-finite-on-zero-probability', async () => {
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
}

{
const { default: assert } = await import("node:assert/strict");
const { default: test } = await import("node:test");
const { gecSurface } = await import("../../../dist/Sphinx/GecSurface.js");

const SEED = 0x51eca129
const WAVES = 12
const SUBJECTS = Array.from({ length: 16 }, (_, index) => `witness-${String(index).padStart(2, '0')}`)
const TREATMENTS = ['wording-a', 'wording-b']
const CANDIDATES = ['c1', 'c2', 'c3']
const PLANTED_EFFECT = 0.15
const xorshift = (initial) => {
  let state = initial >>> 0 || 1
  return () => {
    state ^= state << 13
    state >>>= 0
    state ^= state >>> 17
    state ^= state << 5
    state >>>= 0
    return state / 4294967296
  }
}
const waveInput = (wave) => ({
  rootSnapshot: `snap-soak-${String(wave).padStart(2, '0')}`,
  seed: (SEED + wave * 7919) >>> 0,
  subjects: [...SUBJECTS],
  treatments: [...TREATMENTS],
  candidates: [...CANDIDATES],
})
const waveEvents = (wave, assignment) => {
  const inquiry = `iq_soak${String(wave).padStart(4, '0')}`
  const branch = `branch_soak${String(wave).padStart(4, '0')}`
  const work = `work_soak${String(wave).padStart(4, '0')}`
  const lock = [{ id: 'canon', release: '1.0.0', abiHash: 'abi-canon' }]
  return [
    {
      type: 'InquiryCreated',
      inquiry,
      revision: 0,
      parent: 'none',
      question: 'soak probe',
      pluginLock: lock,
      budget: { compute: 100, budget: 100 },
      root: {
        envelope: { schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' }, payload: { wave } },
        adapter: 'question-to-root:v1',
      },
    },
    {
      type: 'WorkPlanned',
      inquiry,
      revision: 1,
      parent: 'ev0',
      work: { id: work, branch, attempt: 1 },
    },
    {
      type: 'ObservationAccepted',
      inquiry,
      revision: 2,
      parent: 'ev1',
      observation: {
        rootSnapshotHash: `snap-soak-${String(wave).padStart(2, '0')}`,
        branch,
        work,
        attempt: 1,
        pluginLock: lock,
        schema: { id: 'sphinx.probe.open/input@1', hash: 'schema-hash-001' },
        promptId: `prompt-soak-${wave}`,
        questionId: `q-soak-${wave}`,
        wording: { frame: 'open', polarity: 'neutral' },
        permutation: { candidates: [...CANDIDATES], labels: ['A', 'B', 'C'], order: [0, 1, 2] },
        treatment: assignment,
        blindToken: `blind01soakwave${String(wave).padStart(4, '0')}`,
        seed: `seed-soak-${wave}`,
        model: { provider: 'local-sim', name: 'sim-1' },
        sampling: { temperature: 0, maxTokens: 16 },
        usage: { promptTokens: 5, completionTokens: 3 },
        payload: { wave },
      },
    },
    { type: 'BudgetDebited', inquiry, revision: 3, parent: 'ev2', debit: { compute: 7, budget: 7 } },
  ]
}

test('WHAT[EPI-025] soak_scored_forecasts_keep_seal_and_simplex_gates_across_waves', async () => {
  for (let wave = 0; wave < WAVES; wave += 1) {
    const input = waveInput(wave)
    const next = xorshift((input.seed ^ 0x9e3779b9) >>> 0)
    const ballots = Array.from({ length: 6 }, () =>
      next() < 0.5 ? [['c1'], ['c2'], ['c3']] : [['c3'], ['c2'], ['c1']],
    )
    const borda = gecSurface.borda({ candidates: [...CANDIDATES], ballots })
    assert.equal(borda.ok, true)
    const total = Object.values(borda.meanScores).reduce((sum, value) => sum + value, 0)
    const forecast = Object.fromEntries(
      Object.entries(borda.meanScores).map(([candidate, value]) => [candidate, value / total]),
    )
    const mass = Object.values(forecast).reduce((sum, value) => sum + value, 0)
    assert.ok(Math.abs(mass - 1) < 1e-12, `wave ${wave} borda-derived forecast must be a simplex`)
    const workId = `work_soak${String(wave).padStart(4, '0')}`
    const sealedInput = {
      workId,
      predicted: forecast,
      outcome: borda.ranking[0],
      epsilon: 0.01,
      committedBeforeStimulus: true,
      heldOut: false,
    }
    const sealed = gecSurface.selfPrediction(sealedInput)
    assert.equal(sealed.ok, true)
    assert.deepEqual(gecSurface.selfPrediction(sealedInput), sealed, `wave ${wave} sealed scoring must be deterministic`)
    assert.ok(Number.isFinite(sealed.logScore))
    assert.ok(Number.isFinite(sealed.brierScore))
    assert.ok('calibration' in sealed && 'sharpness' in sealed)
    assert.ok(!('answer' in sealed), `wave ${wave} raw score must never render the answer`)
    assert.equal(sealed.calibrationUpdateAllowed, false)
    const heldOut = gecSurface.selfPrediction({ ...sealedInput, heldOut: true })
    assert.equal(heldOut.ok, true)
    assert.equal(heldOut.calibrationUpdateAllowed, true, `wave ${wave} held-out target must gate the calibration update`)
    const unsealed = gecSurface.selfPrediction({ ...sealedInput, committedBeforeStimulus: false })
    assert.equal(unsealed.ok, false)
    assert.match(unsealed.error, /commit|seal|reveal/i)
    const zeroed = { ...forecast, [borda.ranking[0]]: 0 }
    const renorm = Object.values(zeroed).reduce((sum, value) => sum + value, 0)
    for (const candidate of Object.keys(zeroed)) zeroed[candidate] /= renorm
    const floored = gecSurface.selfPrediction({
      workId,
      predicted: zeroed,
      outcome: borda.ranking[0],
      epsilon: 0.01,
      committedBeforeStimulus: true,
      heldOut: false,
    })
    assert.equal(floored.ok, true)
    assert.ok(Math.abs(floored.logScore - Math.log(0.01)) < 1e-12, `wave ${wave} zero-probability outcome must hit the epsilon floor`)
    const unnormalized = gecSurface.selfPrediction({
      ...sealedInput,
      predicted: Object.fromEntries(Object.entries(forecast).map(([candidate, value]) => [candidate, value * 2])),
    })
    assert.equal(unnormalized.ok, false)
    assert.match(unnormalized.error, /simplex/i)
  }
})
}
